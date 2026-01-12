using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplayAuth
    {
        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        internal static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw GameplayFaults.ThrowFault("AUTH_REQUIRED", "Missing token.");
            }

            if (!TokenCache.TryGetValue(token, out AuthToken authToken))
            {
                throw GameplayFaults.ThrowFault("AUTH_INVALID", "Invalid token.");
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw GameplayFaults.ThrowFault("AUTH_EXPIRED", "Token expired.");
            }

            return authToken.UserId;
        }
    }

    internal static class GameplayRandom
    {
        private static readonly object SyncRoot = new object();
        private static readonly Random RandomGenerator = new Random();

        internal static int Next(int minInclusive, int maxExclusive)
        {
            lock (SyncRoot)
            {
                return RandomGenerator.Next(minInclusive, maxExclusive);
            }
        }
    }

    internal static class GameplayMatchRegistry
    {
        private static readonly ConcurrentDictionary<Guid, MatchRuntimeState> Matches =
            new ConcurrentDictionary<Guid, MatchRuntimeState>();

        private static readonly ConcurrentDictionary<int, Guid> RuntimeMatchByWildcardMatchId =
            new ConcurrentDictionary<int, Guid>();

        private static readonly ConcurrentDictionary<int, Guid> PlayerMatchByUserId =
            new ConcurrentDictionary<int, Guid>();

        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>> ExpectedPlayersByMatchId =
            new ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>>();

        internal static MatchRuntimeState GetOrCreateMatch(Guid matchId)
        {
            return Matches.GetOrAdd(matchId, id => new MatchRuntimeState(id));
        }

        internal static bool TryGetMatch(Guid matchId, out MatchRuntimeState state)
        {
            return Matches.TryGetValue(matchId, out state);
        }

        internal static MatchRuntimeState GetMatchOrThrow(Guid matchId)
        {
            if (!Matches.TryGetValue(matchId, out MatchRuntimeState state))
            {
                throw GameplayFaults.ThrowFault(
                    GameplayEngineConstants.ERROR_MATCH_NOT_FOUND,
                    GameplayEngineConstants.ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            return state;
        }

        internal static MatchRuntimeState GetMatchByWildcardDbIdOrThrow(int wildcardMatchId)
        {
            if (wildcardMatchId <= 0)
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "WildcardMatchId inválido.");
            }

            if (RuntimeMatchByWildcardMatchId.TryGetValue(wildcardMatchId, out Guid runtimeMatchId))
            {
                return GetMatchOrThrow(runtimeMatchId);
            }

            MatchRuntimeState fallback = Matches.Values.FirstOrDefault(
                s => s != null && s.WildcardMatchId == wildcardMatchId);

            if (fallback == null)
            {
                throw GameplayFaults.ThrowFault(
                    GameplayEngineConstants.ERROR_MATCH_NOT_FOUND,
                    GameplayEngineConstants.ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            RuntimeMatchByWildcardMatchId.TryAdd(wildcardMatchId, fallback.MatchId);
            return fallback;
        }

        internal static Guid ResolveMatchIdForUserOrThrow(int userId)
        {
            if (!PlayerMatchByUserId.TryGetValue(userId, out Guid matchId))
            {
                throw GameplayFaults.ThrowFault(
                    GameplayEngineConstants.ERROR_MATCH_NOT_FOUND,
                    GameplayEngineConstants.ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            return matchId;
        }

        internal static void TrackPlayerMatch(int userId, Guid matchId)
        {
            PlayerMatchByUserId[userId] = matchId;
        }

        internal static void MapWildcardMatchId(int wildcardMatchId, Guid runtimeMatchId)
        {
            if (wildcardMatchId > 0)
            {
                RuntimeMatchByWildcardMatchId[wildcardMatchId] = runtimeMatchId;
            }
        }

        internal static bool TryGetExpectedPlayers(Guid matchId, out ConcurrentDictionary<int, byte> expectedPlayers)
        {
            return ExpectedPlayersByMatchId.TryGetValue(matchId, out expectedPlayers);
        }

        internal static void StoreOrMergeExpectedPlayers(Guid matchId, int[] expectedPlayerUserIds, int callerUserId)
        {
            if (matchId == Guid.Empty)
            {
                return;
            }

            ConcurrentDictionary<int, byte> set = ExpectedPlayersByMatchId.GetOrAdd(
                matchId,
                _ => new ConcurrentDictionary<int, byte>());

            if (expectedPlayerUserIds != null)
            {
                foreach (int id in expectedPlayerUserIds)
                {
                    if (id > 0)
                    {
                        set.TryAdd(id, 0);
                    }
                }
            }

            if (callerUserId > 0)
            {
                set.TryAdd(callerUserId, 0);
            }
        }

        internal static void CleanupFinishedMatch(MatchRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.WildcardMatchId > 0)
            {
                RuntimeMatchByWildcardMatchId.TryRemove(state.WildcardMatchId, out _);
            }

            Matches.TryRemove(state.MatchId, out _);
            ExpectedPlayersByMatchId.TryRemove(state.MatchId, out _);

            foreach (MatchPlayerRuntime player in state.Players)
            {
                if (player != null)
                {
                    PlayerMatchByUserId.TryRemove(player.UserId, out _);
                }
            }
        }

        internal static void UntrackPlayerMatch(int userId)
        {
            if (userId <= 0)
            {
                return;
            }

            PlayerMatchByUserId.TryRemove(userId, out _);
        }
    }

    internal static class GameplayBroadcaster
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayBroadcaster));

        internal static PlayerSummary BuildPlayerSummary(MatchPlayerRuntime player, bool isOnline)
        {
            return new PlayerSummary
            {
                UserId = player.UserId,
                DisplayName = player.DisplayName,
                IsOnline = isOnline,
                Avatar = player.Avatar
            };
        }

        internal static void Broadcast(MatchRuntimeState state, Action<IGameplayServiceCallback> action, string logContext)
        {
            foreach (MatchPlayerRuntime player in state.Players)
            {
                if (player == null || player.Callback == null)
                {
                    continue;
                }

                try
                {
                    action(player.Callback);
                }
                catch (Exception ex)
                {
                    Logger.WarnFormat(
                        "{0}: callback failed. PlayerUserId={1}",
                        logContext,
                        player.UserId);

                    Logger.Warn(logContext, ex);
                }
            }
        }

        internal static TurnOrderDto BuildTurnOrderDto(MatchRuntimeState state)
        {
            int[] orderedAliveUserIds = state.Players
                .Where(p => p != null && !p.IsEliminated)
                .Select(p => p.UserId)
                .ToArray();

            MatchPlayerRuntime current = state.GetCurrentPlayer();

            return new TurnOrderDto
            {
                OrderedAliveUserIds = orderedAliveUserIds,
                CurrentTurnUserId = current != null ? current.UserId : GameplayEngineConstants.TURN_USER_ID_NONE,
                ServerUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        internal static void BroadcastTurnOrderInitialized(MatchRuntimeState state)
        {
            TurnOrderDto dto = BuildTurnOrderDto(state);

            Broadcast(
                state,
                cb => cb.OnTurnOrderInitialized(state.MatchId, dto),
                "GameplayEngine.TurnOrder");
        }

        internal static void BroadcastTurnOrderChanged(MatchRuntimeState state, string reasonCode)
        {
            TurnOrderDto dto = BuildTurnOrderDto(state);

            Broadcast(
                state,
                cb => cb.OnTurnOrderChanged(state.MatchId, dto, reasonCode ?? string.Empty),
                "GameplayEngine.TurnOrderChanged");
        }

        internal static void NotifyAndClearPendingTimeDeltaIfAny(MatchRuntimeState state, MatchPlayerRuntime targetPlayer)
        {
            if (state == null || targetPlayer == null || targetPlayer.Callback == null)
            {
                return;
            }

            int deltaSeconds = targetPlayer.PendingTimeDeltaSeconds;
            if (deltaSeconds == 0)
            {
                return;
            }

            targetPlayer.PendingTimeDeltaSeconds = 0;

            string reasonCode = GameplayEngineConstants.TURN_REASON_TIME_DELTA_PREFIX +
                                deltaSeconds.ToString(CultureInfo.InvariantCulture);

            try
            {
                targetPlayer.Callback.OnTurnOrderChanged(state.MatchId, BuildTurnOrderDto(state), reasonCode);
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayEngine.TimeDelta callback failed.", ex);
            }
        }

        internal static void TrySendSnapshotToJoiningPlayer(MatchRuntimeState state, int userId)
        {
            if (state == null)
            {
                return;
            }

            MatchPlayerRuntime joiningPlayer = state.Players.FirstOrDefault(p => p.UserId == userId);
            if (joiningPlayer == null || joiningPlayer.Callback == null)
            {
                return;
            }

            try
            {
                if (state.IsDarkModeActive)
                {
                    joiningPlayer.Callback.OnSpecialEvent(
                        state.MatchId,
                        GameplayEngineConstants.SPECIAL_EVENT_DARK_MODE_STARTED_CODE,
                        GameplayEngineConstants.SPECIAL_EVENT_DARK_MODE_STARTED_DESCRIPTION);
                }

                joiningPlayer.Callback.OnTurnOrderInitialized(state.MatchId, BuildTurnOrderDto(state));

                if (state.IsSurpriseExamActive)
                {
                    SurpriseExamState exam = state.SurpriseExam;

                    if (exam != null &&
                        exam.QuestionIdByUserId.TryGetValue(userId, out int examQuestionId) &&
                        state.QuestionsById.TryGetValue(examQuestionId, out QuestionWithAnswersDto examQuestion) &&
                        !exam.IsCorrectByUserId.ContainsKey(userId))
                    {
                        joiningPlayer.Callback.OnSpecialEvent(
                            state.MatchId,
                            GameplayEngineConstants.SPECIAL_EVENT_SURPRISE_EXAM_STARTED_CODE,
                            GameplayEngineConstants.SPECIAL_EVENT_SURPRISE_EXAM_STARTED_DESCRIPTION);

                        joiningPlayer.Callback.OnNextQuestion(
                            state.MatchId,
                            BuildPlayerSummary(joiningPlayer, isOnline: true),
                            examQuestion,
                            state.CurrentChain,
                            state.BankedPoints);

                        return;
                    }
                }

                if (state.IsInVotePhase)
                {
                    joiningPlayer.Callback.OnVotePhaseStarted(
                        state.MatchId,
                        TimeSpan.FromSeconds(GameplayEngineConstants.VOTE_PHASE_TIME_LIMIT_SECONDS));
                    return;
                }

                MatchPlayerRuntime current = state.GetCurrentPlayer();

                if (current != null &&
                    state.CurrentQuestionId > 0 &&
                    state.QuestionsById.TryGetValue(state.CurrentQuestionId, out QuestionWithAnswersDto question))
                {
                    joiningPlayer.Callback.OnNextQuestion(
                        state.MatchId,
                        BuildPlayerSummary(current, isOnline: true),
                        question,
                        state.CurrentChain,
                        state.BankedPoints);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("TrySendSnapshotToJoiningPlayer failed.", ex);
            }
        }
    }
}
