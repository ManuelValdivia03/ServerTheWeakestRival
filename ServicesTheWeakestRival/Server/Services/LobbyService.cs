using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Lobby;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(
        InstanceContextMode = InstanceContextMode.Single,
        ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class LobbyService : ILobbyService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyService));

        private readonly LobbyCallbackHub callbackHub;

        private string connectionString;
        private LobbyRepository lobbyRepository;

        private LobbyRoomOperations lobbyRoomOperations;
        private LobbyAccountOperations lobbyAccountOperations;
        private LobbyMatchOperations lobbyMatchOperations;

        public LobbyService()
        {
            // Importante: NO leer config aquí (como antes).
            callbackHub = LobbyCallbackHub.Shared;
        }

        public JoinLobbyResponse JoinLobby(JoinLobbyRequest request)
        {
            LobbyServiceContext.ValidateRequest(request);

            try
            {
                int userId = LobbyServiceContext.Authenticate(request.Token);

                ILobbyClientCallback callback = LobbyServiceContext.GetCallbackChannel();
                LobbyCallbackRegistry.Upsert(userId, callback);

                var lobbyInfo = new LobbyInfo
                {
                    LobbyId = Guid.NewGuid(),
                    LobbyName = string.IsNullOrWhiteSpace(request.LobbyName)
                        ? LobbyServiceConstants.DEFAULT_LOBBY_NAME
                        : request.LobbyName,
                    MaxPlayers = LobbyServiceConstants.DEFAULT_MAX_PLAYERS,
                    Players = new List<AccountMini>(),
                    AccessCode = null
                };

                Logger.InfoFormat(
                    "JoinLobby: New in-memory lobby created. LobbyId={0}, LobbyName={1}",
                    lobbyInfo.LobbyId,
                    lobbyInfo.LobbyName);

                callbackHub.AddCallback(lobbyInfo.LobbyId, userId, callback);

                try
                {
                    callback.OnLobbyUpdated(lobbyInfo);
                }
                catch (Exception ex)
                {
                    Logger.Warn("JoinLobby: error enviando OnLobbyUpdated al cliente.", ex);
                }

                return new JoinLobbyResponse { Lobby = lobbyInfo };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw LobbyServiceContext.ThrowTechnicalFault(
                    LobbyServiceConstants.ERROR_UNEXPECTED,
                    LobbyServiceConstants.MESSAGE_UNEXPECTED_ERROR,
                    LobbyServiceConstants.CTX_JOIN_LOBBY,
                    ex);
            }
        }

        public void LeaveLobby(LeaveLobbyRequest request)
        {
            int userId = 0;

            try
            {
                if (request == null
                    || string.IsNullOrWhiteSpace(request.Token)
                    || request.LobbyId == Guid.Empty)
                {
                    Logger.Debug("LeaveLobby: invalid request (null, empty token or empty LobbyId).");
                    return;
                }

                if (!TokenStore.TryGetUserId(request.Token, out userId))
                {
                    Logger.WarnFormat("LeaveLobby: invalid token. LobbyId={0}", request.LobbyId);
                    return;
                }

                EnsureInitialized();

                lobbyRoomOperations.LeaveLobby(userId, request.LobbyId);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error at LeaveLobby.", ex);
            }
            finally
            {
                if (request != null && request.LobbyId != Guid.Empty && userId > 0)
                {
                    callbackHub.RemoveCallback(request.LobbyId, userId);
                }

                if (userId > 0)
                {
                    LobbyCallbackRegistry.Remove(userId);
                }
            }
        }

        public ListLobbiesResponse ListLobbies(ListLobbiesRequest request)
        {
            Logger.Debug("ListLobbies called (currently returns empty list).");

            return new ListLobbiesResponse
            {
                Lobbies = new List<LobbyInfo>()
            };
        }

        public void SendChatMessage(SendLobbyMessageRequest request)
        {
            if (request == null
                || request.LobbyId == Guid.Empty
                || string.IsNullOrWhiteSpace(request.Token))
            {
                Logger.Debug("SendChatMessage: invalid request (null, empty LobbyId or token).");
                return;
            }

            if (!TokenStore.TryGetUserId(request.Token, out int userId))
            {
                Logger.WarnFormat("SendChatMessage: invalid token. LobbyId={0}", request.LobbyId);
                return;
            }

            EnsureInitialized();

            callbackHub.TryRefreshLobbyCallbackRegistry(userId);

            string senderName = lobbyRepository.GetUserDisplayName(userId);

            var chatMessage = new ChatMessage
            {
                FromPlayerId = Guid.Empty,
                FromPlayerName = senderName,
                Message = request.Message ?? string.Empty,
                SentAtUtc = DateTime.UtcNow
            };

            Logger.InfoFormat(
                "SendChatMessage: LobbyId={0}, UserId={1}, SenderName={2}",
                request.LobbyId,
                userId,
                senderName);

            callbackHub.Broadcast(request.LobbyId, cb => cb.OnChatMessageReceived(chatMessage));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            EnsureInitialized();
            return lobbyAccountOperations.GetMyProfile(token);
        }

        public UpdateAccountResponse UpdateAccount(UpdateAccountRequest request)
        {
            EnsureInitialized();
            return lobbyAccountOperations.UpdateAccount(request);
        }

        public void UpdateAvatar(UpdateAvatarRequest request)
        {
            EnsureInitialized();
            lobbyAccountOperations.UpdateAvatar(request);
        }

        public CreateLobbyResponse CreateLobby(CreateLobbyRequest request)
        {
            LobbyServiceContext.ValidateRequest(request);

            int ownerId = LobbyServiceContext.Authenticate(request.Token);

            ILobbyClientCallback callback = LobbyServiceContext.GetCallbackChannel();
            LobbyCallbackRegistry.Upsert(ownerId, callback);

            EnsureInitialized();
            return lobbyRoomOperations.CreateLobby(request, ownerId, callback);
        }

        public JoinByCodeResponse JoinByCode(JoinByCodeRequest request)
        {
            LobbyServiceContext.ValidateRequest(request);

            if (string.IsNullOrWhiteSpace(request.AccessCode))
            {
                throw LobbyServiceContext.ThrowFault(LobbyServiceConstants.ERROR_INVALID_REQUEST, "AccessCode requerido.");
            }

            int userId = LobbyServiceContext.Authenticate(request.Token);

            ILobbyClientCallback callback = LobbyServiceContext.GetCallbackChannel();
            LobbyCallbackRegistry.Upsert(userId, callback);

            EnsureInitialized();
            return lobbyRoomOperations.JoinByCode(request, userId, callback);
        }

        public StartLobbyMatchResponse StartLobbyMatch(StartLobbyMatchRequest request)
        {
            EnsureInitialized();
            return lobbyMatchOperations.StartLobbyMatch(request);
        }

        internal static void ForceLogoutAndKickFromLobby(int accountId, byte sanctionType, DateTime? sanctionEndAtUtc)
        {
            LobbyKickCoordinator.ForceLogoutAndKickFromLobby(accountId, sanctionType, sanctionEndAtUtc);
        }

        private void EnsureInitialized()
        {
            if (lobbyRepository != null && lobbyRoomOperations != null && lobbyAccountOperations != null && lobbyMatchOperations != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = ResolveConnectionString(LobbyServiceConstants.MAIN_CONNECTION_STRING_NAME);
            }

            if (lobbyRepository == null)
            {
                lobbyRepository = new LobbyRepository(connectionString);
            }

            if (lobbyRoomOperations == null)
            {
                lobbyRoomOperations = new LobbyRoomOperations(lobbyRepository, callbackHub, CreateAvatarSql);
            }

            if (lobbyAccountOperations == null)
            {
                lobbyAccountOperations = new LobbyAccountOperations(lobbyRepository, CreateAvatarSql);
            }

            if (lobbyMatchOperations == null)
            {
                lobbyMatchOperations = new LobbyMatchOperations(connectionString, callbackHub, lobbyRepository, CreateAvatarSql);
            }
        }

        private UserAvatarSql CreateAvatarSql()
        {
            return new UserAvatarSql(connectionString);
        }

        private static string ResolveConnectionString(string name)
        {
            var setting = ConfigurationManager.ConnectionStrings[name];

            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                throw new InvalidOperationException(
                    string.Format("Falta connectionString '{0}' en App.config.", name));
            }

            return setting.ConnectionString;
        }
    }
}
