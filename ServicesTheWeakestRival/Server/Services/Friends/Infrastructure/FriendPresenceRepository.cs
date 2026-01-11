using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Friends.Infrastructure
{
    internal sealed class FriendPresenceRepository : IFriendPresenceRepository
    {
        private const int FLAG_TRUE = 1;

        private const int ORD_FRIENDS_FROM_ID = 0;
        private const int ORD_FRIENDS_TO_ID = 1;
        private const int ORD_FRIENDS_SINCE_UTC = 2;

        private const int ORD_PENDING_ID = 0;
        private const int ORD_PENDING_FROM = 1;
        private const int ORD_PENDING_TO = 2;
        private const int ORD_PENDING_SENT_UTC = 3;

        private const int ORD_SUMMARY_ACCOUNT_ID = 0;
        private const int ORD_SUMMARY_EMAIL = 1;
        private const int ORD_SUMMARY_DISPLAY_NAME = 2;
        private const int ORD_SUMMARY_HAS_PROFILE_IMAGE = 3;
        private const int ORD_SUMMARY_PROFILE_IMAGE_CODE = 4;
        private const int ORD_SUMMARY_LAST_SEEN_UTC = 5;

        public List<FriendSummary> LoadFriends(FriendDbContext db, DateTime utcNow)
        {
            var friends = new List<FriendSummary>();

            using (SqlCommand command = new SqlCommand(FriendSql.Text.FRIENDS, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_ACCEPTED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Accepted;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int fromId = reader.GetInt32(ORD_FRIENDS_FROM_ID);
                        int toId = reader.GetInt32(ORD_FRIENDS_TO_ID);

                        DateTime sinceUtc = reader.IsDBNull(ORD_FRIENDS_SINCE_UTC)
                            ? utcNow
                            : reader.GetDateTime(ORD_FRIENDS_SINCE_UTC);

                        int friendId = fromId == db.MyAccountId ? toId : fromId;

                        FriendSummary summary = LoadFriendSummary(
                            db.Connection,
                            db.Transaction,
                            friendId,
                            sinceUtc,
                            utcNow);

                        friends.Add(summary);
                    }
                }
            }

            return friends;
        }

        public FriendRequestSummary[] LoadPendingRequests(FriendDbContext db, string sqlText)
        {
            if (string.IsNullOrWhiteSpace(sqlText))
            {
                return Array.Empty<FriendRequestSummary>();
            }

            var list = new List<FriendRequestSummary>();

            using (SqlCommand command = new SqlCommand(sqlText, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_PENDING, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Pending;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new FriendRequestSummary
                        {
                            FriendRequestId = reader.GetInt32(ORD_PENDING_ID),
                            FromAccountId = reader.GetInt32(ORD_PENDING_FROM),
                            ToAccountId = reader.GetInt32(ORD_PENDING_TO),
                            Message = string.Empty,
                            Status = FriendRequestStatus.Pending,
                            CreatedUtc = reader.GetDateTime(ORD_PENDING_SENT_UTC),
                            ResolvedUtc = null
                        });
                    }
                }
            }

            return list.ToArray();
        }

        public void UpsertPresence(FriendDbContext db, string device)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.PRESENCE_UPDATE, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;

                command.Parameters.Add(
                        FriendServiceContext.PARAM_DEVICE,
                        SqlDbType.NVarChar,
                        FriendServiceContext.DEVICE_MAX_LENGTH)
                    .Value = string.IsNullOrWhiteSpace(device) ? (object)DBNull.Value : device;

                int affectedRows = command.ExecuteNonQuery();
                if (affectedRows != 0)
                {
                    return;
                }
            }

            using (SqlCommand insertCommand = new SqlCommand(FriendSql.Text.PRESENCE_INSERT, db.Connection, db.Transaction))
            {
                insertCommand.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;

                insertCommand.Parameters.Add(
                        FriendServiceContext.PARAM_DEVICE,
                        SqlDbType.NVarChar,
                        FriendServiceContext.DEVICE_MAX_LENGTH)
                    .Value = string.IsNullOrWhiteSpace(device) ? (object)DBNull.Value : device;

                insertCommand.ExecuteNonQuery();
            }
        }

        public FriendPresence[] GetFriendsPresence(FriendDbContext db, DateTime utcNow)
        {
            var list = new List<FriendPresence>();

            using (SqlCommand command = new SqlCommand(FriendSql.Text.FRIENDS_PRESENCE, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_ACCEPTED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Accepted;
                command.Parameters.Add(FriendServiceContext.PARAM_WINDOW, SqlDbType.Int).Value =
                    FriendServiceContext.ONLINE_WINDOW_SECONDS;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new FriendPresence
                        {
                            AccountId = reader.GetInt32(0),
                            IsOnline = reader.GetInt32(1) == FLAG_TRUE,
                            LastSeenUtc = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2)
                        });
                    }
                }
            }

            return list.ToArray();
        }

        private static FriendSummary LoadFriendSummary(
            SqlConnection connection,
            SqlTransaction transaction,
            int friendId,
            DateTime sinceUtc,
            DateTime utcNow)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.FRIEND_SUMMARY, connection, transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ID, SqlDbType.Int).Value = friendId;

                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        string fallbackUsername = FriendServiceContext.FALLBACK_USERNAME_PREFIX + friendId;

                        FriendServiceContext.Logger.WarnFormat(
                            "LoadFriendSummary: no data for FriendId={0}.",
                            friendId);

                        return new FriendSummary
                        {
                            AccountId = friendId,
                            Username = fallbackUsername,
                            DisplayName = fallbackUsername,
                            HasProfileImage = false,
                            ProfileImageCode = string.Empty,
                            SinceUtc = sinceUtc,
                            IsOnline = false
                        };
                    }

                    int accountId = reader.GetInt32(ORD_SUMMARY_ACCOUNT_ID);

                    string email = reader.IsDBNull(ORD_SUMMARY_EMAIL) ? string.Empty : reader.GetString(ORD_SUMMARY_EMAIL);
                    string displayName = reader.IsDBNull(ORD_SUMMARY_DISPLAY_NAME) ? string.Empty : reader.GetString(ORD_SUMMARY_DISPLAY_NAME);

                    bool hasProfileImage =
                        !reader.IsDBNull(ORD_SUMMARY_HAS_PROFILE_IMAGE)
                        && reader.GetInt32(ORD_SUMMARY_HAS_PROFILE_IMAGE) == FLAG_TRUE;

                    string profileImageCode = reader.IsDBNull(ORD_SUMMARY_PROFILE_IMAGE_CODE)
                        ? string.Empty
                        : reader.GetString(ORD_SUMMARY_PROFILE_IMAGE_CODE);

                    DateTime? lastSeenUtc = reader.IsDBNull(ORD_SUMMARY_LAST_SEEN_UTC)
                        ? (DateTime?)null
                        : reader.GetDateTime(ORD_SUMMARY_LAST_SEEN_UTC);

                    DateTime onlineWindowStartUtc = utcNow.AddSeconds(-FriendServiceContext.ONLINE_WINDOW_SECONDS);
                    bool isOnline = lastSeenUtc.HasValue && lastSeenUtc.Value >= onlineWindowStartUtc;

                    string username = string.IsNullOrWhiteSpace(email)
                        ? FriendServiceContext.FALLBACK_USERNAME_PREFIX + accountId
                        : email;

                    string effectiveDisplayName = !string.IsNullOrWhiteSpace(displayName)
                        ? displayName
                        : username;

                    return new FriendSummary
                    {
                        AccountId = accountId,
                        Username = username,
                        DisplayName = effectiveDisplayName,
                        HasProfileImage = hasProfileImage,
                        ProfileImageCode = profileImageCode ?? string.Empty,
                        SinceUtc = sinceUtc,
                        IsOnline = isOnline
                    };
                }
            }
        }
    }
}
