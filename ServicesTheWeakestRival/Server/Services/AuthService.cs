using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net.Mail;
using System.ServiceModel;
using BCrypt.Net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;

namespace ServicesTheWeakestRival.Server.Services
{
    public sealed class AuthService : IAuthService
    {
        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["TheWeakestRivalDb"];
                if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                    ThrowFault("CONFIG_ERROR", "Missing connection string 'TheWeakestRivalDb'.");
                return cs.ConnectionString;
            }
        }

        private static readonly int CodeTtlMinutes = ParseIntAppSetting("EmailCodeTtlMinutes", 10);
        private static readonly int ResendCooldownSeconds = ParseIntAppSetting("EmailResendCooldownSeconds", 60);

        private static int ParseIntAppSetting(string key, int @default)
        {
            int value;
            return int.TryParse(ConfigurationManager.AppSettings[key], out value) ? value : @default;
        }

        public PingResponse Ping(PingRequest request)
        {
            return new PingResponse
            {
                Echo = (request != null && !string.IsNullOrEmpty(request.Message)) ? request.Message : "pong",
                Utc = DateTime.UtcNow
            };
        }

        // ======= NUEVO: ENVÍA CÓDIGO DE VERIFICACIÓN =======
        public BeginRegisterResponse BeginRegister(BeginRegisterRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
                ThrowFault("INVALID_REQUEST", "Email is required.");

            string email = request.Email.Trim();

            const string qExists = @"SELECT 1 FROM dbo.Accounts WHERE email = @Email;";
            const string qLast = @"
                SELECT TOP(1) created_at_utc
                FROM dbo.EmailVerifications
                WHERE email = @Email AND used = 0
                ORDER BY created_at_utc DESC;";
            const string qInvalidate = @"
                UPDATE dbo.EmailVerifications
                SET used = 1, used_at_utc = SYSUTCDATETIME()
                WHERE email = @Email AND used = 0;";
            const string qInsert = @"
                INSERT INTO dbo.EmailVerifications(email, code_hash, expires_at_utc)
                VALUES(@Email, @CodeHash, @ExpiresAtUtc);";

            string code = SecurityUtil.CreateNumericCode(6);
            byte[] codeHash = SecurityUtil.Sha256(code);
            DateTime expires = DateTime.UtcNow.AddMinutes(CodeTtlMinutes);

            using (var cn = new SqlConnection(ConnectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    // 1) ¿ya existe cuenta?
                    using (var cmd = new SqlCommand(qExists, cn, tx))
                    {
                        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;
                        var exists = cmd.ExecuteScalar();
                        if (exists != null)
                            ThrowFault("EMAIL_TAKEN", "Email is already registered.");
                    }

                    // 2) cooldown
                    using (var cmd = new SqlCommand(qLast, cn, tx))
                    {
                        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;
                        object lastObj = cmd.ExecuteScalar();
                        DateTime? last = (lastObj == null || lastObj == DBNull.Value) ? (DateTime?)null : (DateTime)lastObj;
                        if (last.HasValue && (DateTime.UtcNow - last.Value).TotalSeconds < ResendCooldownSeconds)
                            ThrowFault("TOO_SOON", "Please wait before requesting another code.");
                    }

                    // 3) invalidar previos
                    using (var cmd = new SqlCommand(qInvalidate, cn, tx))
                    {
                        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;
                        cmd.ExecuteNonQuery();
                    }

                    // 4) insertar nuevo
                    using (var cmd = new SqlCommand(qInsert, cn, tx))
                    {
                        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;
                        cmd.Parameters.Add("@CodeHash", SqlDbType.VarBinary, 32).Value = codeHash;
                        cmd.Parameters.Add("@ExpiresAtUtc", SqlDbType.DateTime2).Value = expires;
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                }
            }

            // Enviar email fuera de la transacción
            try
            {
                EmailSender.SendVerificationCode(email, code, CodeTtlMinutes);
            }
            catch (SmtpException ex)
            {
                ThrowFault("SMTP_ERROR", "Failed to send verification email: " + ex.StatusCode);
            }
            catch (Exception ex)
            {
                ThrowFault("SMTP_ERROR", "Failed to send verification email: " + ex.Message);
            }

            return new BeginRegisterResponse
            {
                ExpiresAtUtc = expires,
                ResendAfterSeconds = ResendCooldownSeconds
            };
        }

        // ======= NUEVO: COMPLETA REGISTRO CON CÓDIGO =======
        public RegisterResponse CompleteRegister(CompleteRegisterRequest request)
        {
            if (request == null)
                ThrowFault("INVALID_REQUEST", "Request payload is null.");

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

            byte[] codeHash = SecurityUtil.Sha256(code);

            const string qPick = @"
                SELECT TOP(1) verification_id, expires_at_utc, used
                FROM dbo.EmailVerifications
                WHERE email = @Email AND used = 0
                ORDER BY created_at_utc DESC;";
            const string qValidate = @"
                SELECT CASE WHEN code_hash = @Hash THEN 1 ELSE 0 END
                FROM dbo.EmailVerifications WHERE verification_id = @Id;";
            const string qMarkUsed = @"
                UPDATE dbo.EmailVerifications
                SET used = 1, used_at_utc = SYSUTCDATETIME()
                WHERE verification_id = @Id;";

            int verificationId;
            DateTime expiresAtUtc;
            bool used;

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(qPick, cn))
            {
                cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;
                cn.Open();
                using (var rd = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!rd.Read())
                        ThrowFault("CODE_MISSING", "No pending code. Request a new one.");
                    verificationId = rd.GetInt32(0);
                    expiresAtUtc = rd.GetDateTime(1);
                    used = rd.GetBoolean(2);
                }
            }

            if (used || expiresAtUtc <= DateTime.UtcNow)
                ThrowFault("CODE_EXPIRED", "Verification code expired. Request a new one.");

            int newAccountId = -1; // <- inicializado

            using (var cn = new SqlConnection(ConnectionString))
            {
                cn.Open();

                // Validar hash
                using (var v = new SqlCommand(qValidate, cn))
                {
                    v.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                    v.Parameters.Add("@Hash", SqlDbType.VarBinary, 32).Value = codeHash;
                    var ok = Convert.ToInt32(v.ExecuteScalar()) == 1;
                    if (!ok)
                    {
                        using (var inc = new SqlCommand(
                            "UPDATE dbo.EmailVerifications SET attempts = attempts + 1 WHERE verification_id = @Id;", cn))
                        {
                            inc.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                            inc.ExecuteNonQuery();
                        }
                        ThrowFault("CODE_INVALID", "Invalid verification code.");
                    }
                }

                string QueryInsertAccount = @"
                    INSERT INTO dbo.Accounts (email, password_hash, status, created_at)
                    OUTPUT INSERTED.account_id
                    VALUES (@Email, @PasswordHash, 1, SYSUTCDATETIME());";

                string QueryInsertUser = @"
                    INSERT INTO dbo.Users (user_id, display_name, profile_image_url, created_at)
                    VALUES (@UserId, @DisplayName, @ProfileImageUrl, SYSUTCDATETIME());";

                using (var tx = cn.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // idempotencia por si algo creó en paralelo
                        using (var check = new SqlCommand("SELECT 1 FROM dbo.Accounts WHERE email = @Email;", cn, tx))
                        {
                            check.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;
                            var exists = check.ExecuteScalar();
                            if (exists != null)
                                ThrowFault("EMAIL_TAKEN", "Email is already registered.");
                        }

                        var passwordHash = HashPasswordBc(password);

                        using (var cmdAcc = new SqlCommand(QueryInsertAccount, cn, tx))
                        {
                            cmdAcc.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;
                            cmdAcc.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 128).Value = passwordHash;
                            newAccountId = Convert.ToInt32(cmdAcc.ExecuteScalar()); // <- asignado
                        }

                        using (var cmdUsr = new SqlCommand(QueryInsertUser, cn, tx))
                        {
                            cmdUsr.Parameters.Add("@UserId", SqlDbType.Int).Value = newAccountId;
                            cmdUsr.Parameters.Add("@DisplayName", SqlDbType.NVarChar, 80).Value = displayName;
                            cmdUsr.Parameters.Add("@ProfileImageUrl", SqlDbType.NVarChar, 500)
                                .Value = string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? (object)DBNull.Value : request.ProfileImageUrl;
                            cmdUsr.ExecuteNonQuery();
                        }

                        using (var mark = new SqlCommand(qMarkUsed, cn, tx))
                        {
                            mark.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                            mark.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                    catch (SqlException ex)
                    {
                        tx.Rollback();
                        if (IsUniqueViolation(ex))
                            ThrowFault("EMAIL_TAKEN", "Email is already registered.");
                        ThrowFault("DB_ERROR", "Unexpected database error: " + ex.Number);
                    }
                }
            }

            if (newAccountId <= 0)
                ThrowFault("DB_ERROR", "Account was not created.");

            var token = IssueToken(newAccountId);
            return new RegisterResponse { UserId = newAccountId, Token = token };
        }

        // ======= REGISTRO (flujo antiguo 1 paso) =======
        public RegisterResponse Register(RegisterRequest request)
        {
            string QueryVerifyIsNotRegistered = @" SELECT account_id FROM dbo.Accounts WHERE email = @Email;";
            string QueryInsertAccount = @" 
                INSERT INTO dbo.Accounts (email, password_hash, status, created_at) 
                OUTPUT INSERTED.account_id 
                VALUES (@Email, @PasswordHash, 1, SYSUTCDATETIME());";
            string QueryInsertUser = @" 
                INSERT INTO dbo.Users (user_id, display_name, profile_image_url, created_at) 
                VALUES (@UserId, @DisplayName, @ProfileImageUrl, SYSUTCDATETIME());";

            if (request == null)
            {
                ThrowFault("INVALID_REQUEST", "Request payload is null.");
            }

            using (var connection = new SqlConnection(ConnectionString))
            using (var sqlCommand = new SqlCommand(QueryVerifyIsNotRegistered, connection))
            {
                sqlCommand.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = request.Email;
                connection.Open();
                var exists = sqlCommand.ExecuteScalar();
                if (exists != null)
                {
                    ThrowFault("EMAIL_TAKEN", "Email is already registered.");
                }
            }

            var passwordHash = HashPasswordBc(request.Password);
            int newAccountId = -1;

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        using (var sqlCommandInsertAccount = new SqlCommand(QueryInsertAccount, connection, transaction))
                        {
                            sqlCommandInsertAccount.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = request.Email;
                            sqlCommandInsertAccount.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 128).Value = passwordHash;
                            newAccountId = Convert.ToInt32(sqlCommandInsertAccount.ExecuteScalar());
                        }

                        using (var sqlCommandInsertUser = new SqlCommand(QueryInsertUser, connection, transaction))
                        {
                            sqlCommandInsertUser.Parameters.Add("@UserId", SqlDbType.Int).Value = newAccountId;
                            sqlCommandInsertUser.Parameters.Add("@DisplayName", SqlDbType.NVarChar, 80).Value =
                                string.IsNullOrWhiteSpace(request.DisplayName) ? (object)DBNull.Value : request.DisplayName;
                            sqlCommandInsertUser.Parameters.Add("@ProfileImageUrl", SqlDbType.NVarChar, 500).Value =
                                string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? (object)DBNull.Value : request.ProfileImageUrl;
                            sqlCommandInsertUser.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (SqlException sqlException)
                    {
                        transaction.Rollback();
                        if (IsUniqueViolation(sqlException))
                        {
                            ThrowFault("EMAIL_TAKEN", "Email is already registered.");
                        }
                        ThrowFault("DB_ERROR", "Unexpected database error: " + sqlException.Number);
                    }
                }
            }

            var token = IssueToken(newAccountId);
            return new RegisterResponse { UserId = newAccountId, Token = token };
        }

        // ======= LOGIN =======
        public LoginResponse Login(LoginRequest request)
        {
            if (request == null)
            {
                ThrowFault("INVALID_REQUEST", "Request payload is null.");
            }

            const string QueryGetAccountByEmail = @"
                SELECT account_id, password_hash, status 
                FROM dbo.Accounts WHERE email = @Email;";

            int userId;
            string storedHash;
            byte status;

            using (var connection = new SqlConnection(ConnectionString))
            using (var sqlCommand = new SqlCommand(QueryGetAccountByEmail, connection))
            {
                sqlCommand.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = request.Email;
                connection.Open();
                using (var rd = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!rd.Read())
                        ThrowFault("INVALID_CREDENTIALS", "Email or password is incorrect.");
                    userId = rd.GetInt32(0);
                    storedHash = rd.GetString(1);
                    status = rd.GetByte(2);
                }
            }

            if (status == 0)
                ThrowFault("ACCOUNT_BLOCKED", "Account is blocked.");

            if (!VerifyPasswordBc(request.Password, storedHash))
                ThrowFault("INVALID_CREDENTIALS", "Email or password is incorrect.");

            var token = IssueToken(userId);
            return new LoginResponse { Token = token };
        }

        // ======= LOGOUT =======
        // dentro de AuthService
        public void Logout(LogoutRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token))
                return;

            AuthToken removed;
            if (TokenCache.TryRemove(request.Token, out removed))
            {
                // Limpieza best-effort; no lances fault si falla
                try
                {
                    using (var cn = new SqlConnection(ConnectionString))
                    using (var cmd = new SqlCommand("dbo.usp_Lobby_LeaveAllByUser", cn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = removed.UserId;
                        cn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                catch
                {
                    //TODO Ingresar Log 
                }
            }
        }


        // ======= Helpers =======
        private static AuthToken IssueToken(int userId)
        {
            var tokenValue = Guid.NewGuid().ToString("N");
            var expiry = DateTime.UtcNow.AddHours(24);

            var token = new AuthToken
            {
                UserId = userId,
                Token = tokenValue,
                ExpiresAtUtc = expiry
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
            return (ex != null) && (ex.Number == 2627 || ex.Number == 2601);
        }

        private static string HashPasswordBc(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password ?? string.Empty, workFactor: 10);
        }

        private static bool VerifyPasswordBc(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;
            return BCrypt.Net.BCrypt.Verify(password ?? string.Empty, storedHash);
        }
    }
}
