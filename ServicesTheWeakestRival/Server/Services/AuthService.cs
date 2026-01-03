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
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.Logic;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class AuthService : IAuthService
    {
        public const int DEFAULT_CODE_TTL_MINUTES = 10;
        public const int DEFAULT_RESEND_COOLDOWN_SECONDS = 60;
        public const int TOKEN_TTL_HOURS = 24;

        public const byte ACCOUNT_STATUS_ACTIVE = 1;
        public const byte ACCOUNT_STATUS_BLOCKED = 0;

        public const int EMAIL_MAX_LENGTH = 320;
        public const int DISPLAY_NAME_MAX_LENGTH = 80;
        public const int PROFILE_URL_MAX_LENGTH = 500;
        public const int EMAIL_CODE_LENGTH = 6;
        public const int PASSWORD_MIN_LENGTH = 8;

        public const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        public const string ERROR_CONFIG = "CONFIG_ERROR";
        public const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        public const string ERROR_EMAIL_TAKEN = "EMAIL_TAKEN";
        public const string ERROR_TOO_SOON = "TOO_SOON";
        public const string ERROR_CODE_MISSING = "CODE_MISSING";
        public const string ERROR_CODE_EXPIRED = "CODE_EXPIRED";
        public const string ERROR_CODE_INVALID = "CODE_INVALID";
        public const string ERROR_DB_ERROR = "DB_ERROR";
        public const string ERROR_INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
        public const string ERROR_ACCOUNT_BLOCKED = "ACCOUNT_BLOCKED";
        public const string ERROR_PAYLOAD_NULL = "Request payload is null.";
        public const string ERROR_SMTP = "SMTP_ERROR";
        public const string ERROR_EMAIL_NOT_FOUND = "EMAIL_NOT_FOUND";
        public const string ERROR_WEAK_PASSWORD = "WEAK_PASSWORD";

        public const string PARAMETER_EMAIL = "@Email";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(AuthService));

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private readonly PasswordService _passwordService;
        private readonly IEmailService _emailService;
        private readonly AuthDataAccess _authDataAccess = new AuthDataAccess();

        private static readonly int CodeTtlMinutes =
            ParseIntAppSetting("EmailCodeTtlMinutes", DEFAULT_CODE_TTL_MINUTES);

        public static readonly int ResendCooldownSeconds =
            ParseIntAppSetting("EmailResendCooldownSeconds", DEFAULT_RESEND_COOLDOWN_SECONDS);

        public AuthService()
            : this(new PasswordService(PASSWORD_MIN_LENGTH), new SmtpEmailService())
        {
        }

        public AuthService(PasswordService passwordService, IEmailService emailService)
        {
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        public static string GetConnectionString()
        {
            var connection = ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (connection == null || string.IsNullOrWhiteSpace(connection.ConnectionString))
            {
                var configurationException = new ConfigurationErrorsException(
                    string.Format("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME));

                throw ThrowTechnicalFault(
                    ERROR_CONFIG,
                    "Configuration error. Please contact support.",
                    "AuthService.GetConnectionString",
                    configurationException);
            }

            return connection.ConnectionString;
        }

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
            string email = NormalizeRequiredEmail(request?.Email, "Email is required.");

            string code = SecurityUtil.CreateNumericCode(EMAIL_CODE_LENGTH);
            byte[] codeHash = SecurityUtil.Sha256(code);
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(CodeTtlMinutes);

            _authDataAccess.CreateRegisterVerification(email, codeHash, expiresAtUtc);

            try
            {
                _emailService.SendVerificationCode(email, code, CodeTtlMinutes);
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

            ValidatePasswordOrThrow(password);

            byte[] codeHash = SecurityUtil.Sha256(code);

            int verificationId;
            DateTime expiresAtUtc;
            bool used;

            _authDataAccess.ReadLatestVerification(email, out verificationId, out expiresAtUtc, out used);
            EnsureCodeNotExpired(expiresAtUtc, used, "Verification code expired. Request a new one.");

            int newAccountId;

            try
            {
                _authDataAccess.ValidateVerificationCodeOrThrow(verificationId, codeHash);

                string passwordHash = _passwordService.Hash(password);

                _authDataAccess.EnsureAccountDoesNotExist(email);
                newAccountId = _authDataAccess.CreateAccountAndUser(
                    email,
                    passwordHash,
                    displayName,
                    request.ProfileImageUrl);

                _authDataAccess.MarkVerificationUsed(verificationId);
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB_ERROR,
                    "Unexpected database error while completing registration.",
                    "CompleteRegister.Tx",
                    ex);
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

            ValidatePasswordOrThrow(request.Password);

            string passwordHash = _passwordService.Hash(request.Password);
            int newAccountId;

            try
            {
                _authDataAccess.EnsureAccountDoesNotExist(request.Email);
                newAccountId = _authDataAccess.CreateAccountAndUser(
                    request.Email,
                    passwordHash,
                    request.DisplayName,
                    request.ProfileImageUrl);
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB_ERROR,
                    "Unexpected database error while registering.",
                    "Register.Tx",
                    ex);
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
                _authDataAccess.GetAccountForLogin(request.Email, out userId, out storedHash, out status);
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

            if (!_passwordService.Verify(request.Password, storedHash))
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
                    _authDataAccess.LeaveAllLobbiesForUser(removed.UserId);
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
            string email = NormalizeRequiredEmail(request?.Email, "Email is required.");
            string code = SecurityUtil.CreateNumericCode(EMAIL_CODE_LENGTH);
            byte[] codeHash = SecurityUtil.Sha256(code);
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(CodeTtlMinutes);

            _authDataAccess.CreatePasswordResetRequest(email, codeHash, expiresAtUtc);

            try
            {
                _emailService.SendPasswordResetCode(email, code, CodeTtlMinutes);
            }
            catch (SmtpException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_SMTP,
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

            ValidatePasswordOrThrow(newPassword);

            byte[] codeHash = SecurityUtil.Sha256(code);

            int resetId;
            DateTime expiresAtUtc;
            bool used;

            _authDataAccess.ReadLatestReset(email, out resetId, out expiresAtUtc, out used);
            EnsureCodeNotExpired(expiresAtUtc, used, "Reset code expired. Request a new one.");

            try
            {
                _authDataAccess.ValidateResetCodeOrThrow(resetId, codeHash);

                string passwordHash = _passwordService.Hash(newPassword);

                int rows = _authDataAccess.UpdateAccountPassword(email, passwordHash);

                if (rows <= 0)
                {
                    throw ThrowFault(ERROR_EMAIL_NOT_FOUND, "No account is registered with that email.");
                }

                _authDataAccess.MarkResetUsed(resetId);
            }
            catch (SqlException ex)
            {
                string userMessage =
                    $"Unexpected database error while completing password reset. (Sql {ex.Number}: {ex.Message})";

                throw ThrowTechnicalFault(
                    ERROR_DB_ERROR,
                    userMessage,
                    "CompletePasswordReset.Tx",
                    ex);
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

        private void ValidatePasswordOrThrow(string password)
        {
            if (!_passwordService.IsValid(password))
            {
                throw ThrowFault(
                    ERROR_WEAK_PASSWORD,
                    $"Password does not meet the minimum length requirements ({PASSWORD_MIN_LENGTH} characters).");
            }
        }

        public static FaultException<ServiceFault> ThrowFault(string code, string message)
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

        public static FaultException<ServiceFault> ThrowTechnicalFault(
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

        private static string NormalizeRequiredEmail(string email, string missingMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, missingMessage);
            }

            return email.Trim();
        }

        private static void EnsureCodeNotExpired(DateTime expiresAtUtc, bool used, string expiredMessage)
        {
            if (used || expiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault(ERROR_CODE_EXPIRED, expiredMessage);
            }
        }
    }
}
