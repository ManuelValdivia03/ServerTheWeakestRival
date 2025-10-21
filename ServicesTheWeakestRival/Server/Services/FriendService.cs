using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public sealed class FriendService : IFriendService
    {
        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;
        private const int ONLINE_WINDOW_SECONDS = 75;

        private static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["TheWeakestRivalDb"];
                if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                    ThrowFault("CONFIG_ERROR", "Missing connection string 'TheWeakestRivalDb'.");
                return cs.ConnectionString;
            }
        }

        private static void ThrowFault(string code, string message)
        {
            var fault = new ServiceFault { Code = code, Message = message };
            throw new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        private static bool IsUniqueViolation(SqlException ex) => ex != null && (ex.Number == 2627 || ex.Number == 2601);

        private static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) ThrowFault("AUTH_REQUIRED", "Missing token.");
            if (!TokenCache.TryGetValue(token, out var at)) ThrowFault("AUTH_INVALID", "Invalid token.");
            if (at.ExpiresAtUtc <= DateTime.UtcNow) ThrowFault("AUTH_EXPIRED", "Token expired.");
            return at.UserId;
        }

        private const byte ST_PENDING = 0;
        private const byte ST_ACCEPTED = 1;
        private const byte ST_DECLINED = 2;
        private const byte ST_CANCELLED = 3;

        public SendFriendRequestResponse SendFriendRequest(SendFriendRequestRequest request)
        {
            var me = Authenticate(request.Token);
            var target = request.TargetAccountId;
            var now = DateTime.UtcNow;

            if (me == target) ThrowFault("FR_SELF", "No puedes enviarte solicitud a ti mismo.");

            const string Q_EXISTS_FRIEND =
                @"SELECT 1 
                  FROM dbo.FriendRequests 
                  WHERE ((from_user_id = @Me AND to_user_id = @Target) OR (from_user_id = @Target AND to_user_id = @Me))
                    AND status = @Accepted;";

            const string Q_PENDING_OUT =
                @"SELECT friend_request_id 
                  FROM dbo.FriendRequests 
                  WHERE from_user_id = @Me AND to_user_id = @Target AND status = @Pending;";

            const string Q_PENDING_IN =
                @"SELECT friend_request_id 
                  FROM dbo.FriendRequests 
                  WHERE from_user_id = @Target AND to_user_id = @Me AND status = @Pending;";

            const string Q_ACCEPT_IN =
                @"UPDATE dbo.FriendRequests
                  SET status = @Accepted, responded_at = SYSUTCDATETIME()
                  OUTPUT INSERTED.friend_request_id
                  WHERE friend_request_id = @ReqId;";

            const string Q_INSERT =
                @"INSERT INTO dbo.FriendRequests(from_user_id, to_user_id, status, sent_at, responded_at)
                  OUTPUT INSERTED.friend_request_id
                  VALUES(@Me, @Target, @Pending, SYSUTCDATETIME(), NULL);";

            const string Q_REOPEN =
                @"UPDATE dbo.FriendRequests
                  SET status = @Pending, sent_at = SYSUTCDATETIME(), responded_at = NULL
                  OUTPUT INSERTED.friend_request_id
                  WHERE from_user_id = @Me AND to_user_id = @Target AND status IN (@Declined, @Cancelled);";

            using (var cn = new SqlConnection(ConnectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var cmd = new SqlCommand(Q_EXISTS_FRIEND, cn, tx))
                    {
                        cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                        cmd.Parameters.Add("@Target", SqlDbType.Int).Value = target;
                        cmd.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = ST_ACCEPTED;
                        var exists = cmd.ExecuteScalar();
                        if (exists != null) ThrowFault("FR_ALREADY", "Ya existe una amistad entre estas cuentas.");
                    }

                    int? existingOutId = null;
                    using (var cmd = new SqlCommand(Q_PENDING_OUT, cn, tx))
                    {
                        cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                        cmd.Parameters.Add("@Target", SqlDbType.Int).Value = target;
                        cmd.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = ST_PENDING;
                        var val = cmd.ExecuteScalar();
                        if (val != null) existingOutId = Convert.ToInt32(val);
                    }
                    if (existingOutId.HasValue)
                    {
                        tx.Commit();
                        return new SendFriendRequestResponse
                        {
                            FriendRequestId = existingOutId.Value,
                            Status = (FriendRequestStatus)ST_PENDING
                        };
                    }

                    int? incomingId = null;
                    using (var cmd = new SqlCommand(Q_PENDING_IN, cn, tx))
                    {
                        cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                        cmd.Parameters.Add("@Target", SqlDbType.Int).Value = target;
                        cmd.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = ST_PENDING;
                        var val = cmd.ExecuteScalar();
                        if (val != null) incomingId = Convert.ToInt32(val);
                    }

                    if (incomingId.HasValue)
                    {
                        int acceptedId;
                        using (var cmd = new SqlCommand(Q_ACCEPT_IN, cn, tx))
                        {
                            cmd.Parameters.Add("@ReqId", SqlDbType.Int).Value = incomingId.Value;
                            cmd.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = ST_ACCEPTED;
                            acceptedId = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                        tx.Commit();
                        return new SendFriendRequestResponse
                        {
                            FriendRequestId = acceptedId,
                            Status = (FriendRequestStatus)ST_ACCEPTED
                        };
                    }

                    try
                    {
                        int newId;
                        using (var cmd = new SqlCommand(Q_INSERT, cn, tx))
                        {
                            cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                            cmd.Parameters.Add("@Target", SqlDbType.Int).Value = target;
                            cmd.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = ST_PENDING;
                            newId = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                        tx.Commit();
                        return new SendFriendRequestResponse
                        {
                            FriendRequestId = newId,
                            Status = (FriendRequestStatus)ST_PENDING
                        };
                    }
                    catch (SqlException ex) when (IsUniqueViolation(ex))
                    {
                        int reopenedId;
                        using (var cmd = new SqlCommand(Q_REOPEN, cn, tx))
                        {
                            cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                            cmd.Parameters.Add("@Target", SqlDbType.Int).Value = target;
                            cmd.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = ST_PENDING;
                            cmd.Parameters.Add("@Declined", SqlDbType.TinyInt).Value = ST_DECLINED;
                            cmd.Parameters.Add("@Cancelled", SqlDbType.TinyInt).Value = ST_CANCELLED;
                            var val = cmd.ExecuteScalar();
                            if (val == null) ThrowFault("FR_RACE", "No se pudo crear ni reabrir la solicitud (carrera).");
                            reopenedId = Convert.ToInt32(val);
                        }
                        tx.Commit();
                        return new SendFriendRequestResponse
                        {
                            FriendRequestId = reopenedId,
                            Status = (FriendRequestStatus)ST_PENDING
                        };
                    }
                }
            }
        }

        public AcceptFriendRequestResponse AcceptFriendRequest(AcceptFriendRequestRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request null.");

            var me = Authenticate(request.Token);

            const byte PENDING = 0;
            const byte ACCEPTED = 1;

            const string qCheck = @"
                SELECT friend_request_id, from_user_id, to_user_id, status
                FROM dbo.FriendRequests
                WHERE friend_request_id = @Id;";

            const string qAccept = @"
                UPDATE dbo.FriendRequests
                SET status = @Accepted, responded_at = SYSUTCDATETIME()
                WHERE friend_request_id = @Id
                  AND to_user_id = @Me
                  AND status = @Pending;";

            int fromId = -1, toId = -1; byte status = 0;

            using (var cn = new SqlConnection(ConnectionString))
            {
                cn.Open();

                using (var cmd = new SqlCommand(qCheck, cn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = request.FriendRequestId;
                    using (var rd = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (!rd.Read())
                            ThrowFault("FR_NOT_FOUND", "La solicitud no existe.");

                        fromId = rd.GetInt32(1);
                        toId = rd.GetInt32(2);
                        status = rd.GetByte(3);
                    }
                }

                if (toId != me)
                    ThrowFault("FORBIDDEN", "No puedes aceptar esta solicitud.");
                if (status != PENDING)
                    ThrowFault("FR_NOT_PENDING", "La solicitud ya fue procesada.");

                using (var cmd = new SqlCommand(qAccept, cn))
                {
                    cmd.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = ACCEPTED;
                    cmd.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = PENDING;
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = request.FriendRequestId;
                    cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;

                    int rows = cmd.ExecuteNonQuery();
                    if (rows == 0)
                        ThrowFault("FR_RACE", "El estado cambió. Refresca.");
                }
            }

            return new AcceptFriendRequestResponse
            {
                NewFriend = new FriendSummary
                {
                    AccountId = fromId,
                    DisplayName = null,
                    AvatarUrl = null,
                    SinceUtc = DateTime.UtcNow,
                    IsOnline = false
                }
            };
        }


        public RejectFriendRequestResponse RejectFriendRequest(RejectFriendRequestRequest request)
        {
            var me = Authenticate(request.Token);

            const string Q_GET = @"
        SELECT friend_request_id, from_user_id, to_user_id, status
        FROM dbo.FriendRequests
        WHERE friend_request_id = @Id;";

            const string Q_REJECT = @"
        UPDATE dbo.FriendRequests
        SET status = @Rejected, responded_at = SYSUTCDATETIME()
        WHERE friend_request_id = @Id
          AND to_user_id = @Me
          AND status = @Pending;";

            const string Q_CANCEL = @"
        UPDATE dbo.FriendRequests
        SET status = @Cancelled, responded_at = SYSUTCDATETIME()
        WHERE friend_request_id = @Id
          AND from_user_id = @Me
          AND status = @Pending;";

            int fromId, toId;
            byte status;

            using (var cn = new SqlConnection(ConnectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var cmd = new SqlCommand(Q_GET, cn, tx))
                    {
                        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = request.FriendRequestId;
                        using (var rd = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (!rd.Read()) ThrowFault("FR_NOT_FOUND", "La solicitud no existe.");
                            fromId = rd.GetInt32(1);
                            toId = rd.GetInt32(2);
                            status = rd.GetByte(3);
                        }
                    }

                    if (status != ST_PENDING)
                        ThrowFault("FR_NOT_PENDING", "La solicitud ya fue resuelta.");

                    if (toId == me)
                    {
                        using (var cmd = new SqlCommand(Q_REJECT, cn, tx))
                        {
                            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = request.FriendRequestId;
                            cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                            cmd.Parameters.Add("@Rejected", SqlDbType.TinyInt).Value = ST_DECLINED;
                            cmd.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = ST_PENDING;

                            var rows = cmd.ExecuteNonQuery();
                            if (rows == 0) ThrowFault("FR_RACE", "El estado cambió. Refresca.");
                        }

                        tx.Commit();
                        return new RejectFriendRequestResponse { Status = FriendRequestStatus.Rejected };
                    }
                    else if (fromId == me)
                    {
                        using (var cmd = new SqlCommand(Q_CANCEL, cn, tx))
                        {
                            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = request.FriendRequestId;
                            cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                            cmd.Parameters.Add("@Cancelled", SqlDbType.TinyInt).Value = ST_CANCELLED;
                            cmd.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = ST_PENDING;

                            var rows = cmd.ExecuteNonQuery();
                            if (rows == 0) ThrowFault("FR_RACE", "El estado cambió. Refresca.");
                        }

                        tx.Commit();
                        return new RejectFriendRequestResponse { Status = FriendRequestStatus.Cancelled };
                    }
                    else
                    {
                        ThrowFault("FR_FORBIDDEN", "No estás involucrado en esta solicitud.");
                        return new RejectFriendRequestResponse(); // unreachable
                    }
                }
            }
        }


        public RemoveFriendResponse RemoveFriend(RemoveFriendRequest request)
        {
            var me = Authenticate(request.Token);
            var other = request.FriendAccountId;

            const string Q_ACCEPTED =
                @"SELECT TOP(1) friend_request_id
                  FROM dbo.FriendRequests
                  WHERE status = @Accepted
                    AND ((from_user_id = @Me AND to_user_id = @Other) OR (from_user_id = @Other AND to_user_id = @Me))
                  ORDER BY responded_at DESC;";

            const string Q_CANCEL =
                @"UPDATE dbo.FriendRequests
                  SET status = @Cancelled, responded_at = SYSUTCDATETIME()
                  WHERE friend_request_id = @Id;";

            using (var cn = new SqlConnection(ConnectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    int? id = null;
                    using (var cmd = new SqlCommand(Q_ACCEPTED, cn, tx))
                    {
                        cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                        cmd.Parameters.Add("@Other", SqlDbType.Int).Value = other;
                        cmd.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = ST_ACCEPTED;
                        var val = cmd.ExecuteScalar();
                        if (val != null) id = Convert.ToInt32(val);
                    }

                    if (!id.HasValue)
                    {
                        tx.Commit();
                        return new RemoveFriendResponse { Removed = false };
                    }

                    using (var cmd = new SqlCommand(Q_CANCEL, cn, tx))
                    {
                        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id.Value;
                        cmd.Parameters.Add("@Cancelled", SqlDbType.TinyInt).Value = ST_CANCELLED;
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return new RemoveFriendResponse { Removed = true };
                }
            }
        }

        public ListFriendsResponse ListFriends(ListFriendsRequest request)
        {
            var me = Authenticate(request.Token);

            const string Q_FRIENDS =
                @"SELECT from_user_id, to_user_id, responded_at
                  FROM dbo.FriendRequests
                  WHERE status = @Accepted
                    AND (from_user_id = @Me OR to_user_id = @Me);";

            const string Q_PENDING_IN =
                @"SELECT friend_request_id, from_user_id, to_user_id, sent_at
                  FROM dbo.FriendRequests
                  WHERE to_user_id = @Me AND status = @Pending
                  ORDER BY sent_at DESC;";

            const string Q_PENDING_OUT =
                @"SELECT friend_request_id, from_user_id, to_user_id, sent_at
                  FROM dbo.FriendRequests
                  WHERE from_user_id = @Me AND status = @Pending
                  ORDER BY sent_at DESC;";

            using (var cn = new SqlConnection(ConnectionString))
            {
                cn.Open();

                var friends = new System.Collections.Generic.List<FriendSummary>();
                using (var cmd = new SqlCommand(Q_FRIENDS, cn))
                {
                    cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                    cmd.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = ST_ACCEPTED;
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var from = rd.GetInt32(0);
                            var to = rd.GetInt32(1);
                            var since = rd.IsDBNull(2) ? DateTime.UtcNow : rd.GetDateTime(2);

                            var friendId = (from == me) ? to : from;
                            var summary = LoadFriendSummary(cn, /*tx*/ null, friendId, since);
                            friends.Add(summary);
                        }
                    }
                }

                FriendRequestSummary[] incoming;
                using (var cmd = new SqlCommand(Q_PENDING_IN, cn))
                {
                    cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                    cmd.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = ST_PENDING;
                    using (var rd = cmd.ExecuteReader())
                    {
                        var list = new System.Collections.Generic.List<FriendRequestSummary>();
                        while (rd.Read())
                        {
                            list.Add(new FriendRequestSummary
                            {
                                FriendRequestId = rd.GetInt32(0),
                                FromAccountId = rd.GetInt32(1),
                                ToAccountId = rd.GetInt32(2),
                                Message = null,
                                Status = FriendRequestStatus.Pending,
                                CreatedUtc = rd.GetDateTime(3),
                                ResolvedUtc = null
                            });
                        }
                        incoming = list.ToArray();
                    }
                }

                FriendRequestSummary[] outgoing;
                using (var cmd = new SqlCommand(Q_PENDING_OUT, cn))
                {
                    cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                    cmd.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = ST_PENDING;
                    using (var rd = cmd.ExecuteReader())
                    {
                        var list = new System.Collections.Generic.List<FriendRequestSummary>();
                        while (rd.Read())
                        {
                            list.Add(new FriendRequestSummary
                            {
                                FriendRequestId = rd.GetInt32(0),
                                FromAccountId = rd.GetInt32(1),
                                ToAccountId = rd.GetInt32(2),
                                Message = null,
                                Status = FriendRequestStatus.Pending,
                                CreatedUtc = rd.GetDateTime(3),
                                ResolvedUtc = null
                            });
                        }
                        outgoing = list.ToArray();
                    }
                }

                return new ListFriendsResponse
                {
                    Friends = friends.OrderBy(f => f.DisplayName ?? f.Username).ToArray(),
                    PendingIncoming = incoming,
                    PendingOutgoing = outgoing
                };
            }
        }

        public HeartbeatResponse PresenceHeartbeat(HeartbeatRequest request)
        {
            var me = Authenticate(request.Token);
            using (var cn = new SqlConnection(ConnectionString))
            {
                cn.Open();

                // UPDATE… si no afectó filas, INSERT
                const string Q_UPD = @"
            UPDATE dbo.UserPresence
            SET last_seen_utc = SYSUTCDATETIME(), device = @Dev
            WHERE user_id = @Me;";

                using (var cmd = new SqlCommand(Q_UPD, cn))
                {
                    cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                    cmd.Parameters.Add("@Dev", SqlDbType.NVarChar, 64).Value =
                        string.IsNullOrWhiteSpace(request.Device) ? (object)DBNull.Value : request.Device;
                    var rows = cmd.ExecuteNonQuery();

                    if (rows == 0)
                    {
                        const string Q_INS = @"
                    INSERT INTO dbo.UserPresence(user_id, last_seen_utc, device)
                    VALUES(@Me, SYSUTCDATETIME(), @Dev);";
                        using (var ins = new SqlCommand(Q_INS, cn))
                        {
                            ins.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                            ins.Parameters.Add("@Dev", SqlDbType.NVarChar, 64).Value =
                                string.IsNullOrWhiteSpace(request.Device) ? (object)DBNull.Value : request.Device;
                            ins.ExecuteNonQuery();
                        }
                    }
                }
            }
            return new HeartbeatResponse { Utc = DateTime.UtcNow };
        }

        public GetFriendsPresenceResponse GetFriendsPresence(GetFriendsPresenceRequest request)
        {
            var me = Authenticate(request.Token);

            const string SQL = @"
                DECLARE @now DATETIME2(3) = SYSUTCDATETIME();
                SELECT F.friend_id,
                       CASE WHEN P.last_seen_utc IS NOT NULL
                                 AND P.last_seen_utc >= DATEADD(SECOND, -@Window, @now)
                            THEN 1 ELSE 0 END AS is_online,
                       P.last_seen_utc
                FROM (
                    SELECT CASE WHEN fr.from_user_id = @Me THEN fr.to_user_id ELSE fr.from_user_id END AS friend_id
                    FROM dbo.FriendRequests fr
                    WHERE fr.status = @Accepted AND (fr.from_user_id = @Me OR fr.to_user_id = @Me)
                ) F
                LEFT JOIN dbo.UserPresence P ON P.user_id = F.friend_id
                ORDER BY F.friend_id;";

            var list = new System.Collections.Generic.List<FriendPresence>();

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(SQL, cn))
            {
                cn.Open();
                cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                cmd.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = ST_ACCEPTED;
                cmd.Parameters.Add("@Window", SqlDbType.Int).Value = ONLINE_WINDOW_SECONDS;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new FriendPresence
                        {
                            AccountId = rd.GetInt32(0),
                            IsOnline = rd.GetInt32(1) == 1,
                            LastSeenUtc = rd.IsDBNull(2) ? (DateTime?)null : rd.GetDateTime(2)
                        });
                    }
                }
            }

            return new GetFriendsPresenceResponse { Friends = list.ToArray() };
        }


        private FriendSummary LoadFriendSummary(SqlConnection cn, SqlTransaction tx, int friendId, DateTime sinceUtc)
        {
            const string Q = @"
                SELECT a.account_id, a.email, u.display_name, u.profile_image_url,
                       p.last_seen_utc
                FROM dbo.Accounts a
                LEFT JOIN dbo.Users u ON u.user_id = a.account_id
                LEFT JOIN dbo.UserPresence p ON p.user_id = a.account_id
                WHERE a.account_id = @Id;";

            int accountId;
            string email = string.Empty;
            string display = string.Empty;
            string avatar = null;
            DateTime? lastSeen = null;

            using (var cmd = new SqlCommand(Q, cn, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = friendId;
                using (var rd = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!rd.Read())
                    {
                        return new FriendSummary
                        {
                            AccountId = friendId,
                            Username = "user:" + friendId,
                            DisplayName = "user:" + friendId,
                            AvatarUrl = null,
                            SinceUtc = sinceUtc,
                            IsOnline = false
                        };
                    }

                    accountId = rd.GetInt32(0);
                    if (!rd.IsDBNull(1)) email = rd.GetString(1);
                    if (!rd.IsDBNull(2)) display = rd.GetString(2);
                    if (!rd.IsDBNull(3)) avatar = rd.GetString(3);
                    if (!rd.IsDBNull(4)) lastSeen = rd.GetDateTime(4);
                }
            }

            bool online = lastSeen.HasValue &&
                          lastSeen.Value >= DateTime.UtcNow.AddSeconds(-ONLINE_WINDOW_SECONDS);

            return new FriendSummary
            {
                AccountId = accountId,
                Username = string.IsNullOrWhiteSpace(email) ? ("user:" + accountId) : email,
                DisplayName = string.IsNullOrWhiteSpace(display) ? email : display,
                AvatarUrl = avatar,
                SinceUtc = sinceUtc,
                IsOnline = online
            };
        }


        public SearchAccountsResponse SearchAccounts(SearchAccountsRequest request)
        {
            var me = Authenticate(request.Token);
            var q = (request.Query ?? string.Empty).Trim();
            var max = (request.MaxResults <= 0 || request.MaxResults > 100) ? 20 : request.MaxResults;

            const string SQL = @"
                SELECT TOP(@Max)
                    a.account_id,
                    ISNULL(u.display_name, a.email) AS display_name,
                    a.email,
                    u.profile_image_url,
                    -- flags
                    CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.FriendRequests fr
                        WHERE fr.status = 1
                          AND ((fr.from_user_id = @Me AND fr.to_user_id = a.account_id)
                            OR (fr.from_user_id = a.account_id AND fr.to_user_id = @Me))
                    ) THEN 1 ELSE 0 END AS is_friend,
                    CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.FriendRequests fr
                        WHERE fr.status = 0 AND fr.from_user_id = @Me AND fr.to_user_id = a.account_id
                    ) THEN 1 ELSE 0 END AS has_outgoing,
                    CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.FriendRequests fr
                        WHERE fr.status = 0 AND fr.from_user_id = a.account_id AND fr.to_user_id = @Me
                    ) THEN 1 ELSE 0 END AS has_incoming,
                    (
                        SELECT TOP(1) fr.friend_request_id
                        FROM dbo.FriendRequests fr
                        WHERE fr.status = 0 AND fr.from_user_id = a.account_id AND fr.to_user_id = @Me
                        ORDER BY fr.sent_at DESC
                    ) AS incoming_id
                FROM dbo.Accounts a
                LEFT JOIN dbo.Users u ON u.user_id = a.account_id
                WHERE a.account_id <> @Me
                  AND (
                        a.email LIKE @Qemail
                     OR ISNULL(u.display_name,'') LIKE @Qname
                  )
                ORDER BY display_name;";

            var list = new System.Collections.Generic.List<SearchAccountItem>();

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(SQL, cn))
            {
                cn.Open();
                cmd.Parameters.Add("@Me", SqlDbType.Int).Value = me;
                cmd.Parameters.Add("@Max", SqlDbType.Int).Value = max;

                var like = "%" + q + "%";
                cmd.Parameters.Add("@Qemail", SqlDbType.NVarChar, 320).Value = like;
                cmd.Parameters.Add("@Qname", SqlDbType.NVarChar, 100).Value = like;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var item = new SearchAccountItem
                        {
                            AccountId = rd.GetInt32(0),
                            DisplayName = rd.IsDBNull(1) ? string.Empty : rd.GetString(1),
                            Email = rd.IsDBNull(2) ? string.Empty : rd.GetString(2),
                            AvatarUrl = rd.IsDBNull(3) ? null : rd.GetString(3),
                            IsFriend = rd.GetInt32(4) == 1,
                            HasPendingOutgoing = rd.GetInt32(5) == 1,
                            HasPendingIncoming = rd.GetInt32(6) == 1,
                            PendingIncomingRequestId = rd.IsDBNull(7) ? (int?)null : rd.GetInt32(7)
                        };
                        list.Add(item);
                    }
                }
            }

            return new SearchAccountsResponse { Results = list.ToArray() };
        }

        public GetAccountsByIdsResponse GetAccountsByIds(GetAccountsByIdsRequest request)
        {
            var me = Authenticate(request.Token);
            var ids = (request.AccountIds ?? new int[0]);
            if (ids.Length == 0) return new GetAccountsByIdsResponse();

            var list = new System.Collections.Generic.List<AccountMini>();

            var inParams = new System.Text.StringBuilder();
            for (int i = 0; i < ids.Length; i++)
            {
                if (i > 0) inParams.Append(",");
                inParams.Append("@p").Append(i);
            }

            string SQL =
                "SELECT a.account_id, ISNULL(u.display_name, a.email) AS display_name, a.email, u.profile_image_url " +
                "FROM dbo.Accounts a LEFT JOIN dbo.Users u ON u.user_id = a.account_id " +
                $"WHERE a.account_id IN ({inParams})";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(SQL, cn))
            {
                cn.Open();
                for (int i = 0; i < ids.Length; i++)
                    cmd.Parameters.Add("@p" + i, SqlDbType.Int).Value = ids[i];

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new AccountMini
                        {
                            AccountId = rd.GetInt32(0),
                            DisplayName = rd.IsDBNull(1) ? string.Empty : rd.GetString(1),
                            Email = rd.IsDBNull(2) ? string.Empty : rd.GetString(2),
                            AvatarUrl = rd.IsDBNull(3) ? null : rd.GetString(3)
                        });
                    }
                }
            }

            return new GetAccountsByIdsResponse { Accounts = list.ToArray() };
        }

    }
}
