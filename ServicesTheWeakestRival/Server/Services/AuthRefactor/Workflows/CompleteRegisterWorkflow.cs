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
    public sealed class CompleteRegisterWorkflow
    {
        private const string OPERATION_KEY_PREFIX = AuthServiceConstants.KEY_PREFIX_COMPLETE_REGISTER;
        private const string DB_CONTEXT = AuthServiceConstants.CTX_COMPLETE_REGISTER;

        private readonly AuthRepository authRepository;
        private readonly PasswordPolicy passwordPolicy;
        private readonly PasswordService passwordService;

        public CompleteRegisterWorkflow(
            AuthRepository authRepository,
            PasswordPolicy passwordPolicy,
            PasswordService passwordService)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
            this.passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
            this.passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        }

        public RegisterResponse Execute(CompleteRegisterRequest request)
        {
            CompleteRegisterInput input = NormalizeOrThrow(request);

            passwordPolicy.ValidateOrThrow(input.Password);
            ProfileImagePolicy.ValidateOrThrow(request.ProfileImageBytes, request.ProfileImageContentType);

            VerificationRow verification = ReadLatestVerificationOrThrow(input.Email);

            AuthRequestValidator.EnsureCodeNotExpired(
                verification.ExpiresAtUtc,
                verification.Used,
                AuthServiceConstants.MESSAGE_VERIFICATION_CODE_EXPIRED);

            ValidateVerificationCodeOrThrow(verification.Id, input.Code);

            EnsureEmailAvailableOrThrow(input.Email);

            int newAccountId = CreateAccountOrThrow(
                input,
                request.ProfileImageBytes,
                request.ProfileImageContentType);

            MarkVerificationUsedOrThrow(verification.Id);

            return BuildRegisterResponse(newAccountId);
        }

        private static CompleteRegisterInput NormalizeOrThrow(CompleteRegisterRequest request)
        {
            if (request == null)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    AuthServiceConstants.MESSAGE_PAYLOAD_NULL);
            }

            string email = (request.Email ?? string.Empty).Trim();
            string displayName = (request.DisplayName ?? string.Empty).Trim();
            string password = request.Password ?? string.Empty;
            string code = request.Code ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(password)
                || string.IsNullOrWhiteSpace(code))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    AuthServiceConstants.MESSAGE_COMPLETE_REGISTER_REQUIRED_FIELDS);
            }

            return new CompleteRegisterInput(email, displayName, password, code);
        }

        private VerificationRow ReadLatestVerificationOrThrow(string email)
        {
            VerificationLookupResult result = SqlExceptionFaultGuard.Execute(
                () => authRepository.ReadLatestVerification(email),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);

            if (!result.Found)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_CODE_MISSING,
                    AuthServiceConstants.MESSAGE_VERIFICATION_CODE_MISSING);
            }

            return result.Verification;
        }

        private void ValidateVerificationCodeOrThrow(int verificationId, string code)
        {
            byte[] codeHash = SecurityUtil.Sha256(code);

            CodeValidationResult result = SqlExceptionFaultGuard.Execute(
                () => authRepository.ValidateVerificationCodeOrThrow(verificationId, codeHash),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);

            if (!result.IsValid)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_CODE_INVALID,
                    AuthServiceConstants.MESSAGE_VERIFICATION_CODE_INVALID);
            }
        }

        private void EnsureEmailAvailableOrThrow(string email)
        {
            bool exists = SqlExceptionFaultGuard.Execute(
                () => authRepository.ExistsAccountByEmail(email),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);

            EmailExistencePolicy.EnsureNotExistsOrThrow(exists);
        }

        private int CreateAccountOrThrow(
            CompleteRegisterInput input,
            byte[] profileImageBytes,
            string profileImageContentType)
        {
            string passwordHash = PasswordService.Hash(input.Password);

            var data = new AccountRegistrationData(
                input.Email,
                passwordHash,
                input.DisplayName,
                new ProfileImagePayload(profileImageBytes, profileImageContentType));

            int newAccountId = SqlExceptionFaultGuard.Execute(
                () => authRepository.CreateAccountAndUser(data),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);

            if (newAccountId <= 0)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_DB_ERROR,
                    AuthServiceConstants.MESSAGE_ACCOUNT_NOT_CREATED);
            }

            return newAccountId;
        }

        private void MarkVerificationUsedOrThrow(int verificationId)
        {
            SqlExceptionFaultGuard.Execute(
                () => authRepository.MarkVerificationUsed(verificationId),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);
        }

        private static RegisterResponse BuildRegisterResponse(int userId)
        {
            return new RegisterResponse
            {
                UserId = userId,
                Token = BuildEmptyToken(userId)
            };
        }

        private static AuthToken BuildEmptyToken(int userId)
        {
            return new AuthToken
            {
                UserId = userId,
                Token = string.Empty,
                ExpiresAtUtc = DateTime.MinValue
            };
        }

        private sealed class CompleteRegisterInput
        {
            public string Email { get; }
            public string DisplayName { get; }
            public string Password { get; }
            public string Code { get; }

            public CompleteRegisterInput(string email, string displayName, string password, string code)
            {
                Email = email ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                Password = password ?? string.Empty;
                Code = code ?? string.Empty;
            }
        }
    }
}
