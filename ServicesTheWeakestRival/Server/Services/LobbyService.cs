using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using log4net;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class LobbyService : ILobbyService
    {
        private const int MAX_DISPLAY_NAME_LENGTH = 80;
        private const int MAX_PROFILE_IMAGE_URL_LENGTH = 500;
        private const int MAX_EMAIL_LENGTH = 320;
        private const int DEFAULT_MAX_PLAYERS = 8;
        private const int ACCESS_CODE_MAX_LENGTH = 12;

        private const string ERROR_NOT_FOUND = "NOT_FOUND";
        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_VALIDATION_ERROR = "VALIDATION_ERROR";
        private const string ERROR_UNAUTHORIZED = "UNAUTHORIZED";
        private const string ERROR_EMAIL_TAKEN = "EMAIL_TAKEN";

        private const string ERROR_DB = "DB_ERROR";
        private const string ERROR_UNEXPECTED = "UNEXPECTED_ERROR";

        private const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";

        private const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyService));

        private static string Connection =>
            ConfigurationManager.ConnectionStrings["TheWeakestRivalDb"].ConnectionString;

        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, ILobbyClientCallback>> CallbackBuckets
            = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, ILobbyClientCallback>>();

        private static string CurrentSessionId
        {
            get
            {
                return OperationContext.Current != null
                    ? OperationContext.Current.SessionId
                    : Guid.NewGuid().ToString("N");
            }
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


        private static void AddCallbackForLobby(Guid lobbyUid, ILobbyClientCallback cb)
        {
            var bucket = CallbackBuckets.GetOrAdd(lobbyUid, _ => new ConcurrentDictionary<string, ILobbyClientCallback>());
            bucket[CurrentSessionId] = cb;

            Logger.DebugFormat("AddCallbackForLobby: LobbyUid={0}, SessionId={1}, BucketCount={2}",
                lobbyUid,
                CurrentSessionId,
                bucket.Count);
        }

        private static void RemoveCallbackForLobby(Guid lobbyUid)
        {
            if (CallbackBuckets.TryGetValue(lobbyUid, out var bucket))
            {
                bucket.TryRemove(CurrentSessionId, out _);
                if (bucket.Count == 0)
                {
                    CallbackBuckets.TryRemove(lobbyUid, out _);
                }

                Logger.DebugFormat("RemoveCallbackForLobby: LobbyUid={0}, SessionId={1}, RemainingInBucket={2}",
                    lobbyUid,
                    CurrentSessionId,
                    bucket.Count);
            }
        }

        private static void BroadcastToLobby(Guid lobbyUid, Action<ILobbyClientCallback> send)
        {
            if (!CallbackBuckets.TryGetValue(lobbyUid, out var bucket))
            {
                Logger.DebugFormat("BroadcastToLobby: no callbacks for LobbyUid={0}", lobbyUid);
                return;
            }

            foreach (var kv in bucket)
            {
                try
                {
                    send(kv.Value);
                }
                catch (Exception ex)
                {
                    var msg = string.Format(
                        "BroadcastToLobby: callback failed. LobbyUid={0}, SessionId={1}",
                        lobbyUid,
                        kv.Key);

                    Logger.Warn(msg, ex);
                }
            }
        }

        public JoinLobbyResponse JoinLobby(JoinLobbyRequest request)
        {
            var callback = OperationContext.Current.GetCallbackChannel<ILobbyClientCallback>();
            var lobbyInfo = new LobbyInfo
            {
                LobbyId = Guid.NewGuid(),
                LobbyName = request != null ? request.LobbyName : "Lobby",
                MaxPlayers = DEFAULT_MAX_PLAYERS,
                Players = new List<PlayerSummary>(),
                AccessCode = null
            };

            Logger.InfoFormat("JoinLobby: New in-memory lobby created. LobbyId={0}, LobbyName={1}",
                lobbyInfo.LobbyId,
                lobbyInfo.LobbyName);

            AddCallbackForLobby(lobbyInfo.LobbyId, callback);
            callback.OnLobbyUpdated(lobbyInfo);
            return new JoinLobbyResponse { Lobby = lobbyInfo };
        }

        public void LeaveLobby(LeaveLobbyRequest request)
        {
            try
            {
                if (request == null
                    || string.IsNullOrWhiteSpace(request.Token)
                    || request.LobbyId == Guid.Empty)
                {
                    Logger.Debug("LeaveLobby: invalid request (null, empty token or empty LobbyId).");
                    return;
                }

                if (!TokenStore.TryGetUserId(request.Token, out var userId))
                {
                    Logger.WarnFormat("LeaveLobby: invalid token. LobbyId={0}", request.LobbyId);
                    return;
                }

                var intId = GetLobbyIdFromUid(request.LobbyId);

                using (var sqlConnection = new SqlConnection(Connection))
                using (var sqlCommand = new SqlCommand(LobbySql.Text.SP_LOBBY_LEAVE, sqlConnection))
                {
                    sqlCommand.CommandType = CommandType.StoredProcedure;
                    sqlCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    sqlCommand.Parameters.Add("@LobbyId", SqlDbType.Int).Value = intId;
                    sqlConnection.Open();
                    sqlCommand.ExecuteNonQuery();
                }

                Logger.InfoFormat("LeaveLobby: user left lobby. UserId={0}, LobbyUid={1}, LobbyId={2}",
                    userId,
                    request.LobbyId,
                    intId);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error at LeaveLobby.", ex);
            }
            finally
            {
                if (request != null && request.LobbyId != Guid.Empty)
                {
                    RemoveCallbackForLobby(request.LobbyId);
                }
            }
        }

        public ListLobbiesResponse ListLobbies(ListLobbiesRequest request)
        {
            Logger.Debug("ListLobbies called (currently returns empty list).");
            return new ListLobbiesResponse { Lobbies = new List<LobbyInfo>() };
        }

        public void SendChatMessage(SendLobbyMessageRequest request)
        {
            if (request == null || request.LobbyId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token))
            {
                Logger.Debug("SendChatMessage: invalid request (null, empty LobbyId or token).");
                return;
            }

            if (!TokenStore.TryGetUserId(request.Token, out var userId))
            {
                Logger.WarnFormat("SendChatMessage: invalid token. LobbyId={0}", request.LobbyId);
                return;
            }

            var senderName = GetUserDisplayName(userId);

            var chatMessage = new ChatMessage
            {
                FromPlayerId = Guid.Empty,
                FromPlayerName = senderName,
                Message = request.Message ?? string.Empty,
                SentAtUtc = DateTime.UtcNow
            };

            Logger.InfoFormat("SendChatMessage: LobbyId={0}, UserId={1}, SenderName={2}",
                request.LobbyId,
                userId,
                senderName);

            BroadcastToLobby(request.LobbyId, cb => cb.OnChatMessageReceived(chatMessage));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            var userId = EnsureAuthorizedAndGetUserId(token);

            UpdateAccountResponse response;

            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_MY_PROFILE, sqlConnection))
            {
                sqlCommand.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                sqlConnection.Open();
                using (var rd = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!rd.Read())
                    {
                        throw ThrowFault(ERROR_NOT_FOUND, "Usuario no encontrado.");
                    }

                    Logger.DebugFormat("GetMyProfile: UserId={0} found.", userId);

                    response = new UpdateAccountResponse
                    {
                        UserId = rd.GetInt32(0),
                        DisplayName = rd.IsDBNull(1) ? null : rd.GetString(1),
                        ProfileImageUrl = rd.IsDBNull(2) ? null : rd.GetString(2),
                        CreatedAtUtc = rd.GetDateTime(3),
                        Email = rd.GetString(4)
                    };
                }
            }

            var avatarSql = new UserAvatarSql(Connection);
            var avatarEntity = avatarSql.GetByUserId(userId);
            response.Avatar = MapAvatar(avatarEntity);

            return response;
        }



        public UpdateAccountResponse UpdateAccount(UpdateAccountRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request nulo.");
            }

            var userId = EnsureAuthorizedAndGetUserId(request.Token);

            var hasDisplayNameChange = !string.IsNullOrWhiteSpace(request.DisplayName);
            var hasProfileImageChange = !string.IsNullOrWhiteSpace(request.ProfileImageUrl);
            var hasEmailChange = !string.IsNullOrWhiteSpace(request.Email);

            if (!hasDisplayNameChange && !hasProfileImageChange && !hasEmailChange)
            {
                Logger.DebugFormat("UpdateAccount: no changes detected. UserId={0}", userId);
                return GetMyProfile(request.Token);
            }

            ValidateProfileChanges(request, hasDisplayNameChange, hasProfileImageChange);

            string normalizedEmail = null;
            if (hasEmailChange)
            {
                normalizedEmail = ValidateAndNormalizeEmail(request.Email);
                EnsureEmailIsNotTaken(normalizedEmail, userId);
            }

            if (hasDisplayNameChange || hasProfileImageChange)
            {
                UpdateUserProfile(request, userId, hasDisplayNameChange, hasProfileImageChange);
            }

            if (hasEmailChange)
            {
                UpdateUserEmail(normalizedEmail, userId);
            }

            Logger.InfoFormat(
                "UpdateAccount: UserId={0}, DisplayNameChange={1}, ProfileImageChange={2}, EmailChange={3}",
                userId,
                hasDisplayNameChange,
                hasProfileImageChange,
                hasEmailChange);

            return GetMyProfile(request.Token);
        }

        public CreateLobbyResponse CreateLobby(CreateLobbyRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request nulo.");
            }

            var ownerId = EnsureAuthorizedAndGetUserId(request.Token);

            int lobbyId;
            Guid lobbyUid;
            string accessCode;

            using (var sqlConnection = new SqlConnection(Connection))
            {
                sqlConnection.Open();

                using (var clean = new SqlCommand(LobbySql.Text.SP_LOBBY_LEAVE_ALL_BY_USER, sqlConnection))
                {
                    clean.CommandType = CommandType.StoredProcedure;
                    clean.Parameters.Add("@UserId", SqlDbType.Int).Value = ownerId;
                    clean.ExecuteNonQuery();
                }

                using (var sqlCommand = new SqlCommand(LobbySql.Text.SP_LOBBY_CREATE, sqlConnection))
                {
                    sqlCommand.CommandType = CommandType.StoredProcedure;
                    sqlCommand.Parameters.Add("@OwnerUserId", SqlDbType.Int).Value = ownerId;
                    sqlCommand.Parameters.Add("@Name", SqlDbType.NVarChar, MAX_DISPLAY_NAME_LENGTH).Value =
                        string.IsNullOrWhiteSpace(request.LobbyName) ? (object)DBNull.Value : request.LobbyName.Trim();
                    sqlCommand.Parameters.Add("@MaxPlayers", SqlDbType.TinyInt).Value =
                        request.MaxPlayers > 0 ? request.MaxPlayers : DEFAULT_MAX_PLAYERS;

                    var pId = sqlCommand.Parameters.Add("@LobbyId", SqlDbType.Int);
                    pId.Direction = ParameterDirection.Output;
                    var pUid = sqlCommand.Parameters.Add("@LobbyUid", SqlDbType.UniqueIdentifier);
                    pUid.Direction = ParameterDirection.Output;
                    var pCode = sqlCommand.Parameters.Add("@AccessCode", SqlDbType.NVarChar, ACCESS_CODE_MAX_LENGTH);
                    pCode.Direction = ParameterDirection.Output;

                    sqlCommand.ExecuteNonQuery();

                    lobbyId = (int)pId.Value;
                    lobbyUid = (Guid)pUid.Value;
                    accessCode = (string)pCode.Value;
                }
            }

            Logger.InfoFormat(
                "CreateLobby: Lobby created. LobbyIdInt={0}, LobbyUid={1}, OwnerUserId={2}",
                lobbyId,
                lobbyUid,
                ownerId);

            var callback = OperationContext.Current.GetCallbackChannel<ILobbyClientCallback>();
            AddCallbackForLobby(lobbyUid, callback);

            var info = LoadLobbyInfoByIntId(lobbyId);
            if (string.IsNullOrWhiteSpace(info.AccessCode))
            {
                info.AccessCode = accessCode;
            }

            callback.OnLobbyUpdated(info);

            return new CreateLobbyResponse { Lobby = info };
        }

        public JoinByCodeResponse JoinByCode(JoinByCodeRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request nulo.");
            }

            if (string.IsNullOrWhiteSpace(request.AccessCode))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "AccessCode requerido.");
            }

            var userId = EnsureAuthorizedAndGetUserId(request.Token);

            int lobbyId;
            Guid lobbyUid;

            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.SP_LOBBY_JOIN_BY_CODE, sqlConnection))
            {
                sqlCommand.CommandType = CommandType.StoredProcedure;
                sqlCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                sqlCommand.Parameters.Add("@AccessCode", SqlDbType.NVarChar, ACCESS_CODE_MAX_LENGTH).Value =
                    request.AccessCode.Trim().ToUpperInvariant();

                var pId = sqlCommand.Parameters.Add("@LobbyId", SqlDbType.Int);
                pId.Direction = ParameterDirection.Output;
                var pUid = sqlCommand.Parameters.Add("@LobbyUid", SqlDbType.UniqueIdentifier);
                pUid.Direction = ParameterDirection.Output;

                sqlConnection.Open();
                sqlCommand.ExecuteNonQuery();

                lobbyId = (int)pId.Value;
                lobbyUid = (Guid)pUid.Value;
            }

            Logger.InfoFormat(
                "JoinByCode: UserId={0} joined lobby. LobbyIdInt={1}, LobbyUid={2}, AccessCode={3}",
                userId,
                lobbyId,
                lobbyUid,
                request.AccessCode);

            var callback = OperationContext.Current.GetCallbackChannel<ILobbyClientCallback>();
            AddCallbackForLobby(lobbyUid, callback);

            var info = LoadLobbyInfoByIntId(lobbyId);
            callback.OnLobbyUpdated(info);

            return new JoinByCodeResponse { Lobby = info };
        }

        private static int EnsureAuthorizedAndGetUserId(string token)
        {
            if (!TokenStore.TryGetUserId(token, out var userId))
            {
                throw ThrowFault(ERROR_UNAUTHORIZED, "Token inválido o expirado.");
            }

            return userId;
        }

        private static void ValidateProfileChanges(
            UpdateAccountRequest request,
            bool hasDisplayNameChange,
            bool hasProfileImageChange)
        {
            if (hasDisplayNameChange && request.DisplayName.Trim().Length > MAX_DISPLAY_NAME_LENGTH)
            {
                throw ThrowFault(ERROR_VALIDATION_ERROR, $"DisplayName máximo {MAX_DISPLAY_NAME_LENGTH}.");
            }

            if (hasProfileImageChange && request.ProfileImageUrl.Trim().Length > MAX_PROFILE_IMAGE_URL_LENGTH)
            {
                throw ThrowFault(ERROR_VALIDATION_ERROR, $"ProfileImageUrl máximo {MAX_PROFILE_IMAGE_URL_LENGTH}.");
            }
        }

        private static string ValidateAndNormalizeEmail(string email)
        {
            var trimmedEmail = (email ?? string.Empty).Trim();

            if (!IsValidEmail(trimmedEmail))
            {
                throw ThrowFault(ERROR_VALIDATION_ERROR, "Email inválido.");
            }

            if (trimmedEmail.Length > MAX_EMAIL_LENGTH)
            {
                throw ThrowFault(ERROR_VALIDATION_ERROR, $"Email máximo {MAX_EMAIL_LENGTH}.");
            }

            return trimmedEmail;
        }

        private static void EnsureEmailIsNotTaken(string email, int userId)
        {
            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.EMAIL_EXISTS_EXCEPT_ID, sqlConnection))
            {
                sqlCommand.Parameters.Add("@E", SqlDbType.NVarChar, MAX_EMAIL_LENGTH).Value = email;
                sqlCommand.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                sqlConnection.Open();
                var exists = sqlCommand.ExecuteScalar();
                if (exists != null)
                {
                    throw ThrowFault(ERROR_EMAIL_TAKEN, "Ese email ya está en uso.");
                }
            }
        }

        private static void UpdateUserProfile(
            UpdateAccountRequest request,
            int userId,
            bool hasDisplayNameChange,
            bool hasProfileImageChange)
        {
            var sqlLobby = LobbySql.BuildUpdateUser(hasDisplayNameChange, hasProfileImageChange);

            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(sqlLobby, sqlConnection))
            {
                sqlCommand.Parameters.Add("@Id", SqlDbType.Int).Value = userId;

                if (hasDisplayNameChange)
                {
                    sqlCommand.Parameters.Add("@DisplayName", SqlDbType.NVarChar, MAX_DISPLAY_NAME_LENGTH)
                        .Value = request.DisplayName.Trim();
                }

                if (hasProfileImageChange)
                {
                    sqlCommand.Parameters.Add("@ImageUrl", SqlDbType.NVarChar, MAX_PROFILE_IMAGE_URL_LENGTH)
                        .Value = request.ProfileImageUrl.Trim();
                }

                sqlConnection.Open();
                var rows = sqlCommand.ExecuteNonQuery();
                if (rows == 0)
                {
                    throw ThrowFault(ERROR_NOT_FOUND, "Usuario no encontrado.");
                }
            }
        }

        private static void UpdateUserEmail(string email, int userId)
        {
            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.UPDATE_ACCOUNT_EMAIL, sqlConnection))
            {
                sqlCommand.Parameters.Add("@E", SqlDbType.NVarChar, MAX_EMAIL_LENGTH).Value = email;
                sqlCommand.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                sqlConnection.Open();
                var rows = sqlCommand.ExecuteNonQuery();
                if (rows == 0)
                {
                    throw ThrowFault(ERROR_NOT_FOUND, "Cuenta no encontrada.");
                }
            }
        }

        private static int GetLobbyIdFromUid(Guid lobbyUid)
        {
            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_LOBBY_ID_FROM_UID, sqlConnection))
            {
                sqlCommand.Parameters.Add("@u", SqlDbType.UniqueIdentifier).Value = lobbyUid;
                sqlConnection.Open();
                var obj = sqlCommand.ExecuteScalar();
                if (obj == null)
                {
                    throw ThrowFault(ERROR_NOT_FOUND, "Lobby no encontrado.");
                }

                return Convert.ToInt32(obj);
            }
        }

        private static LobbyInfo LoadLobbyInfoByIntId(int lobbyId)
        {
            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_LOBBY_BY_ID, sqlConnection))
            {
                sqlCommand.Parameters.Add("@id", SqlDbType.Int).Value = lobbyId;
                sqlConnection.Open();
                using (var rd = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!rd.Read())
                    {
                        throw ThrowFault(ERROR_NOT_FOUND, "Lobby no encontrado.");
                    }

                    var uid = rd.GetGuid(0);
                    var name = rd.IsDBNull(1) ? null : rd.GetString(1);
                    var maxPlayers = rd.GetByte(2);
                    var accessCode = rd.IsDBNull(3) ? null : rd.GetString(3);

                    return new LobbyInfo
                    {
                        LobbyId = uid,
                        LobbyName = name,
                        MaxPlayers = maxPlayers,
                        Players = new List<PlayerSummary>(),
                        AccessCode = accessCode
                    };
                }
            }
        }

        private static string GetUserDisplayName(int userId)
        {
            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_USER_DISPLAY_NAME, sqlConnection))
            {
                sqlCommand.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                sqlConnection.Open();
                var obj = sqlCommand.ExecuteScalar();
                var name = (obj == null || obj == DBNull.Value) ? null : Convert.ToString(obj);
                var finalName = string.IsNullOrWhiteSpace(name) ? "Jugador " + userId : name.Trim();

                Logger.DebugFormat("GetUserDisplayName: UserId={0}, DisplayName={1}", userId, finalName);

                return finalName;
            }
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var _ = new System.Net.Mail.MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat("LobbyService fault. Code='{0}', Message='{1}'", code, message);

            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        public StartLobbyMatchResponse StartLobbyMatch(StartLobbyMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request nulo.");
            }



            var hostUserId = EnsureAuthorizedAndGetUserId(request.Token);

            try
            {
                var manager = new MatchManager(Connection);

                var createRequest = new CreateMatchRequest
                {
                    Token = request.Token,
                    MaxPlayers = DEFAULT_MAX_PLAYERS,
                    Config = new MatchConfigDto
                    {
                        StartingScore = 0m,
                        MaxScore = 100m,
                        PointsPerCorrect = 1m,
                        PointsPerWrong = -1m,
                        PointsPerEliminationGain = 0m,
                        AllowTiebreakCoinflip = true
                    },
                    IsPrivate = false
                };

                var createResponse = manager.CreateMatch(hostUserId, createRequest);
                var match = createResponse.Match;

                if (match == null)
                {
                    throw ThrowTechnicalFault(
                        ERROR_UNEXPECTED,
                        MESSAGE_UNEXPECTED_ERROR,
                        "LobbyService.StartLobbyMatch.NullMatch",
                        new InvalidOperationException("MatchManager returned null Match."));
                }


                if (TryGetLobbyUidForCurrentSession(out var lobbyUid))
                {
                    Logger.InfoFormat(
                        "StartLobbyMatch: broadcasting OnMatchStarted. LobbyUid={0}",
                        lobbyUid);

                    BroadcastToLobby(lobbyUid, cb =>
                    {
                        try
                        {
                            cb.OnMatchStarted(match);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Error sending OnMatchStarted callback.", ex);
                        }
                    });
                }
                else
                {
                    Logger.Warn("StartLobbyMatch: could not resolve lobbyUid for current session.");
                }

                return new StartLobbyMatchResponse
                {
                    Match = match
                };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "LobbyService.StartLobbyMatch",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "LobbyService.StartLobbyMatch",
                    ex);
            }
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

        private static bool TryGetLobbyUidForCurrentSession(out Guid lobbyUid)
        {
            foreach (var kv in CallbackBuckets)
            {
                if (kv.Value.ContainsKey(CurrentSessionId))
                {
                    lobbyUid = kv.Key;
                    return true;
                }
            }

            lobbyUid = Guid.Empty;
            return false;
        }
    }
}
