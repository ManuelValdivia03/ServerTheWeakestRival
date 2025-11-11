using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net.Mail;
using System.ServiceModel;
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;

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
        private const int PASSWORD_MIN_LENGTH = 8;

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private const string ERROR_CONFIG = "CONFIG_ERROR";
        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_EMAIL_TAKEN = "EMAIL_TAKEN";
        private const string ERROR_TOO_SOON = "TOO_SOON";
        private const string ERROR_CODE_MISSING = "CODE_MISSING";
        private const string ERROR_CODE_EXPIRED = "CODE_EXPIRED";
        private const string ERROR_CODE_INVALID = "CODE_INVALID";
        private const string ERROR_DB_ERROR = "DB_ERROR";
        private const string ERROR_INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
        private const string ERROR_ACCOUNT_BLOCKED = "ACCOUNT_BLOCKED";
        private const string ERROR_PAYLOAD_NULL = "Request payload is null.";
        private const string ERROR_SMTP = "SMTP_ERROR";

        private const string PARAMETER_EMAIL = "@Email";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(AuthService));

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private static string GetConnectionString()
        {
            var connection = ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (connection == null || string.IsNullOrWhiteSpace(connection.ConnectionString))
            {
                var configurationException = new ConfigurationErrorsException(
                    string.Format(
                        "Missing connection string '{0}'.",
                        MAIN_CONNECTION_STRING_NAME));

                throw ThrowTechnicalFault(
                    ERROR_CONFIG,
                    "Configuration error. Please contact support.",
                    "AuthService.GetConnectionString",
                    configurationException);
            }

            return connection.ConnectionString;
        }

        private static readonly int CodeTtlMinutes = ParseIntAppSetting("EmailCodeTtlMinutes", DEFAULT_CODE_TTL_MINUTES);
        private static readonly int ResendCooldownSeconds = ParseIntAppSetting("EmailResendCooldownSeconds", DEFAULT_RESEND_COOLDOWN_SECONDS);

        private static int ParseIntAppSetting(string key, int @default)
        {
            return int.TryParse(ConfigurationManager.AppSettings[key], out int value) ? value : @default;
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
                throw ThrowFault(ERROR_INVALID_REQUEST, "Email is required.");
            }

            string email = request.Email.Trim();
            string code = SecurityUtil.CreateNumericCode(EMAIL_CODE_LENGTH);
            byte[] codeHash = SecurityUtil.Sha256(code);
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(CodeTtlMinutes);

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var exists = new SqlCommand(AuthSql.Text.EXISTS_ACCOUNT_BY_EMAIL, connection, transaction))
                    {
                        exists.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        var found = exists.ExecuteScalar();
                        if (found != null)
                        {
                            throw ThrowFault(ERROR_EMAIL_TAKEN, "Email is already registered.");
                        }
                    }

                    using (var last = new SqlCommand(AuthSql.Text.LAST_VERIFICATION, connection, transaction))
                    {
                        last.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        object lastObj = last.ExecuteScalar();
                        DateTime? lastUtc = (lastObj == null || lastObj == DBNull.Value) ? (DateTime?)null : (DateTime)lastObj;

                        bool isInCooldown = lastUtc.HasValue &&
                                            (DateTime.UtcNow - lastUtc.Value).TotalSeconds < ResendCooldownSeconds;
                        if (isInCooldown)
                        {
                            throw ThrowFault(ERROR_TOO_SOON, "Please wait before requesting another code.");
                        }
                    }

                    using (var invalidate = new SqlCommand(AuthSql.Text.INVALIDATE_PENDING_VERIFICATIONS, connection, transaction))
                    {
                        invalidate.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        invalidate.ExecuteNonQuery();
                    }

                    using (var insert = new SqlCommand(AuthSql.Text.INSERT_VERIFICATION, connection, transaction))
                    {
                        insert.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
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
            catch (SmtpException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_SMTP,
                    "Failed to send verification email. Please try again later.",
                    "BeginRegister.EmailSender",
                    ex);
            }

            return new BeginRegisterResponse
            {
                ExpiresAtUtc = expiresAtUtc,
                ResendAfterSeconds = ResendCooldownSeconds
            };
        }
        private static AvatarAppearanceDto MapAvatar(UserAvatarEntity entity)
            {
                if (entity == null)
                {
                    return null;
                }

                return new AvatarAppearanceDto
                {
                    BodyColor = (AvatarBodyColor)entity.BodyColor,
                    PantsColor = (AvatarPantsColor)entity.PantsColor,
                    HatType = (AvatarHatType)entity.HatType,
                    HatColor = (AvatarHatColor)entity.HatColor,
                    FaceType = (AvatarFaceType)entity.FaceType,
                    UseProfilePhotoAsFace = entity.UseProfilePhoto
                };
            }
        public RegisterResponse CompleteRegister(CompleteRegisterRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_PAYLOAD_NULL);
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
                throw ThrowFault(ERROR_INVALID_REQUEST, "Email, display name, password and code are required.");
            }

            int verificationId;
            DateTime expiresAtUtc;
            bool used;

            byte[] codeHash = SecurityUtil.Sha256(code);

            using (var connection = new SqlConnection(GetConnectionString()))
            using (var pick = new SqlCommand(AuthSql.Text.PICK_LATEST_VERIFICATION, connection))
            {
                pick.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                connection.Open();

                using (var reader = pick.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        throw ThrowFault(ERROR_CODE_MISSING, "No pending code. Request a new one.");
                    }

                    verificationId = reader.GetInt32(0);
                    expiresAtUtc = reader.GetDateTime(1);
                    used = reader.GetBoolean(2);
                }
            }

            if (used || expiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault(ERROR_CODE_EXPIRED, "Verification code expired. Request a new one.");
            }

            int newAccountId = -1;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                using (var validate = new SqlCommand(AuthSql.Text.VALIDATE_VERIFICATION, connection))
                {
                    validate.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                    validate.Parameters.Add("@Hash", SqlDbType.VarBinary, 32).Value = codeHash;

                    bool ok = Convert.ToInt32(validate.ExecuteScalar()) == 1;
                    if (!ok)
                    {
                        using (var inc = new SqlCommand(AuthSql.Text.INCREMENT_ATTEMPTS, connection))
                        {
                            inc.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                            inc.ExecuteNonQuery();
                        }

                        ThrowFault(ERROR_CODE_INVALID, "Invalid verification code.");
                    }
                }

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        using (var check = new SqlCommand(AuthSql.Text.EXISTS_ACCOUNT_BY_EMAIL, connection, transaction))
                        {
                            check.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                            var exists = check.ExecuteScalar();
                            if (exists != null)
                            {
                                throw ThrowFault(ERROR_EMAIL_TAKEN, "Email is already registered.");
                            }
                        }

                        string passwordHash = HashPasswordBc(password);

                        using (var insertAcc = new SqlCommand(AuthSql.Text.INSERT_ACCOUNT, connection, transaction))
                        {
                            insertAcc.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                            insertAcc.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 128).Value = passwordHash;
                            insertAcc.Parameters.Add("@Status", SqlDbType.TinyInt).Value = ACCOUNT_STATUS_ACTIVE;
                            newAccountId = Convert.ToInt32(insertAcc.ExecuteScalar());
                        }

                        using (var insertUser = new SqlCommand(AuthSql.Text.INSERT_USER, connection, transaction))
                        {
                            insertUser.Parameters.Add("@UserId", SqlDbType.Int).Value = newAccountId;
                            insertUser.Parameters.Add("@DisplayName", SqlDbType.NVarChar, DISPLAY_NAME_MAX_LENGTH).Value = displayName;
                            insertUser.Parameters.Add("@ProfileImageUrl", SqlDbType.NVarChar, PROFILE_URL_MAX_LENGTH).Value =
                                string.IsNullOrWhiteSpace(request.ProfileImageUrl)
                                    ? (object)DBNull.Value
                                    : request.ProfileImageUrl;
                            insertUser.ExecuteNonQuery();
                        }

                        using (var mark = new SqlCommand(AuthSql.Text.MARK_VERIFICATION_USED, connection, transaction))
                        {
                            mark.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                            mark.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (SqlException ex)
                    {
                        throw ThrowTechnicalFault(
                            ERROR_DB_ERROR,
                            "Unexpected database error while completing registration.",
                            "CompleteRegister.Tx",
                            ex);
                    }
                }
            }

            if (newAccountId <= 0)
            {
                throw ThrowFault(ERROR_DB_ERROR, "Account was not created.");
            }

            var token = IssueToken(newAccountId);
            return new RegisterResponse { UserId = newAccountId, Token = token };
        }

        public RegisterResponse Register(RegisterRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_PAYLOAD_NULL);
            }

            using (var connection = new SqlConnection(GetConnectionString()))
            using (var check = new SqlCommand(AuthSql.Text.EXISTS_ACCOUNT_BY_EMAIL, connection))
            {
                check.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = request.Email;
                connection.Open();

                var exists = check.ExecuteScalar();
                if (exists != null)
                {
                    throw ThrowFault(ERROR_EMAIL_TAKEN, "Email is already registered.");
                }
            }

            int newAccountId = -1;
            string passwordHash = HashPasswordBc(request.Password);

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        using (var insertAcc = new SqlCommand(AuthSql.Text.INSERT_ACCOUNT, connection, transaction))
                        {
                            insertAcc.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = request.Email;
                            insertAcc.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 128).Value = passwordHash;
                            insertAcc.Parameters.Add("@Status", SqlDbType.TinyInt).Value = ACCOUNT_STATUS_ACTIVE;
                            newAccountId = Convert.ToInt32(insertAcc.ExecuteScalar());
                        }

                        using (var insertUser = new SqlCommand(AuthSql.Text.INSERT_USER, connection, transaction))
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
                    catch (SqlException ex)
                    {
                        throw ThrowTechnicalFault(
                            ERROR_DB_ERROR,
                            "Unexpected database error while registering.",
                            "Register.Tx",
                            ex);
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
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_PAYLOAD_NULL);
            }

            int userId;
            string storedHash;
            byte status;

            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                using (var get = new SqlCommand(AuthSql.Text.GET_ACCOUNT_BY_EMAIL, connection))
                {
                    get.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = request.Email;
                    connection.Open();

                    using (var reader = get.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (!reader.Read())
                        {
                            throw ThrowFault(ERROR_INVALID_CREDENTIALS, "Email or password is incorrect.");
                        }

                        userId = reader.GetInt32(0);
                        storedHash = reader.GetString(1);
                        status = reader.GetByte(2);
                    }
                }
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB_ERROR,
                    "Unexpected database error while logging in.",
                    "Login.Db",
                    ex);
            }

            if (status == ACCOUNT_STATUS_BLOCKED)
            {
                throw ThrowFault(ERROR_ACCOUNT_BLOCKED, "Account is blocked.");
            }

            if (!VerifyPasswordBc(request.Password, storedHash))
            {
                throw ThrowFault(ERROR_INVALID_CREDENTIALS, "Email or password is incorrect.");
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
                try
                {
                    using (var connection = new SqlConnection(GetConnectionString()))
                    using (var cmd = new SqlCommand("dbo.usp_Lobby_LeaveAllByUser", connection))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = removed.UserId;
                        connection.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (SqlException ex)
                {
                    throw ThrowTechnicalFault(
                        ERROR_DB_ERROR,
                        "Unexpected database error while logging out.",
                        "Logout.LeaveAllByUser",
                        ex);
                }
            }
        }

        public BeginPasswordResetResponse BeginPasswordReset(BeginPasswordResetRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Email))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Email is required.");
            }

            string email = request.Email.Trim();
            string code = SecurityUtil.CreateNumericCode(EMAIL_CODE_LENGTH);
            byte[] codeHash = SecurityUtil.Sha256(code);
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(CodeTtlMinutes);

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var exists = new SqlCommand(AuthSql.Text.EXISTS_ACCOUNT_BY_EMAIL, connection, transaction))
                    {
                        exists.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        var found = exists.ExecuteScalar();
                        if (found == null)
                        {
                            throw ThrowFault("EMAIL_NOT_FOUND", "No account is registered with that email.");
                        }
                    }

                    using (var last = new SqlCommand(AuthSql.Text.LAST_RESET_REQUEST, connection, transaction))
                    {
                        last.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        object lastObj = last.ExecuteScalar();
                        DateTime? lastUtc = (lastObj == null || lastObj == DBNull.Value) ? (DateTime?)null : (DateTime)lastObj;

                        bool isInCooldown = lastUtc.HasValue &&
                                            (DateTime.UtcNow - lastUtc.Value).TotalSeconds < ResendCooldownSeconds;
                        if (isInCooldown)
                        {
                            throw ThrowFault("TOO_SOON", "Please wait before requesting another code.");
                        }
                    }

                    // 3) Invalidar solicitudes anteriores
                    using (var invalidate = new SqlCommand(AuthSql.Text.INVALIDATE_PENDING_RESETS, connection, transaction))
                    {
                        invalidate.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        invalidate.ExecuteNonQuery();
                    }

                    // 4) Insertar nueva solicitud de reset
                    using (var insert = new SqlCommand(AuthSql.Text.INSERT_RESET_REQUEST, connection, transaction))
                    {
                        insert.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                        insert.Parameters.Add("@CodeHash", SqlDbType.VarBinary, 32).Value = codeHash;
                        insert.Parameters.Add("@ExpiresAtUtc", SqlDbType.DateTime2).Value = expiresAtUtc;
                        insert.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

            try
            {
                EmailSender.SendPasswordResetCode(email, code, CodeTtlMinutes);
            }
            catch (SmtpException ex)
            {
                throw ThrowTechnicalFault(
                    "SMTP_ERROR",
                    "Failed to send password reset email. Please try again later.",
                    "BeginPasswordReset.EmailSender",
                    ex);
            }

            return new BeginPasswordResetResponse
            {
                ExpiresAtUtc = expiresAtUtc,
                ResendAfterSeconds = ResendCooldownSeconds
            };
        }

        public void CompletePasswordReset(CompletePasswordResetRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_PAYLOAD_NULL);
            }

            string email = (request.Email ?? string.Empty).Trim();
            string code = request.Code ?? string.Empty;
            string newPassword = request.NewPassword ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(code) ||
                string.IsNullOrWhiteSpace(newPassword))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Email, code and new password are required.");
            }

            if (newPassword.Length < PASSWORD_MIN_LENGTH)
            {
                throw ThrowFault("WEAK_PASSWORD", "New password does not meet the minimum length requirements.");
            }

            int resetId;
            DateTime expiresAtUtc;
            bool used;

            byte[] codeHash = SecurityUtil.Sha256(code);

            using (var connection = new SqlConnection(GetConnectionString()))
            using (var pick = new SqlCommand(AuthSql.Text.PICK_LATEST_RESET, connection))
            {
                pick.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                connection.Open();

                using (var reader = pick.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        throw ThrowFault("CODE_MISSING", "No pending reset code. Request a new one.");
                    }

                    resetId = reader.GetInt32(0);
                    expiresAtUtc = reader.GetDateTime(1);
                    used = reader.GetBoolean(2);
                }
            }

            if (used || expiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault("CODE_EXPIRED", "Reset code expired. Request a new one.");
            }

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                using (var validate = new SqlCommand(AuthSql.Text.VALIDATE_RESET, connection))
                {
                    validate.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;
                    validate.Parameters.Add("@Hash", SqlDbType.VarBinary, 32).Value = codeHash;

                    bool ok = Convert.ToInt32(validate.ExecuteScalar()) == 1;
                    if (!ok)
                    {
                        using (var inc = new SqlCommand(AuthSql.Text.INCREMENT_RESET_ATTEMPTS, connection))
                        {
                            inc.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;
                            inc.ExecuteNonQuery();
                        }

                        throw ThrowFault("CODE_INVALID", "Invalid reset code.");
                    }
                }

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        string passwordHash = HashPasswordBc(newPassword);

                        using (var updatePwd = new SqlCommand(AuthSql.Text.UPDATE_ACCOUNT_PASSWORD, connection, transaction))
                        {
                            updatePwd.Parameters.Add(PARAMETER_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                            updatePwd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 128).Value = passwordHash;
                            int rows = updatePwd.ExecuteNonQuery();

                            if (rows <= 0)
                            {
                                throw ThrowFault("EMAIL_NOT_FOUND", "No account is registered with that email.");
                            }
                        }

                        using (var mark = new SqlCommand(AuthSql.Text.MARK_RESET_USED, connection, transaction))
                        {
                            mark.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;
                            mark.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (SqlException ex)
                    {
                        string userMessage =
                            $"Unexpected database error while completing password reset. " +
                            $"(Sql {ex.Number}: {ex.Message})";

                        throw ThrowTechnicalFault(
                            "DB_ERROR",
                            userMessage,
                            "CompletePasswordReset.Tx",
                            ex);
                    }
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

        private static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat(
                "Business fault. Code={0}. Message={1}.",
                code,
                message);

            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        private static FaultException<ServiceFault> ThrowTechnicalFault(
            string technicalCode,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            var fault = new ServiceFault
            {
                Code = technicalCode,
                Message = userMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
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
