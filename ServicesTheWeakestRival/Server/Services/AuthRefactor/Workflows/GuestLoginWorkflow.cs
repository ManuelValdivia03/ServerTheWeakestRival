using BCrypt.Net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Infrastructure.Randomization;
using System;
using System.Globalization;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows
{
    public sealed class GuestLoginWorkflow
    {
        private const string OPERATION_KEY_PREFIX = "Auth.GuestLogin";
        private const string DB_CONTEXT = "AuthService.GuestLogin";

        private const string EMPTY_STRING = "";

        private const string GUEST_EMAIL_PREFIX = "guest-";
        private const string GUEST_EMAIL_DOMAIN = "@guest.local";

        private const string GUEST_DISPLAY_NAME_PREFIX = "Guest ";
        private const int GUEST_SUFFIX_MIN_INCLUSIVE = 1000;
        private const int GUEST_SUFFIX_MAX_EXCLUSIVE = 10000;

        private const string GUID_FORMAT_NO_HYPHENS = "N";

        private const int PROFILE_IMAGE_BYTES_LENGTH = 0;

        private readonly AuthRepository authRepository;

        public GuestLoginWorkflow(AuthRepository authRepository)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
        }

        public LoginResponse Execute(GuestLoginRequest request)
        {
            string displayName = NormalizeDisplayName(request?.DisplayName);

            string email = CreateGuestEmail();
            string passwordHash = CreateGuestPasswordHash();

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                displayName,
                new ProfileImagePayload(new byte[PROFILE_IMAGE_BYTES_LENGTH], EMPTY_STRING));

            int userId = SqlExceptionFaultGuard.Execute(
                () => authRepository.CreateAccountAndUser(data),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);

            AuthToken token = AuthServiceContext.IssueToken(userId);

            return new LoginResponse
            {
                Token = token
            };
        }

        private static string NormalizeDisplayName(string raw)
        {
            string trimmed = (raw ?? EMPTY_STRING).Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                int suffix = CryptoRandomInt32.GetInt32(GUEST_SUFFIX_MIN_INCLUSIVE, GUEST_SUFFIX_MAX_EXCLUSIVE);
                return GUEST_DISPLAY_NAME_PREFIX + suffix.ToString(CultureInfo.InvariantCulture);
            }

            if (trimmed.Length > AuthServiceConstants.DISPLAY_NAME_MAX_LENGTH)
            {
                return trimmed.Substring(0, AuthServiceConstants.DISPLAY_NAME_MAX_LENGTH);
            }

            return trimmed;
        }

        private static string CreateGuestEmail()
        {
            return GUEST_EMAIL_PREFIX
                + Guid.NewGuid().ToString(GUID_FORMAT_NO_HYPHENS)
                + GUEST_EMAIL_DOMAIN;
        }

        private static string CreateGuestPasswordHash()
        {
            string rawPassword = Guid.NewGuid().ToString(GUID_FORMAT_NO_HYPHENS);
            return BCrypt.Net.BCrypt.HashPassword(rawPassword);
        }
    }
}
