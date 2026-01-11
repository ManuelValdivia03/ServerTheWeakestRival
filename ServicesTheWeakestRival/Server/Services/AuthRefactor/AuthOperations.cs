using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Email;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public sealed class AuthOperations
    {
        private readonly BeginRegisterWorkflow beginRegisterWorkflow;
        private readonly BeginPasswordResetWorkflow beginPasswordResetWorkflow;
        private readonly CompleteRegisterWorkflow completeRegisterWorkflow;
        private readonly RegisterWorkflow registerWorkflow;
        private readonly CompletePasswordResetWorkflow completePasswordResetWorkflow;
        private readonly LoginWorkflow loginWorkflow;
        private readonly LogoutWorkflow logoutWorkflow;
        private readonly GetProfileImageWorkflow getProfileImageWorkflow;
        private readonly GuestLoginWorkflow guestLoginWorkflow;

        public AuthOperations(
            AuthRepository authRepository,
            PasswordService passwordService,
            IEmailService emailService)
        {
            if (authRepository == null) throw new ArgumentNullException(nameof(authRepository));
            if (passwordService == null) throw new ArgumentNullException(nameof(passwordService));
            if (emailService == null) throw new ArgumentNullException(nameof(emailService));

            var emailDispatcher = new AuthEmailDispatcher(emailService);
            var passwordPolicy = new PasswordPolicy(passwordService);

            beginRegisterWorkflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);
            beginPasswordResetWorkflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);
            completeRegisterWorkflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);
            registerWorkflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);
            completePasswordResetWorkflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);
            loginWorkflow = new LoginWorkflow(authRepository, passwordPolicy);
            logoutWorkflow = new LogoutWorkflow(authRepository);
            getProfileImageWorkflow = new GetProfileImageWorkflow(authRepository);
            guestLoginWorkflow = new GuestLoginWorkflow(authRepository);
        }

        public PingResponse Ping(PingRequest request)
        {
            return new PingResponse
            {
                Echo = !string.IsNullOrWhiteSpace(request?.Message)
                    ? request.Message
                    : AuthServiceConstants.MESSAGE_PONG,
                Utc = DateTime.UtcNow
            };
        }

        public BeginRegisterResponse BeginRegister(BeginRegisterRequest request)
        {
            return beginRegisterWorkflow.Execute(request);
        }

        public RegisterResponse CompleteRegister(CompleteRegisterRequest request)
        {
            return completeRegisterWorkflow.Execute(request);
        }

        public RegisterResponse Register(RegisterRequest request)
        {
            return registerWorkflow.Execute(request);
        }

        public GetProfileImageResponse GetProfileImage(GetProfileImageRequest request)
        {
            return getProfileImageWorkflow.Execute(request);
        }

        public LoginResponse GuestLogin(GuestLoginRequest request)
        {
            return guestLoginWorkflow.Execute(request);
        }

        public LoginResponse Login(LoginRequest request)
        {
            return loginWorkflow.Execute(request);
        }

        public void Logout(LogoutRequest request)
        {
            logoutWorkflow.Execute(request);
        }

        public BeginPasswordResetResponse BeginPasswordReset(BeginPasswordResetRequest request)
        {
            return beginPasswordResetWorkflow.Execute(request);
        }

        public void CompletePasswordReset(CompletePasswordResetRequest request)
        {
            completePasswordResetWorkflow.Execute(request);
        }
    }
}
