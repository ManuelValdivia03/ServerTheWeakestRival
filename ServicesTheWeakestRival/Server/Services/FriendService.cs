using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Friends;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class FriendService : IFriendService
    {
        private readonly FriendRequestLogic requestLogic;
        private readonly FriendPresenceLogic presenceLogic;
        private readonly FriendAccountLogic accountLogic;
        private readonly FriendInviteLogic inviteLogic;

        public FriendService()
        {
            IFriendRequestRepository friendRequestRepository = new FriendRequestRepository();
            requestLogic = new FriendRequestLogic(friendRequestRepository);

            IFriendPresenceRepository presenceRepository = new FriendPresenceRepository();
            presenceLogic = new FriendPresenceLogic(presenceRepository);

            IFriendAccountRepository accountRepository = new FriendAccountRepository();
            accountLogic = new FriendAccountLogic(accountRepository);

            IFriendInviteRepository inviteRepository = new FriendInviteRepository();
            inviteLogic = new FriendInviteLogic(inviteRepository);
        }

        public SendFriendRequestResponse SendFriendRequest(SendFriendRequestRequest request) => requestLogic.SendFriendRequest(request);
        public AcceptFriendRequestResponse AcceptFriendRequest(AcceptFriendRequestRequest request) => requestLogic.AcceptFriendRequest(request);
        public RejectFriendRequestResponse RejectFriendRequest(RejectFriendRequestRequest request) => requestLogic.RejectFriendRequest(request);
        public RemoveFriendResponse RemoveFriend(RemoveFriendRequest request) => requestLogic.RemoveFriend(request);

        public ListFriendsResponse ListFriends(ListFriendsRequest request) => presenceLogic.ListFriends(request);
        public HeartbeatResponse PresenceHeartbeat(HeartbeatRequest request) => presenceLogic.PresenceHeartbeat(request);
        public GetFriendsPresenceResponse GetFriendsPresence(GetFriendsPresenceRequest request) => presenceLogic.GetFriendsPresence(request);

        public SearchAccountsResponse SearchAccounts(SearchAccountsRequest request) => accountLogic.SearchAccounts(request);
        public GetAccountsByIdsResponse GetAccountsByIds(GetAccountsByIdsRequest request) => accountLogic.GetAccountsByIds(request);

        public GetProfileImageResponse GetProfileImage(GetProfileImageRequest request) => accountLogic.GetProfileImage(request);

        public SendLobbyInviteEmailResponse SendLobbyInviteEmail(SendLobbyInviteEmailRequest request) => inviteLogic.SendLobbyInviteEmail(request);
    }
}
