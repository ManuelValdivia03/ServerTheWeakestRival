using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using System;
using System.Collections.Generic;

namespace ServicesTheWeakestRival.Server.Services.Friends
{
    internal interface IFriendPresenceRepository
    {
        List<FriendSummary> LoadFriends(FriendDbContext db, DateTime utcNow);

        FriendRequestSummary[] LoadPendingRequests(FriendDbContext db, string sqlText);

        void UpsertPresence(FriendDbContext db, string device);

        FriendPresence[] GetFriendsPresence(FriendDbContext db, DateTime utcNow);
    }
}
