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
                        int fromId = reader.GetInt32(0);
                        int toId = reader.GetInt32(1);
                        DateTime sinceUtc = reader.IsDBNull(2) ? utcNow : reader.GetDateTime(2);

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
                            FriendRequestId = reader.GetInt32(0),
                            FromAccountId = reader.GetInt32(1),
                            ToAccountId = reader.GetInt32(2),
                            Message = null,
                            Status = FriendRequestStatus.Pending,
                            CreatedUtc = reader.GetDateTime(3),
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
                            IsOnline = reader.GetInt32(1) == 1,
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
                            "LoadFriendSummary: no data for FriendId={0}. Returning fallback summary.",
                            friendId);

                        return new FriendSummary
                        {
                            AccountId = friendId,
                            Username = fallbackUsername,
                            DisplayName = fallbackUsername,
                            AvatarUrl = string.Empty,
                            SinceUtc = sinceUtc,
                            IsOnline = false
                        };
                    }

                    int accountId = reader.GetInt32(0);

                    string email = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    string displayName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    string avatarUrl = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    DateTime? lastSeenUtc = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);

                    DateTime onlineWindowStartUtc =
                        utcNow.AddSeconds(-FriendServiceContext.ONLINE_WINDOW_SECONDS);

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
                        AvatarUrl = avatarUrl,
                        SinceUtc = sinceUtc,
                        IsOnline = isOnline
                    };
                }
            }
        }
    }
}
