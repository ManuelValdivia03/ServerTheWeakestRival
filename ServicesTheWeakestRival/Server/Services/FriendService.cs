using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Friends;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class FriendService : IFriendService
    {
        private readonly FriendRequestLogic _requestLogic;
        private readonly FriendPresenceLogic _presenceLogic;
        private readonly FriendAccountLogic _accountLogic;
        private readonly FriendInviteLogic _inviteLogic;

        public FriendService()
        {
            IFriendRequestRepository friendRequestRepository = new FriendRequestRepository();
            _requestLogic = new FriendRequestLogic(friendRequestRepository);

            IFriendPresenceRepository presenceRepository = new FriendPresenceRepository();
            _presenceLogic = new FriendPresenceLogic(presenceRepository);

            IFriendAccountRepository accountRepository = new FriendAccountRepository();
            _accountLogic = new FriendAccountLogic(accountRepository);

            IFriendInviteRepository inviteRepository = new FriendInviteRepository();
            _inviteLogic = new FriendInviteLogic(inviteRepository);
        }

        public SendFriendRequestResponse SendFriendRequest(SendFriendRequestRequest request)
        {
            return _requestLogic.SendFriendRequest(request);
        }

        public AcceptFriendRequestResponse AcceptFriendRequest(AcceptFriendRequestRequest request)
        {
            return _requestLogic.AcceptFriendRequest(request);
        }

        public RejectFriendRequestResponse RejectFriendRequest(RejectFriendRequestRequest request)
        {
            return _requestLogic.RejectFriendRequest(request);
        }

        public RemoveFriendResponse RemoveFriend(RemoveFriendRequest request)
        {
            return _requestLogic.RemoveFriend(request);
        }

        public ListFriendsResponse ListFriends(ListFriendsRequest request)
        {
            return _presenceLogic.ListFriends(request);
        }

        public HeartbeatResponse PresenceHeartbeat(HeartbeatRequest request)
        {
            return _presenceLogic.PresenceHeartbeat(request);
        }

        public GetFriendsPresenceResponse GetFriendsPresence(GetFriendsPresenceRequest request)
        {
            return _presenceLogic.GetFriendsPresence(request);
        }

        public SearchAccountsResponse SearchAccounts(SearchAccountsRequest request)
        {
            return _accountLogic.SearchAccounts(request);
        }

        public GetAccountsByIdsResponse GetAccountsByIds(GetAccountsByIdsRequest request)
        {
            return _accountLogic.GetAccountsByIds(request);
        }

        public SendLobbyInviteEmailResponse SendLobbyInviteEmail(SendLobbyInviteEmailRequest request)
        {
            return _inviteLogic.SendLobbyInviteEmail(request);
        }
    }
}
