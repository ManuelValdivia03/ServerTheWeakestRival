using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows
{
    public sealed class RegisterWorkflow
    {
        private readonly AuthRepository authRepository;
        private readonly PasswordPolicy passwordPolicy;
        private readonly PasswordService passwordService;

        public RegisterWorkflow(
            AuthRepository authRepository,
            PasswordPolicy passwordPolicy,
            PasswordService passwordService)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
            this.passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
            this.passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        }

        public RegisterResponse Execute(RegisterRequest request)
        {
            RegisterInput input = NormalizeOrThrow(request);

            passwordPolicy.ValidateOrThrow(input.Password);
            ProfileImagePolicy.ValidateOrThrow(request.ProfileImageBytes, request.ProfileImageContentType);

            bool accountExists = SqlExceptionFaultGuard.Execute(
                () => authRepository.ExistsAccountByEmail(input.Email),
                AuthServiceConstants.KEY_PREFIX_REGISTER,
                AuthServiceConstants.ERROR_DB_ERROR,
                AuthServiceConstants.CTX_REGISTER,
                AuthServiceContext.CreateSqlTechnicalFault);

            EmailExistencePolicy.EnsureNotExistsOrThrow(accountExists);

            int newAccountId = CreateAccountOrThrow(
                input,
                request.ProfileImageBytes,
                request.ProfileImageContentType);

            return BuildRegisterResponse(newAccountId);
        }

        private static RegisterInput NormalizeOrThrow(RegisterRequest request)
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

            if (string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(password))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    AuthServiceConstants.MESSAGE_REGISTER_REQUIRED_FIELDS);
            }

            return new RegisterInput(email, displayName, password);
        }

        private int CreateAccountOrThrow(
            RegisterInput input,
            byte[] profileImageBytes,
            string profileImageContentType)
        {
            string passwordHash = passwordService.Hash(input.Password);

            var data = new AccountRegistrationData(
                input.Email,
                passwordHash,
                input.DisplayName,
                new ProfileImagePayload(profileImageBytes, profileImageContentType));

            int newAccountId = SqlExceptionFaultGuard.Execute(
                () => authRepository.CreateAccountAndUser(data),
                AuthServiceConstants.KEY_PREFIX_REGISTER,
                AuthServiceConstants.ERROR_DB_ERROR,
                AuthServiceConstants.CTX_REGISTER,
                AuthServiceContext.CreateSqlTechnicalFault);

            if (newAccountId <= 0)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_DB_ERROR,
                    AuthServiceConstants.MESSAGE_ACCOUNT_NOT_CREATED);
            }

            return newAccountId;
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

        private sealed class RegisterInput
        {
            public string Email { get; }
            public string DisplayName { get; }
            public string Password { get; }

            public RegisterInput(string email, string displayName, string password)
            {
                Email = email ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                Password = password ?? string.Empty;
            }
        }
    }
}
