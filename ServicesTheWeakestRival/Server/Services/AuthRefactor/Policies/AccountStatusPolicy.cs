namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies
{
    public static class AccountStatusPolicy
    {
        public static void EnsureAllowsLogin(byte status)
        {
            if (status == AuthServiceConstants.ACCOUNT_STATUS_INACTIVE)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_ACCOUNT_INACTIVE,
                    AuthServiceConstants.MESSAGE_ACCOUNT_NOT_ACTIVE);
            }

            if (status == AuthServiceConstants.ACCOUNT_STATUS_SUSPENDED)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_ACCOUNT_SUSPENDED,
                    AuthServiceConstants.MESSAGE_ACCOUNT_SUSPENDED);
            }

            if (status == AuthServiceConstants.ACCOUNT_STATUS_BANNED)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_ACCOUNT_BANNED,
                    AuthServiceConstants.MESSAGE_ACCOUNT_BANNED);
            }

            if (status != AuthServiceConstants.ACCOUNT_STATUS_ACTIVE)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_ACCOUNT_INACTIVE,
                    AuthServiceConstants.MESSAGE_ACCOUNT_NOT_ACTIVE);
            }
        }
    }
}
