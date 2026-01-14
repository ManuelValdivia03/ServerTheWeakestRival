using log4net;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal static class GameplayReconnectGraceFlow
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayReconnectGraceFlow));

        private static readonly ConcurrentDictionary<string, Timer> KickTimerByPlayerKey =
            new ConcurrentDictionary<string, Timer>(StringComparer.Ordinal);

        internal static void MarkPlayerDisconnected(Guid matchId, int userId)
        {
            if (matchId == Guid.Empty || userId <= 0)
            {
                return;
            }

            if (!GameplayMatchRegistry.TryGetMatch(matchId, out MatchRuntimeState state) || state == null)
            {
                return;
            }

            bool mustSkipTurn = false;

            lock (state.SyncRoot)
            {
                MatchPlayerRuntime player = state.Players.FirstOrDefault(p => p != null && p.UserId == userId);
                if (player == null || player.IsEliminated)
                {
                    return;
                }

                player.IsOnline = false;
                player.DisconnectedAtUtc = DateTime.UtcNow;

                MatchPlayerRuntime current = state.GetCurrentPlayer();
                mustSkipTurn = current != null &&
                               current.UserId == userId &&
                               !state.IsFinished;

                GameplayBroadcaster.BroadcastTurnOrderChanged(
                    state,
                    GameplayEngineConstants.TURN_REASON_PLAYER_DISCONNECTED);

                if (mustSkipTurn)
                {
                    try
                    {
                        GameplayActionsFlow.HandleDisconnectedCurrentTurnLocked(state, userId);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("HandleDisconnectedCurrentTurnLocked failed.", ex);
                    }
                }
            }

            ScheduleKick(matchId, userId);
        }

        internal static void MarkPlayerReconnected(MatchRuntimeState state, int userId)
        {
            if (state == null || userId <= 0)
            {
                return;
            }

            lock (state.SyncRoot)
            {
                MatchPlayerRuntime player = state.Players.FirstOrDefault(p => p != null && p.UserId == userId);
                if (player == null || player.IsEliminated)
                {
                    return;
                }

                player.IsOnline = true;
                player.DisconnectedAtUtc = null;

                GameplayBroadcaster.BroadcastTurnOrderChanged(
                    state,
                    GameplayEngineConstants.TURN_REASON_PLAYER_RECONNECTED);
            }

            CancelKick(state.MatchId, userId);
        }

        internal static void CleanupMatch(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                return;
            }

            string prefix = matchId.ToString("N", CultureInfo.InvariantCulture) + ":";

            foreach (var kvp in KickTimerByPlayerKey)
            {
                if (kvp.Key == null || !kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (KickTimerByPlayerKey.TryRemove(kvp.Key, out Timer timer))
                {
                    DisposeTimerSafe(timer);
                }
            }
        }

        private static void ScheduleKick(Guid matchId, int userId)
        {
            string key = BuildKey(matchId, userId);

            if (KickTimerByPlayerKey.TryRemove(key, out Timer oldTimer))
            {
                DisposeTimerSafe(oldTimer);
            }

            Timer timer = new Timer(
                KickTimerCallback,
                key,
                TimeSpan.FromSeconds(GameplayEngineConstants.RECONNECT_GRACE_SECONDS),
                Timeout.InfiniteTimeSpan);

            KickTimerByPlayerKey[key] = timer;
        }

        private static void CancelKick(Guid matchId, int userId)
        {
            string key = BuildKey(matchId, userId);

            if (KickTimerByPlayerKey.TryRemove(key, out Timer timer))
            {
                DisposeTimerSafe(timer);
            }
        }

        private static void KickTimerCallback(object stateObj)
        {
            string key = stateObj as string;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (KickTimerByPlayerKey.TryRemove(key, out Timer timer))
            {
                DisposeTimerSafe(timer);
            }

            if (!TryParseKey(key, out Guid matchId, out int userId))
            {
                return;
            }

            if (!GameplayMatchRegistry.TryGetMatch(matchId, out MatchRuntimeState state) || state == null)
            {
                return;
            }

            bool mustKick;

            lock (state.SyncRoot)
            {
                MatchPlayerRuntime player = state.Players.FirstOrDefault(p => p != null && p.UserId == userId);
                mustKick = player != null &&
                           !player.IsEliminated &&
                           !player.IsOnline &&
                           !state.IsFinished;
            }

            if (!mustKick)
            {
                return;
            }

            try
            {
                GameplayPlayerExitFlow.HandlePlayerExit(matchId, userId, PlayerExitReason.Disconnected);
            }
            catch (Exception ex)
            {
                Logger.Error("KickTimerCallback HandlePlayerExit failed.", ex);
            }
        }

        private static string BuildKey(Guid matchId, int userId)
        {
            return matchId.ToString("N", CultureInfo.InvariantCulture) +
                   ":" +
                   userId.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryParseKey(string key, out Guid matchId, out int userId)
        {
            matchId = Guid.Empty;
            userId = 0;

            string[] parts = key.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!Guid.TryParseExact(parts[0], "N", out matchId))
            {
                return false;
            }

            return int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out userId);
        }

        private static void DisposeTimerSafe(Timer timer)
        {
            if (timer == null)
            {
                return;
            }

            try
            {
                timer.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warn("DisposeTimerSafe failed.", ex);
            }
        }
    }
}
