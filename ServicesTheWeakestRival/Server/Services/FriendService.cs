using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public sealed class FriendService : IFriendService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(FriendService));
        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private const int ONLINE_WINDOW_SECONDS = 75;
        private const int MAX_EMAIL_LENGTH = 320;
        private const int MAX_DISPLAY_NAME_LENGTH = 100;
        private const int DEVICE_MAX_LENGTH = 64;
        private const int DEFAULT_MAX_RESULTS = 20;
        private const int MAX_RESULTS_LIMIT = 100;

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        // Business error codes / messages reused muchas veces
        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_INVALID_REQUEST_MESSAGE = "Request is null.";

        private const string ERROR_RACE = "FR_RACE";
        private const string ERROR_DB = "DB_ERROR";
        private const string ERROR_UNEXPECTED = "UNEXPECTED_ERROR";

        private const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";
        private const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";
        private const string MESSAGE_STATE_CHANGED =
            "El estado cambió. Refresca.";

        // Parámetros SQL reutilizados
        private const string PARAM_ME = "@Me";
        private const string PARAM_TARGET = "@Target";
        private const string PARAM_ACCEPTED = "@Accepted";
        private const string PARAM_PENDING = "@Pending";
        private const string PARAM_DECLINED = "@Declined";
        private const string PARAM_CANCELLED = "@Cancelled";
        private const string PARAM_ID = "@Id";

        private enum FriendRequestState : byte
        {
            Pending = 0,
            Accepted = 1,
            Declined = 2,
            Cancelled = 3
        }

        private static string GetConnectionString()
        {
            var configurationString = ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (configurationString == null || string.IsNullOrWhiteSpace(configurationString.ConnectionString))
            {
                Logger.ErrorFormat("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME);

                throw ThrowTechnicalFault(
                    "CONFIG_ERROR",
                    "Configuration error. Please contact support.",
                    "FriendService.GetConnectionString",
                    new ConfigurationErrorsException(
                        string.Format("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME)));
            }

            return configurationString.ConnectionString;
        }

        private static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat("Service fault. Code='{0}', Message='{1}'", code, message);

            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        private static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            var fault = new ServiceFault
            {
                Code = code,
                Message = userMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }

        private static AvatarAppearanceDto MapAvatar(UserAvatarEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            return new AvatarAppearanceDto
            {
                BodyColor = (AvatarBodyColor)entity.BodyColor,
                PantsColor = (AvatarPantsColor)entity.PantsColor,
                HatType = (AvatarHatType)entity.HatType,
                HatColor = (AvatarHatColor)entity.HatColor,
                FaceType = (AvatarFaceType)entity.FaceType,
                UseProfilePhotoAsFace = entity.UseProfilePhoto
            };
        }


        private static bool IsUniqueViolation(SqlException ex) =>
            ex != null && (ex.Number == 2627 || ex.Number == 2601);

        private static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ThrowFault("AUTH_REQUIRED", "Missing token.");
            }

            if (!TokenCache.TryGetValue(token, out var authToken))
            {
                throw ThrowFault("AUTH_INVALID", "Invalid token.");
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault("AUTH_EXPIRED", "Token expired.");
            }

            return authToken.UserId;
        }

        public SendFriendRequestResponse SendFriendRequest(SendFriendRequestRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            var myAccountId = Authenticate(request.Token);
            var targetAccountId = request.TargetAccountId;

            if (myAccountId == targetAccountId)
            {
                throw ThrowFault("FR_SELF", "No puedes enviarte solicitud a ti mismo.");
            }

            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        using (var command = new SqlCommand(FriendSql.Text.EXISTS_FRIEND, connection, transaction))
                        {
                            command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                            command.Parameters.Add(PARAM_TARGET, SqlDbType.Int).Value = targetAccountId;
                            command.Parameters.Add(PARAM_ACCEPTED, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;

                            var scalarValue = command.ExecuteScalar();
                            if (scalarValue != null)
                            {
                                throw ThrowFault("FR_ALREADY", "Ya existe una amistad entre estas cuentas.");
                            }
                        }

                        int? existingOutgoingId = null;
                        using (var command = new SqlCommand(FriendSql.Text.PENDING_OUT, connection, transaction))
                        {
                            command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                            command.Parameters.Add(PARAM_TARGET, SqlDbType.Int).Value = targetAccountId;
                            command.Parameters.Add(PARAM_PENDING, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

                            var scalarValue = command.ExecuteScalar();
                            if (scalarValue != null)
                            {
                                existingOutgoingId = Convert.ToInt32(scalarValue);
                            }
                        }

                        if (existingOutgoingId.HasValue)
                        {
                            transaction.Commit();

                            Logger.InfoFormat(
                                "SendFriendRequest: existing outgoing request reused. Me={0}, Target={1}, RequestId={2}",
                                myAccountId,
                                targetAccountId,
                                existingOutgoingId.Value);

                            return new SendFriendRequestResponse
                            {
                                FriendRequestId = existingOutgoingId.Value,
                                Status = (FriendRequestStatus)FriendRequestState.Pending
                            };
                        }

                        int? incomingId = null;
                        using (var command = new SqlCommand(FriendSql.Text.PENDING_IN, connection, transaction))
                        {
                            command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                            command.Parameters.Add(PARAM_TARGET, SqlDbType.Int).Value = targetAccountId;
                            command.Parameters.Add(PARAM_PENDING, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

                            var scalarValue = command.ExecuteScalar();
                            if (scalarValue != null)
                            {
                                incomingId = Convert.ToInt32(scalarValue);
                            }
                        }

                        if (incomingId.HasValue)
                        {
                            int acceptedId;
                            using (var command = new SqlCommand(FriendSql.Text.ACCEPT_INCOMING, connection, transaction))
                            {
                                command.Parameters.Add("@ReqId", SqlDbType.Int).Value = incomingId.Value;
                                command.Parameters.Add(PARAM_ACCEPTED, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;
                                acceptedId = Convert.ToInt32(command.ExecuteScalar());
                            }

                            transaction.Commit();

                            Logger.InfoFormat(
                                "SendFriendRequest: converted incoming request to accepted. Me={0}, Target={1}, RequestId={2}",
                                myAccountId,
                                targetAccountId,
                                acceptedId);

                            return new SendFriendRequestResponse
                            {
                                FriendRequestId = acceptedId,
                                Status = (FriendRequestStatus)FriendRequestState.Accepted
                            };
                        }

                        try
                        {
                            int newId;
                            using (var command = new SqlCommand(FriendSql.Text.INSERT_REQUEST, connection, transaction))
                            {
                                command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                                command.Parameters.Add(PARAM_TARGET, SqlDbType.Int).Value = targetAccountId;
                                command.Parameters.Add(PARAM_PENDING, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;
                                newId = Convert.ToInt32(command.ExecuteScalar());
                            }

                            transaction.Commit();

                            Logger.InfoFormat(
                                "SendFriendRequest: new request created. Me={0}, Target={1}, RequestId={2}",
                                myAccountId,
                                targetAccountId,
                                newId);

                            return new SendFriendRequestResponse
                            {
                                FriendRequestId = newId,
                                Status = (FriendRequestStatus)FriendRequestState.Pending
                            };
                        }
                        catch (SqlException ex) when (IsUniqueViolation(ex))
                        {
                            int reopenedId;
                            using (var command = new SqlCommand(FriendSql.Text.REOPEN_REQUEST, connection, transaction))
                            {
                                command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                                command.Parameters.Add(PARAM_TARGET, SqlDbType.Int).Value = targetAccountId;
                                command.Parameters.Add(PARAM_PENDING, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;
                                command.Parameters.Add(PARAM_DECLINED, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Declined;
                                command.Parameters.Add(PARAM_CANCELLED, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Cancelled;

                                var scalarValue = command.ExecuteScalar();
                                if (scalarValue == null)
                                {
                                    Logger.WarnFormat(
                                        "SendFriendRequest: unique violation but REOPEN_REQUEST returned null. Me={0}, Target={1}",
                                        myAccountId,
                                        targetAccountId);

                                    throw ThrowFault(
                                        ERROR_RACE,
                                        "No se pudo crear ni reabrir la solicitud (carrera).");
                                }

                                reopenedId = Convert.ToInt32(scalarValue);
                            }

                            transaction.Commit();

                            Logger.InfoFormat(
                                "SendFriendRequest: duplicate handled by reopening request. Me={0}, Target={1}, RequestId={2}",
                                myAccountId,
                                targetAccountId,
                                reopenedId);

                            return new SendFriendRequestResponse
                            {
                                FriendRequestId = reopenedId,
                                Status = (FriendRequestStatus)FriendRequestState.Pending
                            };
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at SendFriendRequest.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at SendFriendRequest.",
                    ex);
            }
        }

        public AcceptFriendRequestResponse AcceptFriendRequest(AcceptFriendRequestRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            var myAccountId = Authenticate(request.Token);

            try
            {
                int fromAccountId;
                int toAccountId;
                byte currentStatus;

                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();

                    using (var command = new SqlCommand(FriendSql.Text.CHECK_REQUEST, connection))
                    {
                        command.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = request.FriendRequestId;

                        using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (!reader.Read())
                            {
                                throw ThrowFault("FR_NOT_FOUND", "La solicitud no existe.");
                            }

                            fromAccountId = reader.GetInt32(1);
                            toAccountId = reader.GetInt32(2);
                            currentStatus = reader.GetByte(3);
                        }
                    }

                    if (toAccountId != myAccountId)
                    {
                        throw ThrowFault("FORBIDDEN", "No puedes aceptar esta solicitud.");
                    }

                    if (currentStatus != (byte)FriendRequestState.Pending)
                    {
                        throw ThrowFault("FR_NOT_PENDING", "La solicitud ya fue procesada.");
                    }

                    using (var command = new SqlCommand(FriendSql.Text.ACCEPT_REQUEST, connection))
                    {
                        command.Parameters.Add(PARAM_ACCEPTED, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;
                        command.Parameters.Add(PARAM_PENDING, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;
                        command.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = request.FriendRequestId;
                        command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;

                        var affectedRows = command.ExecuteNonQuery();
                        if (affectedRows == 0)
                        {
                            throw ThrowFault(ERROR_RACE, MESSAGE_STATE_CHANGED);
                        }
                    }
                }

                Logger.InfoFormat(
                    "AcceptFriendRequest: request accepted. RequestId={0}, Me={1}, From={2}",
                    request.FriendRequestId,
                    myAccountId,
                    fromAccountId);

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
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at AcceptFriendRequest.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at AcceptFriendRequest.",
                    ex);
            }
        }

        public RejectFriendRequestResponse RejectFriendRequest(RejectFriendRequestRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            var myAccountId = Authenticate(request.Token);

            try
            {
                int fromAccountId;
                int toAccountId;
                byte currentStatus;

                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        using (var command = new SqlCommand(FriendSql.Text.GET_REQUEST, connection, transaction))
                        {
                            command.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = request.FriendRequestId;
                            using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                            {
                                if (!reader.Read())
                                {
                                    throw ThrowFault("FR_NOT_FOUND", "La solicitud no existe.");
                                }

                                fromAccountId = reader.GetInt32(1);
                                toAccountId = reader.GetInt32(2);
                                currentStatus = reader.GetByte(3);
                            }
                        }

                        if (currentStatus != (byte)FriendRequestState.Pending)
                        {
                            throw ThrowFault("FR_NOT_PENDING", "La solicitud ya fue resuelta.");
                        }

                        if (toAccountId == myAccountId)
                        {
                            using (var command = new SqlCommand(FriendSql.Text.REJECT_REQUEST, connection, transaction))
                            {
                                command.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = request.FriendRequestId;
                                command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                                command.Parameters.Add("@Rejected", SqlDbType.TinyInt).Value = (byte)FriendRequestState.Declined;
                                command.Parameters.Add(PARAM_PENDING, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

                                var affectedRows = command.ExecuteNonQuery();
                                if (affectedRows == 0)
                                {
                                    throw ThrowFault(ERROR_RACE, MESSAGE_STATE_CHANGED);
                                }
                            }

                            transaction.Commit();

                            Logger.InfoFormat(
                                "RejectFriendRequest: request rejected. RequestId={0}, Me={1}, From={2}",
                                request.FriendRequestId,
                                myAccountId,
                                fromAccountId);

                            return new RejectFriendRequestResponse
                            {
                                Status = FriendRequestStatus.Rejected
                            };
                        }

                        if (fromAccountId == myAccountId)
                        {
                            using (var command = new SqlCommand(FriendSql.Text.CANCEL_REQUEST, connection, transaction))
                            {
                                command.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = request.FriendRequestId;
                                command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                                command.Parameters.Add(PARAM_CANCELLED, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Cancelled;
                                command.Parameters.Add(PARAM_PENDING, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

                                var affectedRows = command.ExecuteNonQuery();
                                if (affectedRows == 0)
                                {
                                    throw ThrowFault(ERROR_RACE, MESSAGE_STATE_CHANGED);
                                }
                            }

                            transaction.Commit();

                            Logger.InfoFormat(
                                "RejectFriendRequest: request cancelled. RequestId={0}, Me={1}, To={2}",
                                request.FriendRequestId,
                                myAccountId,
                                toAccountId);

                            return new RejectFriendRequestResponse
                            {
                                Status = FriendRequestStatus.Cancelled
                            };
                        }

                        throw ThrowFault("FR_FORBIDDEN", "No estás involucrado en esta solicitud.");
                    }
                }
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at RejectFriendRequest.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at RejectFriendRequest.",
                    ex);
            }
        }

        public RemoveFriendResponse RemoveFriend(RemoveFriendRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            var myAccountId = Authenticate(request.Token);
            var otherAccountId = request.FriendAccountId;

            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        int? friendRequestId = null;
                        using (var command = new SqlCommand(FriendSql.Text.LATEST_ACCEPTED, connection, transaction))
                        {
                            command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                            command.Parameters.Add("@Other", SqlDbType.Int).Value = otherAccountId;
                            command.Parameters.Add(PARAM_ACCEPTED, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;

                            var scalarValue = command.ExecuteScalar();
                            if (scalarValue != null)
                            {
                                friendRequestId = Convert.ToInt32(scalarValue);
                            }
                        }

                        if (!friendRequestId.HasValue)
                        {
                            transaction.Commit();

                            Logger.InfoFormat(
                                "RemoveFriend: no friendship found. Me={0}, Other={1}",
                                myAccountId,
                                otherAccountId);

                            return new RemoveFriendResponse
                            {
                                Removed = false
                            };
                        }

                        using (var command = new SqlCommand(FriendSql.Text.MARK_CANCELLED, connection, transaction))
                        {
                            command.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = friendRequestId.Value;
                            command.Parameters.Add(PARAM_CANCELLED, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Cancelled;
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();

                        Logger.InfoFormat(
                            "RemoveFriend: friendship marked as cancelled. Me={0}, Other={1}, RequestId={2}",
                            myAccountId,
                            otherAccountId,
                            friendRequestId.Value);

                        return new RemoveFriendResponse
                        {
                            Removed = true
                        };
                    }
                }
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at RemoveFriend.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at RemoveFriend.",
                    ex);
            }
        }

        public ListFriendsResponse ListFriends(ListFriendsRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            var myAccountId = Authenticate(request.Token);

            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();

                    var friends = new System.Collections.Generic.List<FriendSummary>();
                    using (var command = new SqlCommand(FriendSql.Text.FRIENDS, connection))
                    {
                        command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                        command.Parameters.Add(PARAM_ACCEPTED, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var fromId = reader.GetInt32(0);
                                var toId = reader.GetInt32(1);
                                var since = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2);

                                var friendId = (fromId == myAccountId) ? toId : fromId;
                                var summary = LoadFriendSummary(connection, null, friendId, since);
                                friends.Add(summary);
                            }
                        }
                    }

                    FriendRequestSummary[] incoming;
                    using (var command = new SqlCommand(FriendSql.Text.PENDING_INCOMING, connection))
                    {
                        command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                        command.Parameters.Add(PARAM_PENDING, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

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
                    using (var command = new SqlCommand(FriendSql.Text.PENDING_OUTGOING, connection))
                    {
                        command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                        command.Parameters.Add(PARAM_PENDING, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Pending;

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

                    var response = new ListFriendsResponse
                    {
                        Friends = friends.OrderBy(f => f.DisplayName ?? f.Username).ToArray(),
                        PendingIncoming = incoming,
                        PendingOutgoing = outgoing
                    };

                    Logger.InfoFormat(
                        "ListFriends: Me={0}, Friends={1}, Incoming={2}, Outgoing={3}",
                        myAccountId,
                        response.Friends.Length,
                        response.PendingIncoming.Length,
                        response.PendingOutgoing.Length);

                    return response;
                }
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at ListFriends.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at ListFriends.",
                    ex);
            }
        }

        public HeartbeatResponse PresenceHeartbeat(HeartbeatRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            var myAccountId = Authenticate(request.Token);

            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();

                    using (var command = new SqlCommand(FriendSql.Text.PRESENCE_UPDATE, connection))
                    {
                        command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                        command.Parameters.Add("@Dev", SqlDbType.NVarChar, DEVICE_MAX_LENGTH).Value =
                            string.IsNullOrWhiteSpace(request.Device) ? (object)DBNull.Value : request.Device;

                        var affectedRows = command.ExecuteNonQuery();
                        if (affectedRows == 0)
                        {
                            using (var insertCommand = new SqlCommand(FriendSql.Text.PRESENCE_INSERT, connection))
                            {
                                insertCommand.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                                insertCommand.Parameters.Add("@Dev", SqlDbType.NVarChar, DEVICE_MAX_LENGTH).Value =
                                    string.IsNullOrWhiteSpace(request.Device) ? (object)DBNull.Value : request.Device;
                                insertCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }

                var utcNow = DateTime.UtcNow;

                Logger.DebugFormat(
                    "PresenceHeartbeat: updated. Me={0}, Utc={1}",
                    myAccountId,
                    utcNow.ToString("o"));

                return new HeartbeatResponse
                {
                    Utc = utcNow
                };
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at PresenceHeartbeat.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at PresenceHeartbeat.",
                    ex);
            }
        }

        public GetFriendsPresenceResponse GetFriendsPresence(GetFriendsPresenceRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            var myAccountId = Authenticate(request.Token);

            try
            {
                var list = new System.Collections.Generic.List<FriendPresence>();

                using (var connection = new SqlConnection(GetConnectionString()))
                using (var command = new SqlCommand(FriendSql.Text.FRIENDS_PRESENCE, connection))
                {
                    connection.Open();
                    command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                    command.Parameters.Add(PARAM_ACCEPTED, SqlDbType.TinyInt).Value = (byte)FriendRequestState.Accepted;
                    command.Parameters.Add("@Window", SqlDbType.Int).Value = ONLINE_WINDOW_SECONDS;

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

                Logger.DebugFormat(
                    "GetFriendsPresence: Me={0}, Count={1}",
                    myAccountId,
                    list.Count);

                return new GetFriendsPresenceResponse
                {
                    Friends = list.ToArray()
                };
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at GetFriendsPresence.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at GetFriendsPresence.",
                    ex);
            }
        }

        public SearchAccountsResponse SearchAccounts(SearchAccountsRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            var myAccountId = Authenticate(request.Token);
            var query = (request.Query ?? string.Empty).Trim();
            var maxResults = (request.MaxResults <= 0 || request.MaxResults > MAX_RESULTS_LIMIT)
                ? DEFAULT_MAX_RESULTS
                : request.MaxResults;

            try
            {
                var list = new System.Collections.Generic.List<SearchAccountItem>();

                using (var connection = new SqlConnection(GetConnectionString()))
                using (var command = new SqlCommand(FriendSql.Text.SEARCH_ACCOUNTS, connection))
                {
                    connection.Open();
                    command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;
                    command.Parameters.Add("@Max", SqlDbType.Int).Value = maxResults;

                    var like = "%" + query + "%";
                    command.Parameters.Add("@Qemail", SqlDbType.NVarChar, MAX_EMAIL_LENGTH).Value = like;
                    command.Parameters.Add("@Qname", SqlDbType.NVarChar, MAX_DISPLAY_NAME_LENGTH).Value = like;

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

                Logger.InfoFormat(
                    "SearchAccounts: Me={0}, Query='{1}', Results={2}, MaxResults={3}",
                    myAccountId,
                    query,
                    list.Count,
                    maxResults);

                return new SearchAccountsResponse
                {
                    Results = list.ToArray()
                };
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at SearchAccounts.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at SearchAccounts.",
                    ex);
            }
        }

        public GetAccountsByIdsResponse GetAccountsByIds(GetAccountsByIdsRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            var myAccountId = Authenticate(request.Token);
            var ids = request.AccountIds ?? Array.Empty<int>();
            if (ids.Length == 0)
            {
                return new GetAccountsByIdsResponse();
            }

            try
            {
                var list = new System.Collections.Generic.List<AccountMini>();

                var avatarSql = new UserAvatarSql(GetConnectionString());

                var sqlQuery = FriendSql.BuildGetAccountsByIdsQuery(ids.Length);

                using (var connection = new SqlConnection(GetConnectionString()))
                using (var command = new SqlCommand(sqlQuery, connection))
                {
                    connection.Open();
                    command.Parameters.Add(PARAM_ME, SqlDbType.Int).Value = myAccountId;

                    for (var i = 0; i < ids.Length; i++)
                    {
                        command.Parameters.Add("@p" + i, SqlDbType.Int).Value = ids[i];
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var accountId = reader.GetInt32(0);
                            var displayName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                            var email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                            var avatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3);

                            UserAvatarEntity avatarEntity = avatarSql.GetByUserId(accountId);

                            AvatarAppearanceDto avatar = MapAvatar(avatarEntity);

                            list.Add(new AccountMini
                            {
                                AccountId = accountId,
                                DisplayName = displayName,
                                Email = email,
                                AvatarUrl = avatarUrl,
                                Avatar = avatar
                            });
                        }
                    }
                }

                Logger.DebugFormat(
                    "GetAccountsByIds: Me={0}, Requested={1}, Found={2}",
                    myAccountId,
                    ids.Length,
                    list.Count);

                return new GetAccountsByIdsResponse
                {
                    Accounts = list.ToArray()
                };
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at GetAccountsByIds.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at GetAccountsByIds.",
                    ex);
            }
        }

        private FriendSummary LoadFriendSummary(SqlConnection connection, SqlTransaction transaction, int friendId, DateTime sinceUtc)
        {
            using (var command = new SqlCommand(FriendSql.Text.FRIEND_SUMMARY, connection, transaction))
            {
                command.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = friendId;

                int accountId;
                string email = string.Empty;
                string displayName = string.Empty;
                string avatarUrl = null;
                DateTime? lastSeenUtc = null;

                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        Logger.WarnFormat(
                            "LoadFriendSummary: no data for FriendId={0}. Returning fallback summary.",
                            friendId);

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
                    if (!reader.IsDBNull(1))
                    {
                        email = reader.GetString(1);
                    }

                    if (!reader.IsDBNull(2))
                    {
                        displayName = reader.GetString(2);
                    }

                    if (!reader.IsDBNull(3))
                    {
                        avatarUrl = reader.GetString(3);
                    }

                    if (!reader.IsDBNull(4))
                    {
                        lastSeenUtc = reader.GetDateTime(4);
                    }
                }

                var isOnline = lastSeenUtc.HasValue &&
                               lastSeenUtc.Value >= DateTime.UtcNow.AddSeconds(-ONLINE_WINDOW_SECONDS);

                return new FriendSummary
                {
                    AccountId = accountId,
                    Username = string.IsNullOrWhiteSpace(email) ? "user:" + accountId : email,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                    AvatarUrl = avatarUrl,
                    SinceUtc = sinceUtc,
                    IsOnline = isOnline
                };
            }
        }
    }
}
