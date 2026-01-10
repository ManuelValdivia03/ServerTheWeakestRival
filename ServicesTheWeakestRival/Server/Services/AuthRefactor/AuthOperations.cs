// AuthOperations.cs
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Auth;
using System;
using System.Data.SqlClient;
using System.Globalization;
using System.Net.Mail;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public sealed class AuthOperations
    {
        private const char PROFILE_IMAGE_CODE_SEPARATOR = '|';

        private readonly AuthRepository authRepository;
        private readonly PasswordService passwordService;
        private readonly IEmailService emailService;

        public AuthOperations(AuthRepository authRepository, PasswordService passwordService, IEmailService emailService)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
            this.passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
            this.emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
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

            string code = SecurityUtil.CreateNumericCode(AuthServiceConstants.EMAIL_CODE_LENGTH);
            byte[] codeHash = SecurityUtil.Sha256(code);
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceContext.CodeTtlMinutes);

            authRepository.CreateRegisterVerification(email, codeHash, expiresAtUtc);

            try
            {
                emailService.SendVerificationCode(email, code, AuthServiceContext.CodeTtlMinutes);
            }
            catch (SmtpException ex)
            {
                throw AuthServiceContext.ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_SMTP,
                    "Failed to send verification email. Please try again later.",
                    AuthServiceConstants.CTX_BEGIN_REGISTER,
                    ex);
            }

            return new BeginRegisterResponse
            {
                ExpiresAtUtc = expiresAtUtc,
                ResendAfterSeconds = AuthServiceContext.ResendCooldownSeconds
            };
        }

        public RegisterResponse CompleteRegister(CompleteRegisterRequest request)
        {
            if (request == null)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_REQUEST, AuthServiceConstants.MESSAGE_PAYLOAD_NULL);
            }

            string email = (request.Email ?? string.Empty).Trim();
            string displayName = (request.DisplayName ?? string.Empty).Trim();
            string password = request.Password ?? string.Empty;
            string code = request.Code ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(password)
                || string.IsNullOrWhiteSpace(code))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    "Email, display name, password and code are required.");
            }

            ValidatePasswordOrThrow(password);

            int maxBytes = ReadProfileImageMaxBytes();
            ProfileImageValidator.ValidateOrThrow(request.ProfileImageBytes, request.ProfileImageContentType, maxBytes);

            byte[] codeHash = SecurityUtil.Sha256(code);

            authRepository.ReadLatestVerification(email, out int verificationId, out DateTime expiresAtUtc, out bool used);
            EnsureCodeNotExpired(expiresAtUtc, used, "Verification code expired. Request a new one.");

            int newAccountId;

            try
            {
                authRepository.ValidateVerificationCodeOrThrow(verificationId, codeHash);

                string passwordHash = passwordService.Hash(password);

                authRepository.EnsureAccountDoesNotExist(email);
                newAccountId = authRepository.CreateAccountAndUser(
                    email,
                    passwordHash,
                    displayName,
                    request.ProfileImageBytes,
                    request.ProfileImageContentType);

                authRepository.MarkVerificationUsed(verificationId);
            }
            catch (SqlException ex)
            {
                throw AuthServiceContext.ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_DB_ERROR,
                    "Unexpected database error while completing registration.",
                    AuthServiceConstants.CTX_COMPLETE_REGISTER,
                    ex);
            }

            if (newAccountId <= 0)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_DB_ERROR, "Account was not created.");
            }

            var token = AuthServiceContext.IssueToken(newAccountId);
            return new RegisterResponse { UserId = newAccountId, Token = token };
        }

        public RegisterResponse Register(RegisterRequest request)
        {
            if (request == null)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_REQUEST, AuthServiceConstants.MESSAGE_PAYLOAD_NULL);
            }

            ValidatePasswordOrThrow(request.Password);

            int maxBytes = ReadProfileImageMaxBytes();
            ProfileImageValidator.ValidateOrThrow(request.ProfileImageBytes, request.ProfileImageContentType, maxBytes);

            string passwordHash = passwordService.Hash(request.Password);
            int newAccountId;

            try
            {
                authRepository.EnsureAccountDoesNotExist(request.Email);
                newAccountId = authRepository.CreateAccountAndUser(
                    request.Email,
                    passwordHash,
                    request.DisplayName,
                    request.ProfileImageBytes,
                    request.ProfileImageContentType);
            }
            catch (SqlException ex)
            {
                throw AuthServiceContext.ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_DB_ERROR,
                    "Unexpected database error while registering.",
                    AuthServiceConstants.CTX_REGISTER,
                    ex);
            }

            var token = AuthServiceContext.IssueToken(newAccountId);
            return new RegisterResponse { UserId = newAccountId, Token = token };
        }

        public GetProfileImageResponse GetProfileImage(GetProfileImageRequest request)
        {
            if (request == null)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_REQUEST, AuthServiceConstants.MESSAGE_PAYLOAD_NULL);
            }

            if (!AuthServiceContext.TryGetUserId(request.Token, out int _))
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, "Invalid session.");
            }

            if (request.AccountId <= 0)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_REQUEST, "AccountId is required.");
            }

            try
            {
                ProfileImageRecord record = authRepository.ReadUserProfileImage(request.AccountId);

                byte[] imageBytes = record?.ImageBytes ?? Array.Empty<byte>();
                string contentType = record?.ContentType ?? string.Empty;

                bool hasImage = imageBytes.Length > 0;
                DateTime? updatedAtUtc = record?.UpdatedAtUtc;

                string profileImageCode = BuildProfileImageCode(updatedAtUtc, imageBytes, contentType);

                bool clientHasSameImage =
                    !string.IsNullOrWhiteSpace(request.ProfileImageCode)
                    && string.Equals(request.ProfileImageCode, profileImageCode, StringComparison.Ordinal);

                byte[] responseBytes = clientHasSameImage ? Array.Empty<byte>() : imageBytes;

                string responseContentType =
                    !hasImage || clientHasSameImage
                        ? string.Empty
                        : contentType;

                return new GetProfileImageResponse
                {
                    ImageBytes = responseBytes,
                    ContentType = responseContentType,
                    UpdatedAtUtc = updatedAtUtc,
                    ProfileImageCode = profileImageCode
                };
            }
            catch (SqlException ex)
            {
                throw AuthServiceContext.ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_DB_ERROR,
                    "Unexpected database error while reading profile image.",
                    "AuthOperations.GetProfileImage",
                    ex);
            }
        }

        public LoginResponse Login(LoginRequest request)
        {
            if (request == null)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_REQUEST, AuthServiceConstants.MESSAGE_PAYLOAD_NULL);
            }

            int userId;
            string storedHash;
            byte status;

            try
            {
                authRepository.GetAccountForLogin(request.Email, out userId, out storedHash, out status);
            }
            catch (SqlException ex)
            {
                throw AuthServiceContext.ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_DB_ERROR,
                    "Unexpected database error while logging in.",
                    AuthServiceConstants.CTX_LOGIN,
                    ex);
            }

            if (!passwordService.Verify(request.Password, storedHash))
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, "Email or password is incorrect.");
            }

            ThrowIfBlockedStatus(status);

            var token = AuthServiceContext.IssueToken(userId);
            return new LoginResponse { Token = token };
        }

        public void Logout(LogoutRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token))
            {
                return;
            }

            if (AuthServiceContext.TryRemoveToken(request.Token, out var removed) && removed != null)
            {
                try
                {
                    authRepository.LeaveAllLobbiesForUser(removed.UserId);
                }
                catch (SqlException ex)
                {
                    throw AuthServiceContext.ThrowTechnicalFault(
                        AuthServiceConstants.ERROR_DB_ERROR,
                        "Unexpected database error while logging out.",
                        AuthServiceConstants.CTX_LOGOUT_LEAVE_ALL,
                        ex);
                }
            }
        }

        public BeginPasswordResetResponse BeginPasswordReset(BeginPasswordResetRequest request)
        {
            string email = NormalizeRequiredEmail(request?.Email, "Email is required.");

            string code = SecurityUtil.CreateNumericCode(AuthServiceConstants.EMAIL_CODE_LENGTH);
            byte[] codeHash = SecurityUtil.Sha256(code);
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceContext.CodeTtlMinutes);

            authRepository.CreatePasswordResetRequest(email, codeHash, expiresAtUtc);

            try
            {
                emailService.SendPasswordResetCode(email, code, AuthServiceContext.CodeTtlMinutes);
            }
            catch (SmtpException ex)
            {
                throw AuthServiceContext.ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_SMTP,
                    "Failed to send password reset email. Please try again later.",
                    AuthServiceConstants.CTX_BEGIN_RESET,
                    ex);
            }

            return new BeginPasswordResetResponse
            {
                ExpiresAtUtc = expiresAtUtc,
                ResendAfterSeconds = AuthServiceContext.ResendCooldownSeconds
            };
        }

        public void CompletePasswordReset(CompletePasswordResetRequest request)
        {
            if (request == null)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_REQUEST, AuthServiceConstants.MESSAGE_PAYLOAD_NULL);
            }

            string email = (request.Email ?? string.Empty).Trim();
            string code = request.Code ?? string.Empty;
            string newPassword = request.NewPassword ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(code)
                || string.IsNullOrWhiteSpace(newPassword))
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_REQUEST, "Email, code and new password are required.");
            }

            ValidatePasswordOrThrow(newPassword);

            byte[] codeHash = SecurityUtil.Sha256(code);

            authRepository.ReadLatestReset(email, out int resetId, out DateTime expiresAtUtc, out bool used);
            EnsureCodeNotExpired(expiresAtUtc, used, "Reset code expired. Request a new one.");

            try
            {
                authRepository.ValidateResetCodeOrThrow(resetId, codeHash);

                string passwordHash = passwordService.Hash(newPassword);

                int rows = authRepository.UpdateAccountPassword(email, passwordHash);

                if (rows <= 0)
                {
                    throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_EMAIL_NOT_FOUND, "No account is registered with that email.");
                }

                authRepository.MarkResetUsed(resetId);
            }
            catch (SqlException ex)
            {
                string userMessage =
                    string.Format("Unexpected database error while completing password reset. (Sql {0}: {1})", ex.Number, ex.Message);

                throw AuthServiceContext.ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_DB_ERROR,
                    userMessage,
                    AuthServiceConstants.CTX_COMPLETE_RESET,
                    ex);
            }
        }

        private static string BuildProfileImageCode(DateTime? updatedAtUtc, byte[] imageBytes, string contentType)
        {
            if (!updatedAtUtc.HasValue)
            {
                return string.Empty;
            }

            int byteCount = imageBytes?.Length ?? 0;
            if (byteCount <= 0)
            {
                return string.Empty;
            }

            string safeContentType = contentType ?? string.Empty;

            return string.Concat(
                updatedAtUtc.Value.Ticks.ToString(CultureInfo.InvariantCulture),
                PROFILE_IMAGE_CODE_SEPARATOR,
                byteCount.ToString(CultureInfo.InvariantCulture),
                PROFILE_IMAGE_CODE_SEPARATOR,
                safeContentType);
        }

        private void ValidatePasswordOrThrow(string password)
        {
            if (!passwordService.IsValid(password))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_WEAK_PASSWORD,
                    string.Format(
                        "Password does not meet the minimum length requirements ({0} characters).",
                        AuthServiceConstants.PASSWORD_MIN_LENGTH));
            }
        }

        private static string NormalizeRequiredEmail(string email, string missingMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_REQUEST, missingMessage);
            }

            return email.Trim();
        }

        private static void EnsureCodeNotExpired(DateTime expiresAtUtc, bool used, string expiredMessage)
        {
            if (used || expiresAtUtc <= DateTime.UtcNow)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_CODE_EXPIRED, expiredMessage);
            }
        }

        private static int ReadProfileImageMaxBytes()
        {
            return ProfileImageConstants.DEFAULT_MAX_IMAGE_BYTES;
        }

        private static void ThrowIfBlockedStatus(byte status)
        {
            if (status == AuthServiceConstants.ACCOUNT_STATUS_INACTIVE)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_ACCOUNT_INACTIVE, "Account is not active.");
            }

            if (status == AuthServiceConstants.ACCOUNT_STATUS_SUSPENDED)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_ACCOUNT_SUSPENDED, "Account is suspended.");
            }

            if (status == AuthServiceConstants.ACCOUNT_STATUS_BANNED)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_ACCOUNT_BANNED, "Account is banned.");
            }

            if (status != AuthServiceConstants.ACCOUNT_STATUS_ACTIVE)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_ACCOUNT_INACTIVE, "Account is not active.");
            }
        }
    }
}
