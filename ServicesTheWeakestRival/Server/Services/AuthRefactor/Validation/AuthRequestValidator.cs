using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Validation
{
    public static class AuthRequestValidator
    {
        public static string NormalizeRequiredEmail(string email, string missingMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_REQUEST, missingMessage);
            }

            return email.Trim();
        }

        public static void EnsureCodeNotExpired(DateTime expiresAtUtc, bool used, string expiredMessage)
        {
            if (used || expiresAtUtc <= DateTime.UtcNow)
            {
                throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_CODE_EXPIRED, expiredMessage);
            }
        }

        public static void EnsureValidUserIdOrThrow(int userId)
        {
            if (userId <= 0)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    AuthServiceConstants.MESSAGE_USER_ID_REQUIRED);
            }
        }

        public static void EnsureValidSessionOrThrow(string tokenValue)
        {
            if (!AuthServiceContext.TryGetUserId(tokenValue, out int _))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_CREDENTIALS,
                    AuthServiceConstants.MESSAGE_INVALID_SESSION);
            }
        }
    }
}
