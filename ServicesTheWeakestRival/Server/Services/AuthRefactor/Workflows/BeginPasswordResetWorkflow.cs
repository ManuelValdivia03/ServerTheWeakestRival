using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Email;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Validation;
using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows
{
    public sealed class BeginPasswordResetWorkflow
    {
        private const string OPERATION_KEY_PREFIX = AuthServiceConstants.KEY_PREFIX_BEGIN_RESET;
        private const string DB_CONTEXT = AuthServiceConstants.CTX_BEGIN_RESET;

        private readonly AuthRepository authRepository;
        private readonly AuthEmailDispatcher emailDispatcher;

        public BeginPasswordResetWorkflow(AuthRepository authRepository, AuthEmailDispatcher emailDispatcher)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
            this.emailDispatcher = emailDispatcher ?? throw new ArgumentNullException(nameof(emailDispatcher));
        }

        public BeginPasswordResetResponse Execute(BeginPasswordResetRequest request)
        {
            string email = NormalizeEmail(request);
            EmailCodeInfo codeInfo = CreateCodeInfo();

            PersistResetRequestGuardedOrThrow(email, codeInfo);
            DispatchEmailOrThrow(email, codeInfo);

            return BuildResponse(codeInfo);
        }

        private static string NormalizeEmail(BeginPasswordResetRequest request)
        {
            return AuthRequestValidator.NormalizeRequiredEmail(
                request?.Email,
                AuthServiceConstants.MESSAGE_EMAIL_REQUIRED);
        }

        private static EmailCodeInfo CreateCodeInfo()
        {
            return EmailCodeFactory.Create();
        }

        private void PersistResetRequestGuardedOrThrow(string email, EmailCodeInfo codeInfo)
        {
            SqlExceptionFaultGuard.Execute(
                () => PersistResetRequestOrThrow(email, codeInfo),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);
        }

        private void PersistResetRequestOrThrow(string email, EmailCodeInfo codeInfo)
        {
            EnsureResetEligibilityOrThrow(email);
            authRepository.CreatePasswordResetRequest(email, codeInfo.Hash, codeInfo.ExpiresAtUtc);
        }

        private void EnsureResetEligibilityOrThrow(string email)
        {
            bool accountExists = authRepository.ExistsAccountByEmail(email);
            EmailExistencePolicy.EnsureExistsOrThrow(accountExists);

            LastRequestUtcResult lastUtc = authRepository.ReadLastResetUtc(email);
            ResendCooldownPolicy.EnsureNotTooSoonOrThrow(lastUtc, AuthServiceContext.ResendCooldownSeconds);
        }

        private void DispatchEmailOrThrow(string email, EmailCodeInfo codeInfo)
        {
            emailDispatcher.SendPasswordResetCodeOrThrow(email, codeInfo.Code);
        }

        private static BeginPasswordResetResponse BuildResponse(EmailCodeInfo codeInfo)
        {
            return new BeginPasswordResetResponse
            {
                ExpiresAtUtc = codeInfo.ExpiresAtUtc,
                ResendAfterSeconds = AuthServiceContext.ResendCooldownSeconds
            };
        }
    }
}
