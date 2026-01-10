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
        private const int FlagTrue = 1;

        private const int FallbackOrdAccountId = 0;
        private const int FallbackOrdDisplayName = 1;
        private const int FallbackOrdEmail = 2;

        private const int FallbackOrdHasProfileImage = 3;
        private const int FallbackOrdProfileImageCode = 4;

        private const int FallbackOrdIsFriend = 5;
        private const int FallbackOrdHasPendingOutgoing = 6;
        private const int FallbackOrdHasPendingIncoming = 7;
        private const int FallbackOrdPendingIncomingRequestId = 8;

        private const string ColAccountId = "account_id";
        private const string ColDisplayName = "display_name";
        private const string ColEmail = "email";

        private const string ColHasProfileImage = "has_profile_image";
        private const string ColProfileImageCode = "profile_image_code";

        private const string ColIsFriend = "is_friend";
        private const string ColHasPendingOutgoing = "has_pending_outgoing";
        private const string ColHasPendingIncoming = "has_pending_incoming";
        private const string ColPendingIncomingRequestId = "pending_incoming_request_id";

        public SearchAccountItem[] SearchAccounts(
            SqlConnection connection,
            int myAccountId,
            string likeQuery,
            int maxResults)
        {
            var results = new List<SearchAccountItem>();

            using (var command = new SqlCommand(FriendSql.Text.SEARCH_ACCOUNTS, connection))
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

                using (var reader = command.ExecuteReader())
                {
                    var ordinalMap = BuildOrdinalMap(reader);

                    int ordAccountId = GetOrdinalOrFallback(ordinalMap, ColAccountId, FallbackOrdAccountId);
                    int ordDisplayName = GetOrdinalOrFallback(ordinalMap, ColDisplayName, FallbackOrdDisplayName);
                    int ordEmail = GetOrdinalOrFallback(ordinalMap, ColEmail, FallbackOrdEmail);

                    int ordHasProfileImage = GetOrdinalOrFallback(ordinalMap, ColHasProfileImage, FallbackOrdHasProfileImage);
                    int ordProfileImageCode = GetOrdinalOrFallback(ordinalMap, ColProfileImageCode, FallbackOrdProfileImageCode);

                    int ordIsFriend = GetOrdinalOrFallback(ordinalMap, ColIsFriend, FallbackOrdIsFriend);
                    int ordHasPendingOutgoing = GetOrdinalOrFallback(ordinalMap, ColHasPendingOutgoing, FallbackOrdHasPendingOutgoing);
                    int ordHasPendingIncoming = GetOrdinalOrFallback(ordinalMap, ColHasPendingIncoming, FallbackOrdHasPendingIncoming);
                    int ordPendingIncomingRequestId = GetOrdinalOrFallback(ordinalMap, ColPendingIncomingRequestId, FallbackOrdPendingIncomingRequestId);

                    while (reader.Read())
                    {
                        results.Add(new SearchAccountItem
                        {
                            AccountId = ReadInt32(reader, ordAccountId),
                            DisplayName = ReadStringOrEmpty(reader, ordDisplayName),
                            Email = ReadStringOrEmpty(reader, ordEmail),

                            HasProfileImage = ReadFlag(reader, ordHasProfileImage),
                            ProfileImageCode = ReadStringOrEmpty(reader, ordProfileImageCode),

                            IsFriend = ReadFlag(reader, ordIsFriend),
                            HasPendingOutgoing = ReadFlag(reader, ordHasPendingOutgoing),
                            HasPendingIncoming = ReadFlag(reader, ordHasPendingIncoming),
                            PendingIncomingRequestId = ReadNullableInt32(reader, ordPendingIncomingRequestId)
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

            if (ids == null || ids.Length == 0)
            {
                return accounts;
            }

            string sqlQuery = FriendSql.BuildGetAccountsByIdsQuery(ids.Length);

            using (var command = new SqlCommand(sqlQuery, connection))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = myAccountId;

                for (int i = 0; i < ids.Length; i++)
                {
                    command.Parameters.Add(FriendServiceContext.PARAM_ID_LIST_PREFIX + i, SqlDbType.Int).Value = ids[i];
                }

                using (var reader = command.ExecuteReader())
                {
                    var ordinalMap = BuildOrdinalMap(reader);

                    int ordAccountId = GetOrdinalOrFallback(ordinalMap, ColAccountId, FallbackOrdAccountId);
                    int ordDisplayName = GetOrdinalOrFallback(ordinalMap, ColDisplayName, FallbackOrdDisplayName);
                    int ordEmail = GetOrdinalOrFallback(ordinalMap, ColEmail, FallbackOrdEmail);

                    int ordHasProfileImage = GetOrdinalOrFallback(ordinalMap, ColHasProfileImage, FallbackOrdHasProfileImage);
                    int ordProfileImageCode = GetOrdinalOrFallback(ordinalMap, ColProfileImageCode, FallbackOrdProfileImageCode);

                    while (reader.Read())
                    {
                        int accountId = ReadInt32(reader, ordAccountId);

                        UserAvatarEntity avatarEntity = null;
                        if (avatarsByUserId != null)
                        {
                            avatarsByUserId.TryGetValue(accountId, out avatarEntity);
                        }

                        accounts.Add(new AccountMini
                        {
                            AccountId = accountId,
                            DisplayName = ReadStringOrEmpty(reader, ordDisplayName),
                            Email = ReadStringOrEmpty(reader, ordEmail),

                            HasProfileImage = ReadFlag(reader, ordHasProfileImage),
                            ProfileImageCode = ReadStringOrEmpty(reader, ordProfileImageCode),

                            Avatar = FriendServiceContext.MapAvatar(avatarEntity)
                        });
                    }
                }
            }

            return accounts;
        }

        public UserProfileImageEntity GetProfileImage(SqlConnection connection, int accountId)
        {
            if (connection == null || accountId <= 0)
            {
                return new UserProfileImageEntity(Array.Empty<byte>(), string.Empty, null, string.Empty);
            }

            using (var command = new SqlCommand(FriendSql.Text.GET_PROFILE_IMAGE, connection))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ID, SqlDbType.Int).Value = accountId;

                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return new UserProfileImageEntity(Array.Empty<byte>(), string.Empty, null, string.Empty);
                    }

                    byte[] bytes = reader.IsDBNull(0) ? Array.Empty<byte>() : (byte[])reader.GetValue(0);
                    string contentType = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1));
                    DateTime? updatedAtUtc = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);

                    string code = bytes.Length == 0
                        ? string.Empty
                        : (updatedAtUtc.HasValue ? updatedAtUtc.Value.ToString("o") : "1");

                    return new UserProfileImageEntity(bytes ?? Array.Empty<byte>(), contentType ?? string.Empty, updatedAtUtc, code);
                }
            }
        }

        private static Dictionary<string, int> BuildOrdinalMap(IDataRecord record)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < record.FieldCount; i++)
            {
                string name = record.GetName(i);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    map[name] = i;
                }
            }

            return map;
        }

        private static int GetOrdinalOrFallback(Dictionary<string, int> map, string columnName, int fallbackOrdinal)
        {
            return map != null && map.TryGetValue(columnName, out int ordinal)
                ? ordinal
                : fallbackOrdinal;
        }

        private static string ReadStringOrEmpty(SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal));
        }

        private static int ReadInt32(SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static int? ReadNullableInt32(SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static bool ReadFlag(SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return false;
            }

            object value = reader.GetValue(ordinal);

            if (value is bool b)
            {
                return b;
            }

            return Convert.ToInt32(value) == FlagTrue;
        }
    }
}
