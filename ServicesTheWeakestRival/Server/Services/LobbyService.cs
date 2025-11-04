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

        private static string Connection =>
            ConfigurationManager.ConnectionStrings["TheWeakestRivalDb"].ConnectionString;

        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, ILobbyClientCallback>> CallbackBuckets
            = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, ILobbyClientCallback>>();

        private static string CurrentSessionId
        {
            get { return OperationContext.Current != null ? OperationContext.Current.SessionId : Guid.NewGuid().ToString("N"); }
        }

        private static void AddCallbackForLobby(Guid lobbyUid, ILobbyClientCallback cb)
        {
            var bucket = CallbackBuckets.GetOrAdd(lobbyUid, _ => new ConcurrentDictionary<string, ILobbyClientCallback>());
            bucket[CurrentSessionId] = cb;
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
            }
        }

        private static void BroadcastToLobby(Guid lobbyUid, Action<ILobbyClientCallback> send)
        {
            if (!CallbackBuckets.TryGetValue(lobbyUid, out var bucket)) return;

            foreach (var kv in bucket)
            {
                try { send(kv.Value); }
                catch { /* canal caído, ignorar o loggear */ }
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
                    return;
                }

                if (!TokenStore.TryGetUserId(request.Token, out var userId))
                {
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
            }
            catch (Exception ex)
            {
                // TODO: logger / ThrowTechnicalFault(...)
            }
            finally
            {
                if (request != null && request.LobbyId != Guid.Empty)
                {
                    RemoveCallbackForLobby(request.LobbyId);
                }
            }
        }

        public ListLobbiesResponse ListLobbies(ListLobbiesRequest request) =>
            new ListLobbiesResponse { Lobbies = new List<LobbyInfo>() };

        public void SendChatMessage(SendLobbyMessageRequest request)
        {
            if (request == null || request.LobbyId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token))
                return;

            if (!TokenStore.TryGetUserId(request.Token, out var userId))
                return;

            var senderName = GetUserDisplayName(userId);

            var chatMessage = new ChatMessage
            {
                FromPlayerId = Guid.Empty,
                FromPlayerName = senderName,
                Message = request.Message ?? string.Empty,
                SentAtUtc = DateTime.UtcNow
            };

            BroadcastToLobby(request.LobbyId, cb => cb.OnChatMessageReceived(chatMessage));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            var userId = EnsureAuthorizedAndGetUserId(token);

            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_MY_PROFILE, sqlConnection))
            {
                sqlCommand.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                sqlConnection.Open();
                using (var rd = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!rd.Read()) throw ThrowFault("NOT_FOUND", "Usuario no encontrado.");
                    return new UpdateAccountResponse
                    {
                        UserId = rd.GetInt32(0),
                        DisplayName = rd.IsDBNull(1) ? null : rd.GetString(1),
                        ProfileImageUrl = rd.IsDBNull(2) ? null : rd.GetString(2),
                        CreatedAtUtc = rd.GetDateTime(3),
                        Email = rd.GetString(4)
                    };
                }
            }
        }

        public UpdateAccountResponse UpdateAccount(UpdateAccountRequest request)
        {
            if (request == null)
            {
                throw ThrowFault("INVALID_REQUEST", "Request nulo.");
            }

            var userId = EnsureAuthorizedAndGetUserId(request.Token);

            var hasDisplayNameChange = !string.IsNullOrWhiteSpace(request.DisplayName);
            var hasProfileImageChange = !string.IsNullOrWhiteSpace(request.ProfileImageUrl);
            var hasEmailChange = !string.IsNullOrWhiteSpace(request.Email);

            if (!hasDisplayNameChange && !hasProfileImageChange && !hasEmailChange)
            {
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

            return GetMyProfile(request.Token);
        }


        public CreateLobbyResponse CreateLobby(CreateLobbyRequest request)
        {
            if (request == null) throw ThrowFault("INVALID_REQUEST", "Request nulo.");
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

                    var pId = sqlCommand.Parameters.Add("@LobbyId", SqlDbType.Int); pId.Direction = ParameterDirection.Output;
                    var pUid = sqlCommand.Parameters.Add("@LobbyUid", SqlDbType.UniqueIdentifier); pUid.Direction = ParameterDirection.Output;
                    var pCode = sqlCommand.Parameters.Add("@AccessCode", SqlDbType.NVarChar, ACCESS_CODE_MAX_LENGTH); pCode.Direction = ParameterDirection.Output;

                    sqlCommand.ExecuteNonQuery();

                    lobbyId = (int)pId.Value;
                    lobbyUid = (Guid)pUid.Value;
                    accessCode = (string)pCode.Value;
                }
            }

            var callback = OperationContext.Current.GetCallbackChannel<ILobbyClientCallback>();
            AddCallbackForLobby(lobbyUid, callback);

            var info = LoadLobbyInfoByIntId(lobbyId);
            if (string.IsNullOrWhiteSpace(info.AccessCode))
                info.AccessCode = accessCode;

            callback.OnLobbyUpdated(info);

            return new CreateLobbyResponse { Lobby = info };
        }

        public JoinByCodeResponse JoinByCode(JoinByCodeRequest request)
        {
            if (request == null) throw ThrowFault("INVALID_REQUEST", "Request nulo.");
            if (string.IsNullOrWhiteSpace(request.AccessCode))
                throw ThrowFault("INVALID_REQUEST", "AccessCode requerido.");

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

                var pId = sqlCommand.Parameters.Add("@LobbyId", SqlDbType.Int); pId.Direction = ParameterDirection.Output;
                var pUid = sqlCommand.Parameters.Add("@LobbyUid", SqlDbType.UniqueIdentifier); pUid.Direction = ParameterDirection.Output;

                sqlConnection.Open();
                sqlCommand.ExecuteNonQuery();

                lobbyId = (int)pId.Value;
                lobbyUid = (Guid)pUid.Value;
            }

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
                throw ThrowFault("UNAUTHORIZED", "Token inválido o expirado.");
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
                throw ThrowFault("VALIDATION_ERROR", $"DisplayName máximo {MAX_DISPLAY_NAME_LENGTH}.");
            }

            if (hasProfileImageChange && request.ProfileImageUrl.Trim().Length > MAX_PROFILE_IMAGE_URL_LENGTH)
            {
                throw ThrowFault("VALIDATION_ERROR", $"ProfileImageUrl máximo {MAX_PROFILE_IMAGE_URL_LENGTH}.");
            }
        }

        private static string ValidateAndNormalizeEmail(string email)
        {
            var trimmedEmail = (email ?? string.Empty).Trim();

            if (!IsValidEmail(trimmedEmail))
            {
                throw ThrowFault("VALIDATION_ERROR", "Email inválido.");
            }

            if (trimmedEmail.Length > MAX_EMAIL_LENGTH)
            {
                throw ThrowFault("VALIDATION_ERROR", $"Email máximo {MAX_EMAIL_LENGTH}.");
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
                    throw ThrowFault("EMAIL_TAKEN", "Ese email ya está en uso.");
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
                    throw ThrowFault("NOT_FOUND", "Usuario no encontrado.");
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
                    throw ThrowFault("NOT_FOUND", "Cuenta no encontrada.");
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
                if (obj == null) throw ThrowFault("NOT_FOUND", "Lobby no encontrado.");
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
                    if (!rd.Read()) throw ThrowFault("NOT_FOUND", "Lobby no encontrado.");

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
                return string.IsNullOrWhiteSpace(name) ? ("Jugador " + userId) : name.Trim();
            }
        }

        private static bool IsValidEmail(string email)
        {
            try { var _ = new System.Net.Mail.MailAddress(email); return true; }
            catch { return false; }
        }

        private static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }
    }
}
