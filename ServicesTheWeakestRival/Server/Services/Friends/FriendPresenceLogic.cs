using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Friends;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class FriendPresenceLogic
    {
        private readonly IFriendPresenceRepository _presenceRepository;

        public FriendPresenceLogic(IFriendPresenceRepository presenceRepository)
        {
            _presenceRepository = presenceRepository ??
                throw new ArgumentNullException(nameof(presenceRepository));
        }

        public ListFriendsResponse ListFriends(ListFriendsRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);

            return FriendServiceContext.ExecuteDbOperation(
                FriendServiceContext.CONTEXT_LIST_FRIENDS,
                connection =>
                {
                    DateTime utcNow = DateTime.UtcNow;

                    var db = new FriendDbContext(connection, transaction: null, myAccountId);

                    List<FriendSummary> friends = _presenceRepository.LoadFriends(db, utcNow);

                    FriendRequestSummary[] incoming = _presenceRepository.LoadPendingRequests(
                        db,
                        FriendSql.Text.PENDING_INCOMING);

                    FriendRequestSummary[] outgoing = _presenceRepository.LoadPendingRequests(
                        db,
                        FriendSql.Text.PENDING_OUTGOING);

                    FriendSummary[] orderedFriends = friends
                        .OrderBy(f => f.DisplayName)
                        .ToArray();

                    var response = new ListFriendsResponse
                    {
                        Friends = orderedFriends,
                        PendingIncoming = incoming,
                        PendingOutgoing = outgoing
                    };

                    FriendServiceContext.Logger.InfoFormat(
                        "ListFriends: Me={0}, Friends={1}, Incoming={2}, Outgoing={3}",
                        myAccountId,
                        response.Friends.Length,
                        response.PendingIncoming.Length,
                        response.PendingOutgoing.Length);

                    return response;
                });
        }

        public HeartbeatResponse PresenceHeartbeat(HeartbeatRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);

            return FriendServiceContext.ExecuteDbOperation(
                FriendServiceContext.CONTEXT_PRESENCE_HEARTBEAT,
                connection =>
                {
                    var db = new FriendDbContext(connection, transaction: null, myAccountId);

                    _presenceRepository.UpsertPresence(db, request.Device);

                    DateTime utcNow = DateTime.UtcNow;

                    FriendServiceContext.Logger.DebugFormat(
                        "PresenceHeartbeat: updated. Me={0}, Utc={1}",
                        myAccountId,
                        utcNow.ToString("o"));

                    return new HeartbeatResponse
                    {
                        Utc = utcNow
                    };
                });
        }

        public GetFriendsPresenceResponse GetFriendsPresence(GetFriendsPresenceRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);

            return FriendServiceContext.ExecuteDbOperation(
                FriendServiceContext.CONTEXT_GET_FRIENDS_PRESENCE,
                connection =>
                {
                    DateTime utcNow = DateTime.UtcNow;

                    var db = new FriendDbContext(connection, transaction: null, myAccountId);

                    FriendPresence[] friends = _presenceRepository.GetFriendsPresence(db, utcNow);

                    FriendServiceContext.Logger.DebugFormat(
                        "GetFriendsPresence: Me={0}, Count={1}",
                        myAccountId,
                        friends.Length);

                    return new GetFriendsPresenceResponse
                    {
                        Friends = friends
                    };
                });
        }
    }
}
