using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class AuthService : IAuthService
    {
        private readonly AuthOperations authOperations;

        public AuthService()
            : this(new PasswordService(AuthServiceConstants.PASSWORD_MIN_LENGTH), new SmtpEmailService())
        {
        }

        public AuthService(PasswordService passwordService, IEmailService emailService)
        {
            var repository = new AuthRepository(
                () => AuthServiceContext.ResolveConnectionString(AuthServiceConstants.MAIN_CONNECTION_STRING_NAME));

            authOperations = new AuthOperations(repository, passwordService, emailService);
        }

        public PingResponse Ping(PingRequest request) => 
            authOperations.Ping(request);

        public LoginResponse GuestLogin(GuestLoginRequest request) =>
            authOperations.GuestLogin(request);

        public BeginRegisterResponse BeginRegister(BeginRegisterRequest request) => 
            authOperations.BeginRegister(request);

        public RegisterResponse CompleteRegister(CompleteRegisterRequest request) => 
            authOperations.CompleteRegister(request);

        public RegisterResponse Register(RegisterRequest request) => 
            authOperations.Register(request);

        public LoginResponse Login(LoginRequest request) => 
            authOperations.Login(request);

        public void Logout(LogoutRequest request) => 
            authOperations.Logout(request);

        public BeginPasswordResetResponse BeginPasswordReset(BeginPasswordResetRequest request) =>
            authOperations.BeginPasswordReset(request);

        public void CompletePasswordReset(CompletePasswordResetRequest request) =>
            authOperations.CompletePasswordReset(request);

        public GetProfileImageResponse GetProfileImage(GetProfileImageRequest request) =>
            authOperations.GetProfileImage(request);
    }
}
