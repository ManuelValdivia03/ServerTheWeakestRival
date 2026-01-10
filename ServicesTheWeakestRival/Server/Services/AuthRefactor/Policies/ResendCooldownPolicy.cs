using System;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies
{
    public static class ResendCooldownPolicy
    {
        public static void EnsureNotTooSoonOrThrow(LastRequestUtcResult lastRequest, int cooldownSeconds)
        {
            if (lastRequest == null || !lastRequest.HasValue)
            {
                return;
            }

            double seconds = (DateTime.UtcNow - lastRequest.Utc).TotalSeconds;
            if (seconds < cooldownSeconds)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_TOO_SOON,
                    AuthServiceConstants.MESSAGE_TOO_SOON);
            }
        }
    }
}
