using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using System.Text;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public sealed class FriendService : IFriendService
    {
        // ====== Config y límites (sin números mágicos) ======
        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private const int OnlineWindowSeconds = 75;
        private const int MaxEmailLength = 320;
        private const int MaxDisplayNameLength = 100;
        private const int DeviceMaxLength = 64;
        private const int DefaultMaxResults = 20;
        private const int MaxResultsLimit = 100;

        private enum FriendRequestState : byte
        {
            Pending = 0,
            Accepted = 1,
            Declined = 2,
            Cancelled = 3
        }

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

        private static bool IsUniqueViolation(SqlException ex) =>
            ex != null && (ex.Number == 2627 || ex.Number == 2601);

        private static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) ThrowFault("AUTH_REQUIRED", "Missing token.");
            if (!TokenCache.TryGetValue(token, out var authToken)) ThrowFault("AUTH_INVALID", "Invalid token.");
            if (authToken.ExpiresAtUtc <= DateTime.UtcNow) ThrowFault("AUTH_EXPIRED", "Token expired.");
            return authToken.UserId;
        }

        // ====== SQL ======
        private static class SqlText
        {
            public const string ExistsFriend = @"
                SELECT 1 
                FROM dbo.FriendRequests 
                WHERE ((from_user_id = @Me AND to_user_id = @Target) OR (from_user_id = @Target AND to_user_id = @Me))
                  AND status = @Accepted;";

            public const string PendingOut = @"
                SELECT friend_request_id 
                FROM dbo.FriendRequests 
                WHERE from_user_id = @Me AND to_user_id = @Target AND status = @Pending;";

            public const string PendingIn = @"
                SELECT friend_request_id 
                FROM dbo.FriendRequests 
                WHERE from_user_id = @Target AND to_user_id = @Me AND status = @Pending;";

            public const string AcceptIncoming = @"
                UPDATE dbo.FriendRequests
                SET status = @Accepted, responded_at = SYSUTCDATETIME()
                OUTPUT INSERTED.friend_request_id
                WHERE friend_request_id = @ReqId;";

            public const string InsertRequest = @"
                INSERT INTO dbo.FriendRequests (from_user_id, to_user_id, status, sent_at, responded_at)
                OUTPUT INSERTED.friend_request_id
                VALUES (@Me, @Target, @Pending, SYSUTCDATETIME(), NULL);";

            public const string ReopenRequest = @"
                UPDATE dbo.FriendRequests
                SET status = @Pending, sent_at = SYSUTCDATETIME(), responded_at = NULL
                OUTPUT INSERTED.friend_request_id
                WHERE from_user_id = @Me AND to_user_id = @Target AND status IN (@Declined, @Cancelled);";

            public const string CheckRequest = @"
                SELECT friend_request_id, from_user_id, to_user_id, status
                FROM dbo.FriendRequests
                WHERE friend_request_id = @Id;";

            public const string AcceptRequest = @"
                UPDATE dbo.FriendRequests
                SET status = @Accepted, responded_at = SYSUTCDATETIME()
                WHERE friend_request_id = @Id
                  AND to_user_id = @Me
                  AND status = @Pending;";

            public const string GetRequest = CheckRequest;

            public const string RejectRequest = @"
                UPDATE dbo.FriendRequests
                SET status = @Rejected, responded_at = SYSUTCDATETIME()
                WHERE friend_request_id = @Id
                  AND to_user_id = @Me
                  AND status = @Pending;";

            public const string CancelRequest = @"
                UPDATE dbo.FriendRequests
                SET status = @Cancelled, responded_at = SYSUTCDATETIME()
                WHERE friend_request_id = @Id
                  AND from_user_id = @Me
                  AND status = @Pending;";

            public const string LatestAccepted = @"
                SELECT TOP(1) friend_request_id
                FROM dbo.FriendRequests
                WHERE status = @Accepted
                  AND ((from_user_id = @Me AND to_user_id = @Other) OR (from_user_id = @Other AND to_user_id = @Me))
                ORDER BY responded_at DESC;";

            public const string MarkCancelled = @"
                UPDATE dbo.FriendRequests
                SET status = @Cancelled, responded_at = SYSUTCDATETIME()
                WHERE friend_request_id = @Id;";

            public const string Friends = @"
                SELECT from_user_id, to_user_id, responded_at
                FROM dbo.FriendRequests
                WHERE status = @Accepted
                  AND (from_user_id = @Me OR to_user_id = @Me);";

            public const string PendingIncoming = @"
                SELECT friend_request_id, from_user_id, to_user_id, sent_at
                FROM dbo.FriendRequests
                WHERE to_user_id = @Me AND status = @Pending
                ORDER BY sent_at DESC;";

            public const string PendingOutgoing = @"
                SELECT friend_request_id, from_user_id, to_user_id, sent_at
                FROM dbo.FriendRequests
                WHERE from_user_id = @Me AND status = @Pending
                ORDER BY sent_at DESC;";

            public const string PresenceUpdate = @"
                UPDATE dbo.UserPresence
                SET last_seen_utc = SYSUTCDATETIME(), device = @Dev
                WHERE user_id = @Me;";

            public const string PresenceInsert = @"
                INSERT INTO dbo.UserPresence (user_id, last_seen_utc, device)
                VALUES (@Me, SYSUTCDATETIME(), @Dev);";

            public const string FriendsPresence = @"
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

            public const string FriendSummary = @"
                SELECT a.account_id, a.email, u.display_name, u.profile_image_url, p.last_seen_utc
                FROM dbo.Accounts a
                LEFT JOIN dbo.Users u ON u.user_id = a.account_id
                LEFT JOIN dbo.UserPresence p ON p.user_id = a.account_id
                WHERE a.account_id = @Id;";

            public const string SearchAccounts = @"
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
                  AND (a.email LIKE @Qemail OR ISNULL(u.display_name, '') LIKE @Qname)
                ORDER BY display_name;";
        }

        // ====== Logger provisional (consola) ======
        private static class AppLogger
        {
            public static void Info(string message) =>
                Console.WriteLine($"[INFO ] {DateTime.UtcNow:o} {message}");

            public static void Warn(Exception ex, string message) =>
                Console.WriteLine($"[WARN ] {DateTime.UtcNow:o} {message} | {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

            public static void Error(Exception ex, string message) =>
                Console.WriteLine($"[ERROR] {DateTime.UtcNow:o} {message} | {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }

        // ====== API ======
        public SendFriendRequestResponse SendFriendRequest(SendFriendRequestRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request is null.");

            var myAccountId = Authenticate(request.Token);
            var targetAccountId = request.TargetAccountId;

            if (myAccountId == targetAccountId) ThrowFault("FR_SELF", "No puedes enviarte solicitud a ti mismo.");

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        // ¿Ya son amigos?
                        using (var command = new SqlCommand(SqlText.ExistsFriend, connection, transaction))
                        {
                            command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                            command.Parameters.Add("@Target", SqlDbType.Int).Value = targetAccountId;
                            command.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;

                            var scalarValue = command.ExecuteScalar();
                            if (scalarValue != null) ThrowFault("FR_ALREADY", "Ya existe una amistad entre estas cuentas.");
                        }

                        // ¿Ya tengo una saliente pendiente?
                        int? existingOutgoingId = null;
                        using (var command = new SqlCommand(SqlText.PendingOut, connection, transaction))
                        {
                            command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                            command.Parameters.Add("@Target", SqlDbType.Int).Value = targetAccountId;
                            command.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

                            var scalarValue = command.ExecuteScalar();
                            if (scalarValue != null) existingOutgoingId = Convert.ToInt32(scalarValue);
                        }

                        if (existingOutgoingId.HasValue)
                        {
                            transaction.Commit();
                            return new SendFriendRequestResponse
                            {
                                FriendRequestId = existingOutgoingId.Value,
                                Status = (FriendRequestStatus)FriendRequestState.Pending
                            };
                        }

                        // ¿El otro ya me envió una y está pendiente? -> aceptar
                        int? incomingId = null;
                        using (var command = new SqlCommand(SqlText.PendingIn, connection, transaction))
                        {
                            command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                            command.Parameters.Add("@Target", SqlDbType.Int).Value = targetAccountId;
                            command.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

                            var scalarValue = command.ExecuteScalar();
                            if (scalarValue != null) incomingId = Convert.ToInt32(scalarValue);
                        }

                        if (incomingId.HasValue)
                        {
                            int acceptedId;
                            using (var command = new SqlCommand(SqlText.AcceptIncoming, connection, transaction))
                            {
                                command.Parameters.Add("@ReqId", SqlDbType.Int).Value = incomingId.Value;
                                command.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;
                                acceptedId = Convert.ToInt32(command.ExecuteScalar());
                            }

                            transaction.Commit();
                            return new SendFriendRequestResponse
                            {
                                FriendRequestId = acceptedId,
                                Status = (FriendRequestStatus)FriendRequestState.Accepted
                            };
                        }

                        // Crear solicitud; si choca por unique, reabrir
                        try
                        {
                            int newId;
                            using (var command = new SqlCommand(SqlText.InsertRequest, connection, transaction))
                            {
                                command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                                command.Parameters.Add("@Target", SqlDbType.Int).Value = targetAccountId;
                                command.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;
                                newId = Convert.ToInt32(command.ExecuteScalar());
                            }

                            transaction.Commit();
                            return new SendFriendRequestResponse
                            {
                                FriendRequestId = newId,
                                Status = (FriendRequestStatus)FriendRequestState.Pending
                            };
                        }
                        catch (SqlException ex) when (IsUniqueViolation(ex))
                        {
                            int reopenedId;
                            using (var command = new SqlCommand(SqlText.ReopenRequest, connection, transaction))
                            {
                                command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                                command.Parameters.Add("@Target", SqlDbType.Int).Value = targetAccountId;
                                command.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;
                                command.Parameters.Add("@Declined", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Declined;
                                command.Parameters.Add("@Cancelled", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Cancelled;

                                var scalarValue = command.ExecuteScalar();
                                if (scalarValue == null) ThrowFault("FR_RACE", "No se pudo crear ni reabrir la solicitud (carrera).");
                                reopenedId = Convert.ToInt32(scalarValue);
                            }

                            transaction.Commit();
                            return new SendFriendRequestResponse
                            {
                                FriendRequestId = reopenedId,
                                Status = (FriendRequestStatus)FriendRequestState.Pending
                            };
                        }
                    }
                }
            }
            catch (FaultException<ServiceFault> ex) { AppLogger.Warn(ex, "Business fault at SendFriendRequest"); throw; }
            catch (SqlException ex) { AppLogger.Error(ex, "Database error at SendFriendRequest"); throw; }
            catch (Exception ex) { AppLogger.Error(ex, "Unexpected error at SendFriendRequest"); throw; }
        }

        public AcceptFriendRequestResponse AcceptFriendRequest(AcceptFriendRequestRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request is null.");

            var myAccountId = Authenticate(request.Token);

            try
            {
                int fromAccountId = -1, toAccountId = -1;
                byte currentStatus = (byte)FriendRequestState.Pending;

                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand(SqlText.CheckRequest, connection))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = request.FriendRequestId;

                        using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (!reader.Read()) ThrowFault("FR_NOT_FOUND", "La solicitud no existe.");

                            fromAccountId = reader.GetInt32(1);
                            toAccountId = reader.GetInt32(2);
                            currentStatus = reader.GetByte(3);
                        }
                    }

                    if (toAccountId != myAccountId) ThrowFault("FORBIDDEN", "No puedes aceptar esta solicitud.");
                    if (currentStatus != (byte)FriendRequestState.Pending)
                        ThrowFault("FR_NOT_PENDING", "La solicitud ya fue procesada.");

                    using (var command = new SqlCommand(SqlText.AcceptRequest, connection))
                    {
                        command.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;
                        command.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = request.FriendRequestId;
                        command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;

                        var affectedRows = command.ExecuteNonQuery();
                        if (affectedRows == 0) ThrowFault("FR_RACE", "El estado cambió. Refresca.");
                    }
                }

                return new AcceptFriendRequestResponse
                {
                    NewFriend = new FriendSummary
                    {
                        AccountId = fromAccountId,
                        DisplayName = null,
                        AvatarUrl = null,
                        SinceUtc = DateTime.UtcNow,
                        IsOnline = false
                    }
                };
            }
            catch (FaultException<ServiceFault> ex) { AppLogger.Warn(ex, "Business fault at AcceptFriendRequest"); throw; }
            catch (SqlException ex) { AppLogger.Error(ex, "Database error at AcceptFriendRequest"); throw; }
            catch (Exception ex) { AppLogger.Error(ex, "Unexpected error at AcceptFriendRequest"); throw; }
        }

        public RejectFriendRequestResponse RejectFriendRequest(RejectFriendRequestRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request is null.");

            var myAccountId = Authenticate(request.Token);

            try
            {
                int fromAccountId, toAccountId;
                byte currentStatus;

                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        using (var command = new SqlCommand(SqlText.GetRequest, connection, transaction))
                        {
                            command.Parameters.Add("@Id", SqlDbType.Int).Value = request.FriendRequestId;
                            using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                            {
                                if (!reader.Read()) ThrowFault("FR_NOT_FOUND", "La solicitud no existe.");
                                fromAccountId = reader.GetInt32(1);
                                toAccountId = reader.GetInt32(2);
                                currentStatus = reader.GetByte(3);
                            }
                        }

                        if (currentStatus != (byte)FriendRequestState.Pending)
                            ThrowFault("FR_NOT_PENDING", "La solicitud ya fue resuelta.");

                        if (toAccountId == myAccountId)
                        {
                            // Rechazar (quien recibe)
                            using (var command = new SqlCommand(SqlText.RejectRequest, connection, transaction))
                            {
                                command.Parameters.Add("@Id", SqlDbType.Int).Value = request.FriendRequestId;
                                command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                                // SQL usa @Rejected; mapeamos al valor "Declined".
                                command.Parameters.Add("@Rejected", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Declined;
                                command.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

                                var affectedRows = command.ExecuteNonQuery();
                                if (affectedRows == 0) ThrowFault("FR_RACE", "El estado cambió. Refresca.");
                            }

                            transaction.Commit();
                            return new RejectFriendRequestResponse { Status = FriendRequestStatus.Rejected };
                        }
                        else if (fromAccountId == myAccountId)
                        {
                            // Cancelar (quien envió)
                            using (var command = new SqlCommand(SqlText.CancelRequest, connection, transaction))
                            {
                                command.Parameters.Add("@Id", SqlDbType.Int).Value = request.FriendRequestId;
                                command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                                command.Parameters.Add("@Cancelled", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Cancelled;
                                command.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

                                var affectedRows = command.ExecuteNonQuery();
                                if (affectedRows == 0) ThrowFault("FR_RACE", "El estado cambió. Refresca.");
                            }

                            transaction.Commit();
                            return new RejectFriendRequestResponse { Status = FriendRequestStatus.Cancelled };
                        }
                        else
                        {
                            ThrowFault("FR_FORBIDDEN", "No estás involucrado en esta solicitud.");
                            return new RejectFriendRequestResponse(); // inalcanzable
                        }
                    }
                }
            }
            catch (FaultException<ServiceFault> ex) { AppLogger.Warn(ex, "Business fault at RejectFriendRequest"); throw; }
            catch (SqlException ex) { AppLogger.Error(ex, "Database error at RejectFriendRequest"); throw; }
            catch (Exception ex) { AppLogger.Error(ex, "Unexpected error at RejectFriendRequest"); throw; }
        }

        public RemoveFriendResponse RemoveFriend(RemoveFriendRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request is null.");

            var myAccountId = Authenticate(request.Token);
            var otherAccountId = request.FriendAccountId;

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        int? friendRequestId = null;
                        using (var command = new SqlCommand(SqlText.LatestAccepted, connection, transaction))
                        {
                            command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                            command.Parameters.Add("@Other", SqlDbType.Int).Value = otherAccountId;
                            command.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;

                            var scalarValue = command.ExecuteScalar();
                            if (scalarValue != null) friendRequestId = Convert.ToInt32(scalarValue);
                        }

                        if (!friendRequestId.HasValue)
                        {
                            transaction.Commit();
                            return new RemoveFriendResponse { Removed = false };
                        }

                        using (var command = new SqlCommand(SqlText.MarkCancelled, connection, transaction))
                        {
                            command.Parameters.Add("@Id", SqlDbType.Int).Value = friendRequestId.Value;
                            command.Parameters.Add("@Cancelled", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Cancelled;
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return new RemoveFriendResponse { Removed = true };
                    }
                }
            }
            catch (FaultException<ServiceFault> ex) { AppLogger.Warn(ex, "Business fault at RemoveFriend"); throw; }
            catch (SqlException ex) { AppLogger.Error(ex, "Database error at RemoveFriend"); throw; }
            catch (Exception ex) { AppLogger.Error(ex, "Unexpected error at RemoveFriend"); throw; }
        }

        public ListFriendsResponse ListFriends(ListFriendsRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request is null.");

            var myAccountId = Authenticate(request.Token);

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();

                    var friends = new System.Collections.Generic.List<FriendSummary>();
                    using (var command = new SqlCommand(SqlText.Friends, connection))
                    {
                        command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                        command.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var fromId = reader.GetInt32(0);
                                var toId = reader.GetInt32(1);
                                var since = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2);

                                var friendId = (fromId == myAccountId) ? toId : fromId;
                                var summary = LoadFriendSummary(connection, /*transaction*/ null, friendId, since);
                                friends.Add(summary);
                            }
                        }
                    }

                    FriendRequestSummary[] incoming;
                    using (var command = new SqlCommand(SqlText.PendingIncoming, connection))
                    {
                        command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                        command.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

                        using (var reader = command.ExecuteReader())
                        {
                            var list = new System.Collections.Generic.List<FriendRequestSummary>();
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
                            incoming = list.ToArray();
                        }
                    }

                    FriendRequestSummary[] outgoing;
                    using (var command = new SqlCommand(SqlText.PendingOutgoing, connection))
                    {
                        command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                        command.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

                        using (var reader = command.ExecuteReader())
                        {
                            var list = new System.Collections.Generic.List<FriendRequestSummary>();
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
            catch (FaultException<ServiceFault> ex) { AppLogger.Warn(ex, "Business fault at ListFriends"); throw; }
            catch (SqlException ex) { AppLogger.Error(ex, "Database error at ListFriends"); throw; }
            catch (Exception ex) { AppLogger.Error(ex, "Unexpected error at ListFriends"); throw; }
        }

        public HeartbeatResponse PresenceHeartbeat(HeartbeatRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request is null.");

            var myAccountId = Authenticate(request.Token);

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand(SqlText.PresenceUpdate, connection))
                    {
                        command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                        command.Parameters.Add("@Dev", SqlDbType.NVarChar, DeviceMaxLength).Value =
                            string.IsNullOrWhiteSpace(request.Device) ? (object)DBNull.Value : request.Device;

                        var affectedRows = command.ExecuteNonQuery();
                        if (affectedRows == 0)
                        {
                            using (var insertCommand = new SqlCommand(SqlText.PresenceInsert, connection))
                            {
                                insertCommand.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                                insertCommand.Parameters.Add("@Dev", SqlDbType.NVarChar, DeviceMaxLength).Value =
                                    string.IsNullOrWhiteSpace(request.Device) ? (object)DBNull.Value : request.Device;
                                insertCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }

                return new HeartbeatResponse { Utc = DateTime.UtcNow };
            }
            catch (FaultException<ServiceFault> ex) { AppLogger.Warn(ex, "Business fault at PresenceHeartbeat"); throw; }
            catch (SqlException ex) { AppLogger.Error(ex, "Database error at PresenceHeartbeat"); throw; }
            catch (Exception ex) { AppLogger.Error(ex, "Unexpected error at PresenceHeartbeat"); throw; }
        }

        public GetFriendsPresenceResponse GetFriendsPresence(GetFriendsPresenceRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request is null.");

            var myAccountId = Authenticate(request.Token);

            try
            {
                var list = new System.Collections.Generic.List<FriendPresence>();

                using (var connection = new SqlConnection(ConnectionString))
                using (var command = new SqlCommand(SqlText.FriendsPresence, connection))
                {
                    connection.Open();
                    command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                    command.Parameters.Add("@Accepted", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;
                    command.Parameters.Add("@Window", SqlDbType.Int).Value = OnlineWindowSeconds;

                    using (var reader = command.ExecuteReader())
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

                return new GetFriendsPresenceResponse { Friends = list.ToArray() };
            }
            catch (FaultException<ServiceFault> ex) { AppLogger.Warn(ex, "Business fault at GetFriendsPresence"); throw; }
            catch (SqlException ex) { AppLogger.Error(ex, "Database error at GetFriendsPresence"); throw; }
            catch (Exception ex) { AppLogger.Error(ex, "Unexpected error at GetFriendsPresence"); throw; }
        }

        public SearchAccountsResponse SearchAccounts(SearchAccountsRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request is null.");

            var myAccountId = Authenticate(request.Token);
            var query = (request.Query ?? string.Empty).Trim();
            var maxResults = (request.MaxResults <= 0 || request.MaxResults > MaxResultsLimit) ? DefaultMaxResults : request.MaxResults;

            try
            {
                var list = new System.Collections.Generic.List<SearchAccountItem>();

                using (var connection = new SqlConnection(ConnectionString))
                using (var command = new SqlCommand(SqlText.SearchAccounts, connection))
                {
                    connection.Open();
                    command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;
                    command.Parameters.Add("@Max", SqlDbType.Int).Value = maxResults;

                    var like = $"%{query}%";
                    command.Parameters.Add("@Qemail", SqlDbType.NVarChar, MaxEmailLength).Value = like;
                    command.Parameters.Add("@Qname", SqlDbType.NVarChar, MaxDisplayNameLength).Value = like;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new SearchAccountItem
                            {
                                AccountId = reader.GetInt32(0),
                                DisplayName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                AvatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                                IsFriend = reader.GetInt32(4) == 1,
                                HasPendingOutgoing = reader.GetInt32(5) == 1,
                                HasPendingIncoming = reader.GetInt32(6) == 1,
                                PendingIncomingRequestId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7)
                            });
                        }
                    }
                }

                return new SearchAccountsResponse { Results = list.ToArray() };
            }
            catch (FaultException<ServiceFault> ex) { AppLogger.Warn(ex, "Business fault at SearchAccounts"); throw; }
            catch (SqlException ex) { AppLogger.Error(ex, "Database error at SearchAccounts"); throw; }
            catch (Exception ex) { AppLogger.Error(ex, "Unexpected error at SearchAccounts"); throw; }
        }

        public GetAccountsByIdsResponse GetAccountsByIds(GetAccountsByIdsRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request is null.");

            var myAccountId = Authenticate(request.Token);
            var ids = request.AccountIds ?? Array.Empty<int>();
            if (ids.Length == 0) return new GetAccountsByIdsResponse();

            try
            {
                var list = new System.Collections.Generic.List<AccountMini>();

                var inParamsBuilder = new StringBuilder();
                for (int i = 0; i < ids.Length; i++)
                {
                    if (i > 0) inParamsBuilder.Append(",");
                    inParamsBuilder.Append("@p").Append(i);
                }

                var sqlQuery =
                    "SELECT a.account_id, ISNULL(u.display_name, a.email) AS display_name, a.email, u.profile_image_url " +
                    "FROM dbo.Accounts a LEFT JOIN dbo.Users u ON u.user_id = a.account_id " +
                    $"WHERE a.account_id IN ({inParamsBuilder}) AND a.account_id <> @Me";

                using (var connection = new SqlConnection(ConnectionString))
                using (var command = new SqlCommand(sqlQuery, connection))
                {
                    connection.Open();
                    command.Parameters.Add("@Me", SqlDbType.Int).Value = myAccountId;

                    for (int i = 0; i < ids.Length; i++)
                        command.Parameters.Add("@p" + i, SqlDbType.Int).Value = ids[i];

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new AccountMini
                            {
                                AccountId = reader.GetInt32(0),
                                DisplayName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                AvatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3)
                            });
                        }
                    }
                }

                return new GetAccountsByIdsResponse { Accounts = list.ToArray() };
            }
            catch (FaultException<ServiceFault> ex) { AppLogger.Warn(ex, "Business fault at GetAccountsByIds"); throw; }
            catch (SqlException ex) { AppLogger.Error(ex, "Database error at GetAccountsByIds"); throw; }
            catch (Exception ex) { AppLogger.Error(ex, "Unexpected error at GetAccountsByIds"); throw; }
        }

        private FriendSummary LoadFriendSummary(SqlConnection connection, SqlTransaction transaction, int friendId, DateTime sinceUtc)
        {
            using (var command = new SqlCommand(SqlText.FriendSummary, connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = friendId;

                int accountId;
                string email = string.Empty;
                string displayName = string.Empty;
                string avatarUrl = null;
                DateTime? lastSeenUtc = null;

                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
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

                    accountId = reader.GetInt32(0);
                    if (!reader.IsDBNull(1)) email = reader.GetString(1);
                    if (!reader.IsDBNull(2)) displayName = reader.GetString(2);
                    if (!reader.IsDBNull(3)) avatarUrl = reader.GetString(3);
                    if (!reader.IsDBNull(4)) lastSeenUtc = reader.GetDateTime(4);
                }

                var isOnline = lastSeenUtc.HasValue && lastSeenUtc.Value >= DateTime.UtcNow.AddSeconds(-OnlineWindowSeconds);

                return new FriendSummary
                {
                    AccountId = accountId,
                    Username = string.IsNullOrWhiteSpace(email) ? $"user:{accountId}" : email,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                    AvatarUrl = avatarUrl,
                    SinceUtc = sinceUtc,
                    IsOnline = isOnline
                };
            }
        }
    }
}
