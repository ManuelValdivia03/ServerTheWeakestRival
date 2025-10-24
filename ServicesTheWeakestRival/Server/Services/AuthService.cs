using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net.Mail;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;

namespace ServicesTheWeakestRival.Server.Services
{
    public sealed class AuthService : IAuthService
    {
        private const int DEFAULT_CODE_TTL_MINUTES = 10;
        private const int DEFAULT_RESEND_COOLDOWN_SECONDS = 60;
        private const int TOKEN_TTL_HOURS = 24;
        private const byte ACCOUNT_STATUS_ACTIVE = 1;
        private const byte ACCOUNT_STATUS_BLOCKED = 0;
        private const int EMAIL_MAX_LENGTH = 320;
        private const int DISPLAY_NAME_MAX_LENGTH = 80;
        private const int PROFILE_URL_MAX_LENGTH = 500;
        private const int EMAIL_CODE_LENGTH = 6;
        private const int BCRYPT_WORK_FACTOR = 10;

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private static string ConnectionString
        {
            get
            {
                var connection = ConfigurationManager.ConnectionStrings["TheWeakestRivalDb"];
                if (connection == null || string.IsNullOrWhiteSpace(connection.ConnectionString))
                {
                    ThrowFault("CONFIG_ERROR", "Missing connection string 'TheWeakestRivalDb'.");
                }
                return connection.ConnectionString;
            }
        }
        private static readonly int CodeTtlMinutes = ParseIntAppSetting("EmailCodeTtlMinutes", DEFAULT_CODE_TTL_MINUTES);
        private static readonly int ResendCooldownSeconds = ParseIntAppSetting("EmailResendCooldownSeconds", DEFAULT_RESEND_COOLDOWN_SECONDS);

        private static int ParseIntAppSetting(string key, int @default)
        {
            return int.TryParse(ConfigurationManager.AppSettings[key], out int value) ? value : @default;
        }

        private const string SqlExistsAccountByEmail = @"SELECT 1 FROM dbo.Accounts WHERE email = @Email;";
        private const string SqlLastVerification = @"
            SELECT TOP(1) created_at_utc
            FROM dbo.EmailVerifications
            WHERE email = @Email AND used = 0
            ORDER BY created_at_utc DESC;";
        private const string SqlInvalidatePendingVerifications = @"
            UPDATE dbo.EmailVerifications
            SET used = 1, used_at_utc = SYSUTCDATETIME()
            WHERE email = @Email AND used = 0;";
        private const string SqlInsertVerification = @"
            INSERT INTO dbo.EmailVerifications(email, code_hash, expires_at_utc)
            VALUES(@Email, @CodeHash, @ExpiresAtUtc);";

        private const string SqlPickLatestVerification = @"
            SELECT TOP(1) verification_id, expires_at_utc, used
            FROM dbo.EmailVerifications
            WHERE email = @Email AND used = 0
            ORDER BY created_at_utc DESC;";
        private const string SqlValidateVerification = @"
            SELECT CASE WHEN code_hash = @Hash THEN 1 ELSE 0 END
            FROM dbo.EmailVerifications
            WHERE verification_id = @Id;";
        private const string SqlMarkVerificationUsed = @"
            UPDATE dbo.EmailVerifications
            SET used = 1, used_at_utc = SYSUTCDATETIME()
            WHERE verification_id = @Id;";
        private const string SqlIncrementAttempts = @"
            UPDATE dbo.EmailVerifications
            SET attempts = attempts + 1
            WHERE verification_id = @Id;";

        private const string SqlInsertAccount = @"
            INSERT INTO dbo.Accounts (email, password_hash, status, created_at)
            OUTPUT INSERTED.account_id
            VALUES (@Email, @PasswordHash, @Status, SYSUTCDATETIME());";
        private const string SqlInsertUser = @"
            INSERT INTO dbo.Users (user_id, display_name, profile_image_url, created_at)
            VALUES (@UserId, @DisplayName, @ProfileImageUrl, SYSUTCDATETIME());";
        private const string SqlGetAccountByEmail = @"
            SELECT account_id, password_hash, status
            FROM dbo.Accounts
            WHERE email = @Email;";

