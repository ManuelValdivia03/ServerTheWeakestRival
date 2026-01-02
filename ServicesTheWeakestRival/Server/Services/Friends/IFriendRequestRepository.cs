using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;

namespace ServicesTheWeakestRival.Server.Services.Friends
{
    internal interface IFriendRequestRepository
    {
        bool FriendshipExists(FriendDbContext db, int targetAccountId);

        int? GetPendingOutgoingId(FriendDbContext db, int targetAccountId);
        int? GetPendingIncomingId(FriendDbContext db, int targetAccountId);

        int AcceptIncomingRequest(FriendDbContext db, int requestId);
        int InsertNewRequest(FriendDbContext db, int targetAccountId);
        int ReopenRequest(FriendDbContext db, int targetAccountId);

        FriendRequestRow ReadRequestRow(FriendDbContext db, string sqlText, int requestId);

        int AcceptRequest(FriendDbContext db, int friendRequestId, int myAccountId);
        int RejectAsReceiver(FriendDbContext db, int friendRequestId);
        int CancelAsSender(FriendDbContext db, int friendRequestId);

        int? GetLatestAcceptedFriendRequestId(FriendDbContext db, int otherAccountId);
        int MarkFriendRequestCancelled(FriendDbContext db, int friendRequestId);
    }
}
