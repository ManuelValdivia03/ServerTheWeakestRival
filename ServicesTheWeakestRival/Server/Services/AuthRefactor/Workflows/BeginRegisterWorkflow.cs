using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Email;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Validation;
using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows
{
    public sealed class BeginRegisterWorkflow
    {
        private const string OPERATION_KEY_PREFIX = AuthServiceConstants.KEY_PREFIX_BEGIN_REGISTER;
        private const string DB_CONTEXT = AuthServiceConstants.CTX_BEGIN_REGISTER;

        private readonly AuthRepository authRepository;
        private readonly AuthEmailDispatcher emailDispatcher;

        public BeginRegisterWorkflow(AuthRepository authRepository, AuthEmailDispatcher emailDispatcher)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
            this.emailDispatcher = emailDispatcher ?? throw new ArgumentNullException(nameof(emailDispatcher));
        }

        public BeginRegisterResponse Execute(BeginRegisterRequest request)
        {
            string email = NormalizeEmail(request);
            EmailCodeInfo codeInfo = CreateCodeInfo();

            PersistVerificationGuardedOrThrow(email, codeInfo);
            DispatchEmailOrThrow(email, codeInfo);

            return BuildResponse(codeInfo);
        }

        private static string NormalizeEmail(BeginRegisterRequest request)
        {
            return AuthRequestValidator.NormalizeRequiredEmail(
                request?.Email,
                AuthServiceConstants.MESSAGE_EMAIL_REQUIRED);
        }

        private static EmailCodeInfo CreateCodeInfo()
        {
            return EmailCodeFactory.Create();
        }

        private void PersistVerificationGuardedOrThrow(string email, EmailCodeInfo codeInfo)
        {
            SqlExceptionFaultGuard.Execute(
                () => PersistVerificationOrThrow(email, codeInfo),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);
        }

        private void PersistVerificationOrThrow(string email, EmailCodeInfo codeInfo)
        {
            EnsureRegisterEligibilityOrThrow(email);
            authRepository.CreateRegisterVerification(email, codeInfo.Hash, codeInfo.ExpiresAtUtc);
        }

        private void EnsureRegisterEligibilityOrThrow(string email)
        {
            bool accountExists = authRepository.ExistsAccountByEmail(email);
            EmailExistencePolicy.EnsureNotExistsOrThrow(accountExists);

            LastRequestUtcResult lastUtc = authRepository.ReadLastVerificationUtc(email);
            ResendCooldownPolicy.EnsureNotTooSoonOrThrow(lastUtc, AuthServiceContext.ResendCooldownSeconds);
        }

        private void DispatchEmailOrThrow(string email, EmailCodeInfo codeInfo)
        {
            emailDispatcher.SendVerificationCodeOrThrow(email, codeInfo.Code);
        }

        private static BeginRegisterResponse BuildResponse(EmailCodeInfo codeInfo)
        {
            return new BeginRegisterResponse
            {
                ExpiresAtUtc = codeInfo.ExpiresAtUtc,
                ResendAfterSeconds = AuthServiceContext.ResendCooldownSeconds
            };
        }
    }
}
