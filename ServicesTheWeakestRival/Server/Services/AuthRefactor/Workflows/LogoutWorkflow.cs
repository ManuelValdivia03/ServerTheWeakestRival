using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows
{
    public sealed class LogoutWorkflow
    {
        private readonly AuthRepository authRepository;

        public LogoutWorkflow(AuthRepository authRepository)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
        }

        public void Execute(LogoutRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token))
            {
                return;
            }

            if (!AuthServiceContext.TryRemoveToken(request.Token, out AuthToken removedToken))
            {
                return;
            }

            LeaveAllLobbiesForUserOrThrow(removedToken.UserId);
        }

        private void LeaveAllLobbiesForUserOrThrow(int userId)
        {
            SqlExceptionFaultGuard.Execute(
                () => authRepository.LeaveAllLobbiesForUser(userId),
                AuthServiceConstants.KEY_PREFIX_LOGOUT,
                AuthServiceConstants.ERROR_DB_ERROR,
                AuthServiceConstants.CTX_LOGOUT_LEAVE_ALL,
                AuthServiceContext.CreateSqlTechnicalFault);
        }
    }
}
