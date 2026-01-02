using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Friends.Infrastructure
{
    internal sealed class FriendAccountRepository : IFriendAccountRepository
    {
        public SearchAccountItem[] SearchAccounts(
            SqlConnection connection,
            int myAccountId,
            string likeQuery,
            int maxResults)
        {
            var results = new List<SearchAccountItem>();

            using (SqlCommand command = new SqlCommand(FriendSql.Text.SEARCH_ACCOUNTS, connection))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = myAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_MAX, SqlDbType.Int).Value = maxResults;

                command.Parameters.Add(
                        FriendServiceContext.PARAM_QUERY_EMAIL,
                        SqlDbType.NVarChar,
                        FriendServiceContext.MAX_EMAIL_LENGTH)
                    .Value = likeQuery;

                command.Parameters.Add(
                        FriendServiceContext.PARAM_QUERY_NAME,
                        SqlDbType.NVarChar,
                        FriendServiceContext.MAX_DISPLAY_NAME_LENGTH)
                    .Value = likeQuery;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new SearchAccountItem
                        {
                            AccountId = reader.GetInt32(0),
                            DisplayName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            Email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            AvatarUrl = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            IsFriend = reader.GetInt32(4) == 1,
                            HasPendingOutgoing = reader.GetInt32(5) == 1,
                            HasPendingIncoming = reader.GetInt32(6) == 1,
                            PendingIncomingRequestId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7)
                        });
                    }
                }
            }

            return results.ToArray();
        }

        public List<AccountMini> GetAccountsByIds(
            SqlConnection connection,
            int myAccountId,
            int[] ids,
            IDictionary<int, UserAvatarEntity> avatarsByUserId)
        {
            var accounts = new List<AccountMini>();

            string sqlQuery = FriendSql.BuildGetAccountsByIdsQuery(ids.Length);

            using (SqlCommand command = new SqlCommand(sqlQuery, connection))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = myAccountId;

                for (int i = 0; i < ids.Length; i++)
                {
                    command.Parameters.Add(FriendServiceContext.PARAM_ID_LIST_PREFIX + i, SqlDbType.Int).Value = ids[i];
                }

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int accountId = reader.GetInt32(0);
                        string displayName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        string email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                        string avatarUrl = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);

                        avatarsByUserId.TryGetValue(accountId, out UserAvatarEntity avatarEntity);

                        accounts.Add(new AccountMini
                        {
                            AccountId = accountId,
                            DisplayName = displayName,
                            Email = email,
                            AvatarUrl = avatarUrl,
                            Avatar = FriendServiceContext.MapAvatar(avatarEntity)
                        });
                    }
                }
            }

            return accounts;
        }
    }
}
