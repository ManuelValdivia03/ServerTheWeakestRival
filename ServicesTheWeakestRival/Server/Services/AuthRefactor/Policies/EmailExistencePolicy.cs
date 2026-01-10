namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies
{
    public static class EmailExistencePolicy
    {
        public static void EnsureNotExistsOrThrow(bool exists)
        {
            if (exists)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_EMAIL_TAKEN,
                    AuthServiceConstants.MESSAGE_EMAIL_TAKEN);
            }
        }

        public static void EnsureExistsOrThrow(bool exists)
        {
            if (!exists)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_EMAIL_NOT_FOUND,
                    AuthServiceConstants.MESSAGE_EMAIL_NOT_REGISTERED);
            }
        }
    }
}
