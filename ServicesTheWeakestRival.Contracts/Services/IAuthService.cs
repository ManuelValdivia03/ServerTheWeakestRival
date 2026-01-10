using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    [ServiceContract]
    public interface IAuthService
    {
        [OperationContract]
        PingResponse Ping(PingRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        BeginRegisterResponse BeginRegister(BeginRegisterRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        RegisterResponse CompleteRegister(CompleteRegisterRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        RegisterResponse Register(RegisterRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        LoginResponse Login(LoginRequest request);

        [OperationContract]
        void Logout(LogoutRequest request);

        [OperationContract]
        BeginPasswordResetResponse BeginPasswordReset(BeginPasswordResetRequest request);

        [OperationContract]
        void CompletePasswordReset(CompletePasswordResetRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        GetProfileImageResponse GetProfileImage(GetProfileImageRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        LoginResponse GuestLogin(GuestLoginRequest request);
    }
}
