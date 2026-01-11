using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Validation;
using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows
{
    public sealed class CompletePasswordResetWorkflow
    {
        private const string OPERATION_KEY_PREFIX = AuthServiceConstants.KEY_PREFIX_COMPLETE_RESET;
        private const string DB_CONTEXT = AuthServiceConstants.CTX_COMPLETE_RESET;

        private readonly AuthRepository authRepository;
        private readonly PasswordPolicy passwordPolicy;
        private readonly PasswordService passwordService;

        public CompletePasswordResetWorkflow(
            AuthRepository authRepository,
            PasswordPolicy passwordPolicy,
            PasswordService passwordService)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
            this.passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
            this.passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        }

        public void Execute(CompletePasswordResetRequest request)
        {
            ResetPasswordInput input = NormalizeOrThrow(request);

            passwordPolicy.ValidateOrThrow(input.NewPassword);

            ResetRow reset = ReadLatestResetOrThrow(input.Email);

            AuthRequestValidator.EnsureCodeNotExpired(
                reset.ExpiresAtUtc,
                reset.Used,
                AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED);

            ValidateResetCodeOrThrow(reset.Id, input.Code);

            ApplyPasswordChangeOrThrow(input.Email, input.NewPassword);

            MarkResetUsedOrThrow(reset.Id);
        }

        private static ResetPasswordInput NormalizeOrThrow(CompletePasswordResetRequest request)
        {
            if (request == null)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    AuthServiceConstants.MESSAGE_PAYLOAD_NULL);
            }

            string email = (request.Email ?? string.Empty).Trim();
            string code = request.Code ?? string.Empty;
            string newPassword = request.NewPassword ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(code)
                || string.IsNullOrWhiteSpace(newPassword))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    AuthServiceConstants.MESSAGE_COMPLETE_RESET_REQUIRED_FIELDS);
            }

            return new ResetPasswordInput(email, code, newPassword);
        }

        private ResetRow ReadLatestResetOrThrow(string email)
        {
            ResetLookupResult result = SqlExceptionFaultGuard.Execute(
                () => authRepository.ReadLatestReset(email),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);

            if (!result.Found)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_CODE_MISSING,
                    AuthServiceConstants.MESSAGE_RESET_CODE_MISSING);
            }

            return result.Reset;
        }

        private void ValidateResetCodeOrThrow(int resetId, string code)
        {
            byte[] codeHash = SecurityUtil.Sha256(code);

            CodeValidationResult result = SqlExceptionFaultGuard.Execute(
                () => authRepository.ValidateResetCodeOrThrow(resetId, codeHash),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);

            if (!result.IsValid)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_CODE_INVALID,
                    AuthServiceConstants.MESSAGE_RESET_CODE_INVALID);
            }
        }

        private void ApplyPasswordChangeOrThrow(string email, string newPassword)
        {
            string passwordHash = PasswordService.Hash(newPassword);

            int rows = SqlExceptionFaultGuard.Execute(
                () => authRepository.UpdateAccountPassword(email, passwordHash),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);

            if (rows <= 0)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_EMAIL_NOT_FOUND,
                    AuthServiceConstants.MESSAGE_EMAIL_NOT_REGISTERED);
            }
        }

        private void MarkResetUsedOrThrow(int resetId)
        {
            SqlExceptionFaultGuard.Execute(
                () => authRepository.MarkResetUsed(resetId),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);
        }

        private sealed class ResetPasswordInput
        {
            public string Email { get; }
            public string Code { get; }
            public string NewPassword { get; }

            public ResetPasswordInput(string email, string code, string newPassword)
            {
                Email = email ?? string.Empty;
                Code = code ?? string.Empty;
                NewPassword = newPassword ?? string.Empty;
            }
        }
    }
}
