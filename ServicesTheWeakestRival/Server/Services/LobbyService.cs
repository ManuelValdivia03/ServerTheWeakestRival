using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class LobbyService : ILobbyService
    {
        private static readonly ConcurrentDictionary<Guid, ILobbyClientCallback> Callbacks =
            new ConcurrentDictionary<Guid, ILobbyClientCallback>();

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
            ILobbyClientCallback _; Callbacks.TryRemove(request.LobbyId, out _);
        }

        public ListLobbiesResponse ListLobbies(ListLobbiesRequest request) =>
            new ListLobbiesResponse { Lobbies = new List<LobbyInfo>() };

        public void SendChatMessage(SendLobbyMessageRequest request)
        {
            ILobbyClientCallback cb;
            if (Callbacks.TryGetValue(request.LobbyId, out cb))
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
    }
}
