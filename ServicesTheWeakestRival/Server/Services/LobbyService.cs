using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class LobbyService : ILobbyService
    {
        // ====== CADENA DE CONEXIÓN (Opción A) ======
        private static string Cnx =>
            ConfigurationManager.ConnectionStrings["TheWeakestRivalDb"].ConnectionString;

        // ====== CALLBACKS EXISTENTES ======
        private static readonly ConcurrentDictionary<Guid, ILobbyClientCallback> Callbacks =
            new ConcurrentDictionary<Guid, ILobbyClientCallback>();

        // ====== EXISTENTE ======
        public JoinLobbyResponse JoinLobby(JoinLobbyRequest request)
        {
            var cb = OperationContext.Current.GetCallbackChannel<ILobbyClientCallback>();
            var lobby = new LobbyInfo
            {
                LobbyId = Guid.NewGuid(),
                LobbyName = request.LobbyName,
                MaxPlayers = 8,
                Players = new List<PlayerSummary>()
            };
            Callbacks[lobby.LobbyId] = cb;
            cb.OnLobbyUpdated(lobby);
            return new JoinLobbyResponse { Lobby = lobby };
        }

        public void LeaveLobby(LeaveLobbyRequest request)
        {
            Callbacks.TryRemove(request.LobbyId, out _);
        }

        public ListLobbiesResponse ListLobbies(ListLobbiesRequest request) =>
            new ListLobbiesResponse { Lobbies = new List<LobbyInfo>() };

        public void SendChatMessage(SendLobbyMessageRequest request)
        {
            if (Callbacks.TryGetValue(request.LobbyId, out var cb))
            {
                cb.OnChatMessageReceived(new ChatMessage
                {
                    FromPlayerId = Guid.Empty,
                    FromPlayerName = "System",
                    Message = request.Message,
                    SentAtUtc = DateTime.UtcNow
                });
            }
        }

        // ====== NUEVO: PERFIL ======
        public UpdateAccountResponse GetMyProfile(string token)
        {
            if (!TokenStore.TryGetUserId(token, out var userId))
                ThrowFault("UNAUTHORIZED", "Token inválido o expirado.");

            const string q = @"
        SELECT u.user_id, u.display_name, u.profile_image_url, u.created_at, a.email
        FROM dbo.Users u
        JOIN dbo.Accounts a ON a.account_id = u.user_id
        WHERE u.user_id = @Id;";

            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand(q, cn))
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

        public UpdateAccountResponse UpdateAccount(UpdateAccountRequest req)
        {
            if (req == null) ThrowFault("INVALID_REQUEST", "Request nulo.");
            if (!TokenStore.TryGetUserId(req.Token, out var userId))
                ThrowFault("UNAUTHORIZED", "Token inválido o expirado.");

            var setName = !string.IsNullOrWhiteSpace(req.DisplayName);
            var setImg = !string.IsNullOrWhiteSpace(req.ProfileImageUrl);
            var setEmail = !string.IsNullOrWhiteSpace(req.Email);

            if (!setName && !setImg && !setEmail)
                return GetMyProfile(req.Token);

            if (setName && req.DisplayName.Trim().Length > 80)
                ThrowFault("VALIDATION_ERROR", "DisplayName máximo 80.");
            if (setImg && req.ProfileImageUrl.Trim().Length > 500)
                ThrowFault("VALIDATION_ERROR", "ProfileImageUrl máximo 500.");
            if (setEmail)
            {
                var email = req.Email.Trim();
                if (!IsValidEmail(email))
                    ThrowFault("VALIDATION_ERROR", "Email inválido.");

                // Unicidad
                const string qExists = "SELECT 1 FROM dbo.Accounts WHERE email = @E AND account_id <> @Id;";
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(qExists, cn))
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
                var sql = "UPDATE dbo.Users SET ";
                if (setName) sql += "display_name = @DisplayName";
                if (setImg) sql += (setName ? ", " : "") + "profile_image_url = @ImageUrl";
                sql += " WHERE user_id = @Id;";

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                    if (setName) cmd.Parameters.Add("@DisplayName", SqlDbType.NVarChar, 80).Value = req.DisplayName.Trim();
                    if (setImg) cmd.Parameters.Add("@ImageUrl", SqlDbType.NVarChar, 500).Value = req.ProfileImageUrl.Trim();
                    cn.Open();
                    var rows = cmd.ExecuteNonQuery();
                    if (rows == 0) ThrowFault("NOT_FOUND", "Usuario no encontrado.");
                }
            }

            // Accounts: email
            if (setEmail)
            {
                const string qUpd = "UPDATE dbo.Accounts SET email = @E WHERE account_id = @Id;";
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(qUpd, cn))
                {
                    cmd.Parameters.Add("@E", SqlDbType.NVarChar, 320).Value = req.Email.Trim();
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = userId;
                    cn.Open();
                    var rows = cmd.ExecuteNonQuery();
                    if (rows == 0) ThrowFault("NOT_FOUND", "Cuenta no encontrada.");
                }
            }

            return GetMyProfile(req.Token);
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
