using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic; // <-- usar LobbySql

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class LobbyService : ILobbyService
    {
        private static string Cnx =>
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
            var cb = OperationContext.Current.GetCallbackChannel<ILobbyClientCallback>();
            var lobby = new LobbyInfo
            {
                LobbyId = Guid.NewGuid(),
                LobbyName = request != null ? request.LobbyName : "Lobby",
                MaxPlayers = 8,
                Players = new List<PlayerSummary>(),
                AccessCode = null
            };

            AddCallbackForLobby(lobby.LobbyId, cb);
            cb.OnLobbyUpdated(lobby);
            return new JoinLobbyResponse { Lobby = lobby };
        }

        public void LeaveLobby(LeaveLobbyRequest request)
        {
            try
            {
                if (request != null && !string.IsNullOrWhiteSpace(request.Token) && request.LobbyId != Guid.Empty)
                {
                    if (TokenStore.TryGetUserId(request.Token, out var userId))
                    {
                        var intId = GetLobbyIdFromUid(request.LobbyId);
                        using (var cn = new SqlConnection(Cnx))
                        using (var cmd = new SqlCommand(LobbySql.Text.SP_LOBBY_LEAVE, cn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                            cmd.Parameters.Add("@LobbyId", SqlDbType.Int).Value = intId;
                            cn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch
            {
                // TODO: logger
            }
            finally
            {
                if (request != null && request.LobbyId != Guid.Empty)
                    RemoveCallbackForLobby(request.LobbyId);
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

            var msg = new ChatMessage
            {
                FromPlayerId = Guid.Empty,
                FromPlayerName = senderName,
                Message = request.Message ?? string.Empty,
                SentAtUtc = DateTime.UtcNow
            };

            BroadcastToLobby(request.LobbyId, cb => cb.OnChatMessageReceived(msg));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            if (!TokenStore.TryGetUserId(token, out var userId))
                ThrowFault("UNAUTHORIZED", "Token inválido o expirado.");

            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand(LobbySql.Text.GET_MY_PROFILE, cn))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                cn.Open();
                using (var rd = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!rd.Read()) ThrowFault("NOT_FOUND", "Usuario no encontrado.");
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
            if (request == null) ThrowFault("INVALID_REQUEST", "Request nulo.");
            if (!TokenStore.TryGetUserId(request.Token, out var userId))
                ThrowFault("UNAUTHORIZED", "Token inválido o expirado.");

            var setName = !string.IsNullOrWhiteSpace(request.DisplayName);
            var setImg = !string.IsNullOrWhiteSpace(request.ProfileImageUrl);
            var setEmail = !string.IsNullOrWhiteSpace(request.Email);

            if (!setName && !setImg && !setEmail)
                return GetMyProfile(request.Token);

            if (setName && request.DisplayName.Trim().Length > 80)
                ThrowFault("VALIDATION_ERROR", "DisplayName máximo 80.");
            if (setImg && request.ProfileImageUrl.Trim().Length > 500)
                ThrowFault("VALIDATION_ERROR", "ProfileImageUrl máximo 500.");

            if (setEmail)
            {
                var email = request.Email.Trim();
                if (!IsValidEmail(email))
                    ThrowFault("VALIDATION_ERROR", "Email inválido.");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(LobbySql.Text.EMAIL_EXISTS_EXCEPT_ID, cn))
                {
                    cmd.Parameters.Add("@E", SqlDbType.NVarChar, 320).Value = email;
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                    cn.Open();
                    var exists = cmd.ExecuteScalar();
                    if (exists != null) ThrowFault("EMAIL_TAKEN", "Ese email ya está en uso.");
                }
            }

            if (setName || setImg)
            {
                var sql = LobbySql.BuildUpdateUser(setName, setImg);
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                    if (setName) cmd.Parameters.Add("@DisplayName", SqlDbType.NVarChar, 80).Value = request.DisplayName.Trim();
                    if (setImg) cmd.Parameters.Add("@ImageUrl", SqlDbType.NVarChar, 500).Value = request.ProfileImageUrl.Trim();
                    cn.Open();
                    var rows = cmd.ExecuteNonQuery();
                    if (rows == 0) ThrowFault("NOT_FOUND", "Usuario no encontrado.");
                }
            }

            if (setEmail)
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(LobbySql.Text.UPDATE_ACCOUNT_EMAIL, cn))
                {
                    cmd.Parameters.Add("@E", SqlDbType.NVarChar, 320).Value = request.Email.Trim();
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                    cn.Open();
                    var rows = cmd.ExecuteNonQuery();
                    if (rows == 0) ThrowFault("NOT_FOUND", "Cuenta no encontrada.");
                }
            }

            return GetMyProfile(request.Token);
        }

        public CreateLobbyResponse CreateLobby(CreateLobbyRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request nulo.");
            var ownerId = EnsureAuthorizedAndGetUserId(request.Token);

            int lobbyId;
            Guid lobbyUid;
            string accessCode;

            using (var cn = new SqlConnection(Cnx))
            {
                cn.Open();

                using (var clean = new SqlCommand(LobbySql.Text.SP_LOBBY_LEAVE_ALL_BY_USER, cn))
                {
                    clean.CommandType = CommandType.StoredProcedure;
                    clean.Parameters.Add("@UserId", SqlDbType.Int).Value = ownerId;
                    clean.ExecuteNonQuery();
                }

                using (var cmd = new SqlCommand(LobbySql.Text.SP_LOBBY_CREATE, cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@OwnerUserId", SqlDbType.Int).Value = ownerId;
                    cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 80).Value =
                        string.IsNullOrWhiteSpace(request.LobbyName) ? (object)DBNull.Value : request.LobbyName.Trim();
                    cmd.Parameters.Add("@MaxPlayers", SqlDbType.TinyInt).Value = request.MaxPlayers > 0 ? request.MaxPlayers : 8;

                    var pId = cmd.Parameters.Add("@LobbyId", SqlDbType.Int); pId.Direction = ParameterDirection.Output;
                    var pUid = cmd.Parameters.Add("@LobbyUid", SqlDbType.UniqueIdentifier); pUid.Direction = ParameterDirection.Output;
                    var pCode = cmd.Parameters.Add("@AccessCode", SqlDbType.NVarChar, 12); pCode.Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    lobbyId = (int)pId.Value;
                    lobbyUid = (Guid)pUid.Value;
                    accessCode = (string)pCode.Value;
                }
            }

            var cb = OperationContext.Current.GetCallbackChannel<ILobbyClientCallback>();
            AddCallbackForLobby(lobbyUid, cb);

            var info = LoadLobbyInfoByIntId(lobbyId);
            if (string.IsNullOrWhiteSpace(info.AccessCode))
                info.AccessCode = accessCode;

            cb.OnLobbyUpdated(info);

            return new CreateLobbyResponse { Lobby = info };
        }

        public JoinByCodeResponse JoinByCode(JoinByCodeRequest request)
        {
            if (request == null) ThrowFault("INVALID_REQUEST", "Request nulo.");
            if (string.IsNullOrWhiteSpace(request.AccessCode))
                ThrowFault("INVALID_REQUEST", "AccessCode requerido.");

            var userId = EnsureAuthorizedAndGetUserId(request.Token);

            int lobbyId;
            Guid lobbyUid;

            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand(LobbySql.Text.SP_LOBBY_JOIN_BY_CODE, cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                cmd.Parameters.Add("@AccessCode", SqlDbType.NVarChar, 12).Value = request.AccessCode.Trim().ToUpperInvariant();

                var pId = cmd.Parameters.Add("@LobbyId", SqlDbType.Int); pId.Direction = ParameterDirection.Output;
                var pUid = cmd.Parameters.Add("@LobbyUid", SqlDbType.UniqueIdentifier); pUid.Direction = ParameterDirection.Output;

                cn.Open();
                cmd.ExecuteNonQuery();

                lobbyId = (int)pId.Value;
                lobbyUid = (Guid)pUid.Value;
            }

            var cb = OperationContext.Current.GetCallbackChannel<ILobbyClientCallback>();
            AddCallbackForLobby(lobbyUid, cb);

            var info = LoadLobbyInfoByIntId(lobbyId);
            cb.OnLobbyUpdated(info);

            return new JoinByCodeResponse { Lobby = info };
        }

        private static int EnsureAuthorizedAndGetUserId(string token)
        {
            if (!TokenStore.TryGetUserId(token, out var userId))
                ThrowFault("UNAUTHORIZED", "Token inválido o expirado.");
            return userId;
        }

        private static int GetLobbyIdFromUid(Guid lobbyUid)
        {
            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand(LobbySql.Text.GET_LOBBY_ID_FROM_UID, cn))
            {
                cmd.Parameters.Add("@u", SqlDbType.UniqueIdentifier).Value = lobbyUid;
                cn.Open();
                var obj = cmd.ExecuteScalar();
                if (obj == null) ThrowFault("NOT_FOUND", "Lobby no encontrado.");
                return Convert.ToInt32(obj);
            }
        }

        private static LobbyInfo LoadLobbyInfoByIntId(int lobbyId)
        {
            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand(LobbySql.Text.GET_LOBBY_BY_ID, cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = lobbyId;
                cn.Open();
                using (var rd = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!rd.Read()) ThrowFault("NOT_FOUND", "Lobby no encontrado.");

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
            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand(LobbySql.Text.GET_USER_DISPLAY_NAME, cn))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                cn.Open();
                var obj = cmd.ExecuteScalar();
                var name = (obj == null || obj == DBNull.Value) ? null : Convert.ToString(obj);
                return string.IsNullOrWhiteSpace(name) ? ("Jugador " + userId) : name.Trim();
            }
        }

        private static bool IsValidEmail(string email)
        {
            try { var _ = new System.Net.Mail.MailAddress(email); return true; }
            catch { return false; }
        }

        private static void ThrowFault(string code, string message)
        {
            var fault = new ServiceFault { Code = code, Message = message };
            throw new FaultException<ServiceFault>(fault, new FaultReason(message));
        }
    }
}
