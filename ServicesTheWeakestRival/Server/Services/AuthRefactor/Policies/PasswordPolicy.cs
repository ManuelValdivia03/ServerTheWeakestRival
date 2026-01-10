using BCrypt.Net;
using ServicesTheWeakestRival.Server.Services.Auth;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies
{
    public sealed class PasswordPolicy
    {
        private readonly PasswordService passwordService;

        public PasswordPolicy(PasswordService passwordService)
        {
            this.passwordService = passwordService;
        }

        public void ValidateOrThrow(string password)
        {
            if (!passwordService.IsValid(password))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_WEAK_PASSWORD,
                    string.Format(
                        AuthServiceConstants.MESSAGE_PASSWORD_MIN_LENGTH_NOT_MET,
                        AuthServiceConstants.PASSWORD_MIN_LENGTH));
            }
        }

        public void VerifyOrThrow(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_CREDENTIALS,
                    AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS);
            }

            bool isValid;
            try
            {
                isValid = passwordService.Verify(password, storedHash);
            }
            catch (SaltParseException ex)
            {
                System.GC.KeepAlive(ex);

                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_CREDENTIALS,
                    AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS);
            }
            catch (System.ArgumentException ex)
            {
                System.GC.KeepAlive(ex);

                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_CREDENTIALS,
                    AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS);
            }

            if (!isValid)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_CREDENTIALS,
                    AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS);
            }
        }
    }
}
