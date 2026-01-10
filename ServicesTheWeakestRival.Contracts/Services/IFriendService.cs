using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    [ServiceContract]
    public interface IFriendService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        SendFriendRequestResponse SendFriendRequest(SendFriendRequestRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        AcceptFriendRequestResponse AcceptFriendRequest(AcceptFriendRequestRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        RejectFriendRequestResponse RejectFriendRequest(RejectFriendRequestRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        RemoveFriendResponse RemoveFriend(RemoveFriendRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        ListFriendsResponse ListFriends(ListFriendsRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        SearchAccountsResponse SearchAccounts(SearchAccountsRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        HeartbeatResponse PresenceHeartbeat(HeartbeatRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        GetFriendsPresenceResponse GetFriendsPresence(GetFriendsPresenceRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        GetAccountsByIdsResponse GetAccountsByIds(GetAccountsByIdsRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        GetProfileImageResponse GetProfileImage(GetProfileImageRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        SendLobbyInviteEmailResponse SendLobbyInviteEmail(SendLobbyInviteEmailRequest request);
    }
}
