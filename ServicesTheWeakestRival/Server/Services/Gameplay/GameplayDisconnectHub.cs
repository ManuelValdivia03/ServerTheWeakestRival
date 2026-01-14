using log4net;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal static class GameplayDisconnectHub
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayDisconnectHub));

        private static readonly ConcurrentDictionary<string, SessionInfo> SessionById =
            new ConcurrentDictionary<string, SessionInfo>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<string, byte> HookedSessionIds =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        private sealed class SessionInfo
        {
            public Guid MatchId
            {
                get; set;
            }

            public int UserId
            {
                get; set;
            }
        }

        internal static void RegisterCurrentSession(Guid matchId, int userId, IGameplayServiceCallback callback)
        {
            if (matchId == Guid.Empty || userId <= 0 || callback == null)
            {
                return;
            }

            if (OperationContext.Current == null)
            {
                return;
            }

            string sessionId = OperationContext.Current.SessionId;

            SessionById[sessionId] = new SessionInfo
            {
                MatchId = matchId,
                UserId = userId
            };

            HookChannelOnce(sessionId);
        }

        internal static void CleanupMatch(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                return;
            }

            foreach (var kvp in SessionById)
            {
                if (kvp.Value != null && kvp.Value.MatchId == matchId)
                {
                    SessionById.TryRemove(kvp.Key, out _);
                    HookedSessionIds.TryRemove(kvp.Key, out _);
                }
            }
        }

        private static void HookChannelOnce(string sessionId)
        {
            if (!HookedSessionIds.TryAdd(sessionId, 0))
            {
                return;
            }

            IContextChannel channel = OperationContext.Current.Channel;
            if (channel == null)
            {
                return;
            }

            channel.Closed += (sender, args) => HandleSessionTerminated(sessionId);
            channel.Faulted += (sender, args) => HandleSessionTerminated(sessionId);
        }

        private static void HandleSessionTerminated(string sessionId)
        {
            try
            {
                if (!SessionById.TryRemove(sessionId, out SessionInfo info) || info == null)
                {
                    return;
                }

                HookedSessionIds.TryRemove(sessionId, out _);

                if (!GameplayMatchRegistry.TryGetMatch(info.MatchId, out MatchRuntimeState state) || state == null)
                {
                    return;
                }

                lock (state.SyncRoot)
                {
                    MatchPlayerRuntime player = state.Players.FirstOrDefault(p => p != null && p.UserId == info.UserId);
                    if (player == null || player.IsEliminated)
                    {
                        return;
                    }

                    player.IsOnline = false;
                    player.DisconnectedAtUtc = DateTime.UtcNow;
                    player.Callback = null;

                    GameplayActionsFlow.HandleDisconnectedCurrentTurnLocked(state, info.UserId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GameplayDisconnectHub.HandleSessionTerminated", ex);
            }
        }
    }
}
