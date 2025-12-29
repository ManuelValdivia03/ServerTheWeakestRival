using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Friends
{
    internal interface IFriendAccountRepository
    {
        SearchAccountItem[] SearchAccounts(
            SqlConnection connection,
            int myAccountId,
            string likeQuery,
            int maxResults);

        List<AccountMini> GetAccountsByIds(
            SqlConnection connection,
            int myAccountId,
            int[] ids,
            IDictionary<int, UserAvatarEntity> avatarsByUserId);
    }
}