        private static class ErrorFilters
        {
            public static bool Log(Exception ex, string context = null)
            {
                // TODO: reemplazar por logger real (Serilog/NLog/AppInsights). No lanzar desde aquí.
                return false; // NO manejar: deja que la excepción siga su curso
            }
        }
        public PingResponse Ping(PingRequest request)
        {
            return new PingResponse
            {
                Echo = !string.IsNullOrWhiteSpace(request?.Message) ? request.Message : "pong",
                Utc = DateTime.UtcNow
            };
        }

        public BeginRegisterResponse BeginRegister(BeginRegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Email))
            {
                ThrowFault("INVALID_REQUEST", "Email is required.");
            }

            string email = request.Email.Trim();
            string code = SecurityUtil.CreateNumericCode(EMAIL_CODE_LENGTH);
            byte[] codeHash = SecurityUtil.Sha256(code);
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(CodeTtlMinutes);

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var exists = new SqlCommand(SqlExistsAccountByEmail, connection, transaction))
                    {
                        exists.Parameters.Add("@Email", SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        var found = exists.ExecuteScalar();
                        if (found != null)
                        {
                            ThrowFault("EMAIL_TAKEN", "Email is already registered.");
                        }
                    }

                    using (var last = new SqlCommand(SqlLastVerification, connection, transaction))
                    {
                        last.Parameters.Add("@Email", SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        object lastObj = last.ExecuteScalar();
                        DateTime? lastUtc = (lastObj == null || lastObj == DBNull.Value) ? (DateTime?)null : (DateTime)lastObj;

                        bool isInCooldown = lastUtc.HasValue &&
                                            (DateTime.UtcNow - lastUtc.Value).TotalSeconds < ResendCooldownSeconds;
                        if (isInCooldown)
                        {
                            ThrowFault("TOO_SOON", "Please wait before requesting another code.");
                        }
                    }

                    using (var invalidate = new SqlCommand(SqlInvalidatePendingVerifications, connection, transaction))
                    {
                        invalidate.Parameters.Add("@Email", SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        invalidate.ExecuteNonQuery();
                    }

                    using (var insert = new SqlCommand(SqlInsertVerification, connection, transaction))
                    {
                        insert.Parameters.Add("@Email", SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        insert.Parameters.Add("@CodeHash", SqlDbType.VarBinary, 32).Value = codeHash;
                        insert.Parameters.Add("@ExpiresAtUtc", SqlDbType.DateTime2).Value = expiresAtUtc;
                        insert.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

            try
            {
                EmailSender.SendVerificationCode(email, code, CodeTtlMinutes);
            }
            catch (SmtpException ex) when (ErrorFilters.Log(ex, "BeginRegister.EmailSender"))
            {
                //TODO solo se registra, pero no hay logger aun
            }
            catch (SmtpException ex)
            {
                // Mapeo a Fault 
                ThrowFault("SMTP_ERROR", $"Failed to send verification email: {ex.StatusCode}");
            }

            return new BeginRegisterResponse
            {
                ExpiresAtUtc = expiresAtUtc,
                ResendAfterSeconds = ResendCooldownSeconds
            };
        }

        public RegisterResponse CompleteRegister(CompleteRegisterRequest request)
        {
            if (request == null)
            {
                ThrowFault("INVALID_REQUEST", "Request payload is null.");
            }

            string email = (request.Email ?? string.Empty).Trim();
            string displayName = (request.DisplayName ?? string.Empty).Trim();
            string password = request.Password ?? string.Empty;
            string code = request.Code ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(code))
            {
                ThrowFault("INVALID_REQUEST", "Email, display name, password and code are required.");
            }

            int verificationId;
            DateTime expiresAtUtc;
            bool used;

            byte[] codeHash = SecurityUtil.Sha256(code);

            using (var connection = new SqlConnection(ConnectionString))
            using (var pick = new SqlCommand(SqlPickLatestVerification, connection))
            {
                pick.Parameters.Add("@Email", SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                connection.Open();

                using (var reader = pick.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        ThrowFault("CODE_MISSING", "No pending code. Request a new one.");
                    }

                    verificationId = reader.GetInt32(0);
                    expiresAtUtc = reader.GetDateTime(1);
                    used = reader.GetBoolean(2);
                }
            }

            if (used || expiresAtUtc <= DateTime.UtcNow)
            {
                ThrowFault("CODE_EXPIRED", "Verification code expired. Request a new one.");
            }

            int newAccountId = -1;

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (var validate = new SqlCommand(SqlValidateVerification, connection))
                {
                    validate.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                    validate.Parameters.Add("@Hash", SqlDbType.VarBinary, 32).Value = codeHash;

                    bool ok = Convert.ToInt32(validate.ExecuteScalar()) == 1;
                    if (!ok)
                    {
                        using (var inc = new SqlCommand(SqlIncrementAttempts, connection))
                        {
                            inc.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                            inc.ExecuteNonQuery();
                        }

                        ThrowFault("CODE_INVALID", "Invalid verification code.");
                    }
                }

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        using (var check = new SqlCommand(SqlExistsAccountByEmail, connection, transaction))
                        {
                            check.Parameters.Add("@Email", SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                            var exists = check.ExecuteScalar();
                            if (exists != null)
                            {
                                ThrowFault("EMAIL_TAKEN", "Email is already registered.");
                            }
                        }

                        string passwordHash = HashPasswordBc(password);

                        using (var insertAcc = new SqlCommand(SqlInsertAccount, connection, transaction))
                        {
                            insertAcc.Parameters.Add("@Email", SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                            insertAcc.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 128).Value = passwordHash;
                            insertAcc.Parameters.Add("@Status", SqlDbType.TinyInt).Value = ACCOUNT_STATUS_ACTIVE;
                            newAccountId = Convert.ToInt32(insertAcc.ExecuteScalar());
                        }

                        using (var insertUser = new SqlCommand(SqlInsertUser, connection, transaction))
                        {
                            insertUser.Parameters.Add("@UserId", SqlDbType.Int).Value = newAccountId;
                            insertUser.Parameters.Add("@DisplayName", SqlDbType.NVarChar, DISPLAY_NAME_MAX_LENGTH).Value = displayName;
                            insertUser.Parameters.Add("@ProfileImageUrl", SqlDbType.NVarChar, PROFILE_URL_MAX_LENGTH).Value =
                                string.IsNullOrWhiteSpace(request.ProfileImageUrl)
                                    ? (object)DBNull.Value
                                    : request.ProfileImageUrl;
                            insertUser.ExecuteNonQuery();
                        }

                        using (var mark = new SqlCommand(SqlMarkVerificationUsed, connection, transaction))
                        {
                            mark.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                            mark.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (SqlException ex) when (ErrorFilters.Log(ex, "CompleteRegister.Tx"))
                    {
                        //TODO se registraria en el log pero aun no lo hay
                        throw;
                    }
                    catch (SqlException)
                    {
                        //TODO se registraria en el log pero aun no lo hay
                        throw;
                    }
                }
            }

            if (newAccountId <= 0)
            {
                ThrowFault("DB_ERROR", "Account was not created.");
            }

            var token = IssueToken(newAccountId);
            return new RegisterResponse { UserId = newAccountId, Token = token };
        }

        public RegisterResponse Register(RegisterRequest request)
        {
            if (request == null)
            {
                ThrowFault("INVALID_REQUEST", "Request payload is null.");
            }

            using (var connection = new SqlConnection(ConnectionString))
            using (var check = new SqlCommand(SqlExistsAccountByEmail, connection))
            {
                check.Parameters.Add("@Email", SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = request.Email;
                connection.Open();

                var exists = check.ExecuteScalar();
                if (exists != null)
                {
                    ThrowFault("EMAIL_TAKEN", "Email is already registered.");
                }
            }

            int newAccountId = -1;
            string passwordHash = HashPasswordBc(request.Password);

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        using (var insertAcc = new SqlCommand(SqlInsertAccount, connection, transaction))
                        {
                            insertAcc.Parameters.Add("@Email", SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = request.Email;
                            insertAcc.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 128).Value = passwordHash;
                            insertAcc.Parameters.Add("@Status", SqlDbType.TinyInt).Value = ACCOUNT_STATUS_ACTIVE;
                            newAccountId = Convert.ToInt32(insertAcc.ExecuteScalar());
                        }

                        using (var insertUser = new SqlCommand(SqlInsertUser, connection, transaction))
                        {
                            insertUser.Parameters.Add("@UserId", SqlDbType.Int).Value = newAccountId;
                            insertUser.Parameters.Add("@DisplayName", SqlDbType.NVarChar, DISPLAY_NAME_MAX_LENGTH).Value =
                                string.IsNullOrWhiteSpace(request.DisplayName) ? (object)DBNull.Value : request.DisplayName;
                            insertUser.Parameters.Add("@ProfileImageUrl", SqlDbType.NVarChar, PROFILE_URL_MAX_LENGTH).Value =
                                string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? (object)DBNull.Value : request.ProfileImageUrl;
                            insertUser.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (SqlException ex) when (ErrorFilters.Log(ex, "Register.Tx"))
                    {
                        throw;
                    }
                    catch (SqlException)
                    {
                        throw;
                    }
                }
            }

            var token = IssueToken(newAccountId);
            return new RegisterResponse { UserId = newAccountId, Token = token };
        }

        public LoginResponse Login(LoginRequest request)
        {
            if (request == null)
            {
                ThrowFault("INVALID_REQUEST", "Request payload is null.");
            }

            int userId;
            string storedHash;
            byte status;

            using (var connection = new SqlConnection(ConnectionString))
            using (var get = new SqlCommand(SqlGetAccountByEmail, connection))
            {
                get.Parameters.Add("@Email", SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = request.Email;
                connection.Open();

                using (var reader = get.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        ThrowFault("INVALID_CREDENTIALS", "Email or password is incorrect.");
                    }

                    userId = reader.GetInt32(0);
                    storedHash = reader.GetString(1);
                    status = reader.GetByte(2);
                }
            }

            if (status == ACCOUNT_STATUS_BLOCKED)
            {
                ThrowFault("ACCOUNT_BLOCKED", "Account is blocked.");
            }

            if (!VerifyPasswordBc(request.Password, storedHash))
            {
                ThrowFault("INVALID_CREDENTIALS", "Email or password is incorrect.");
            }

            var token = IssueToken(userId);
            return new LoginResponse { Token = token };
        }

        public void Logout(LogoutRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token))
            {
                return;
            }

            if (TokenCache.TryRemove(request.Token, out var removed))
            {
                using (var connection = new SqlConnection(ConnectionString))
                using (var cmd = new SqlCommand("dbo.usp_Lobby_LeaveAllByUser", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = removed.UserId;
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static AuthToken IssueToken(int userId)
        {
            string tokenValue = Guid.NewGuid().ToString("N");
            DateTime expiresAt = DateTime.UtcNow.AddHours(TOKEN_TTL_HOURS);

            var token = new AuthToken
            {
                UserId = userId,
                Token = tokenValue,
                ExpiresAtUtc = expiresAt
            };

            TokenCache[tokenValue] = token;
            return token;
        }

        private static void ThrowFault(string code, string message)
        {
            var fault = new ServiceFault { Code = code, Message = message };
            throw new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        private static bool IsUniqueViolation(SqlException ex)
        {
            return ex != null && (ex.Number == 2627 || ex.Number == 2601);
        }

        private static string HashPasswordBc(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password ?? string.Empty, workFactor: BCRYPT_WORK_FACTOR);
        }

        private static bool VerifyPasswordBc(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                return false;
            }
            return BCrypt.Net.BCrypt.Verify(password ?? string.Empty, storedHash);
        }
    }
}
