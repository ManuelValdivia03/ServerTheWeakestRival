using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Friends
{
    internal interface IFriendInviteRepository
    {
        bool ExistsAcceptedFriendship(SqlConnection connection, int myAccountId, int targetAccountId);

        AccountContactLookup GetAccountContact(SqlConnection connection, int accountId);
    }
}
