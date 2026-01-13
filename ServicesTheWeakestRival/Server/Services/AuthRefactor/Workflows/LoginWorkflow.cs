using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows
{
    public sealed class LoginWorkflow
    {
        private const string OPERATION_KEY_PREFIX = AuthServiceConstants.KEY_PREFIX_LOGIN;
        private const string DB_CONTEXT = AuthServiceConstants.CTX_LOGIN;

        private readonly AuthRepository authRepository;
        private readonly PasswordPolicy passwordPolicy;

        public LoginWorkflow(AuthRepository authRepository, PasswordPolicy passwordPolicy)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
            this.passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
        }

        public LoginResponse Execute(LoginRequest request)
        {
            LoginInput input = NormalizeOrThrow(request);

            LoginAccountRow account = ReadLoginAccountOrThrow(input.Email);

            PasswordPolicy.VerifyOrThrow(input.Password, account.PasswordHash);

            AccountStatusPolicy.EnsureAllowsLogin(account.Status);

            AuthToken token = AuthServiceContext.IssueToken(account.UserId);

            return new LoginResponse
            {
                Token = token
            };
        }

        private static LoginInput NormalizeOrThrow(LoginRequest request)
        {
            if (request == null)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    AuthServiceConstants.MESSAGE_PAYLOAD_NULL);
            }

            string email = (request.Email ?? string.Empty).Trim();
            string password = request.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_CREDENTIALS,
                    AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS);
            }

            return new LoginInput(email, password);
        }

        private LoginAccountRow ReadLoginAccountOrThrow(string email)
        {
            LoginLookupResult result = SqlExceptionFaultGuard.Execute(
                () => authRepository.GetAccountForLogin(email),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);

            if (!result.Found)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_CREDENTIALS,
                    AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS);
            }

            return result.Account;
        }

        private sealed class LoginInput
        {
            public string Email 
            { 
                get;
            }
            public string Password 
            { 
                get;
            }

            public LoginInput(string email, string password)
            {
                Email = email ?? string.Empty;
                Password = password ?? string.Empty;
            }
        }
    }
}
