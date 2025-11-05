using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    [ServiceContract]
    public interface ILobbyClientCallback
    {
        [OperationContract(IsOneWay = true)] void OnLobbyUpdated(LobbyInfo lobby);
        [OperationContract(IsOneWay = true)] void OnPlayerJoined(PlayerSummary player);
        [OperationContract(IsOneWay = true)] void OnPlayerLeft(Guid playerId);
        [OperationContract(IsOneWay = true)] void OnChatMessageReceived(ChatMessage message);
    }

    [ServiceContract(CallbackContract = typeof(ILobbyClientCallback))]
    public interface ILobbyService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        JoinLobbyResponse JoinLobby(JoinLobbyRequest request);

        [OperationContract] void LeaveLobby(LeaveLobbyRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        ListLobbiesResponse ListLobbies(ListLobbiesRequest request);

        [OperationContract(IsOneWay = true)]
        void SendChatMessage(SendLobbyMessageRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        UpdateAccountResponse GetMyProfile(string token);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        UpdateAccountResponse UpdateAccount(UpdateAccountRequest request);

        [OperationContract, FaultContract(typeof(ServiceFault))]
        CreateLobbyResponse CreateLobby(CreateLobbyRequest request);

        [OperationContract, FaultContract(typeof(ServiceFault))]
        JoinByCodeResponse JoinByCode(JoinByCodeRequest request);
    }
}
