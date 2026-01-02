using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Auth;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class AuthService : IAuthService
    {
        private readonly AuthLogic _logic;

        public AuthService()
        {
            _logic = new AuthLogic();
        }

        public PingResponse Ping(PingRequest request)
        {
            return _logic.Ping(request);
        }

        public BeginRegisterResponse BeginRegister(BeginRegisterRequest request)
        {
            return _logic.BeginRegister(request);
        }

        public CompleteRegisterResponse CompleteRegister(CompleteRegisterRequest request)
        {
            return _logic.CompleteRegister(request);
        }

        public LoginResponse Login(LoginRequest request)
        {
            return _logic.Login(request);
        }

        public AuthToken ValidateToken(AuthToken token)
        {
            return _logic.ValidateToken(token);
        }

        public ResetPasswordResponse ResetPassword(ResetPasswordRequest request)
        {
            return _logic.ResetPassword(request);
        }

        public BeginPasswordChangeResponse BeginPasswordChange(BeginPasswordChangeRequest request)
        {
            return _logic.BeginPasswordChange(request);
        }

        public CompletePasswordChangeResponse CompletePasswordChange(CompletePasswordChangeRequest request)
        {
            return _logic.CompletePasswordChange(request);
        }
    }
}
