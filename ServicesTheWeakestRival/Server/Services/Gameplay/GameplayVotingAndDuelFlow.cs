using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TheWeakestRival.Contracts.Enums;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplayVotingAndDuelFlow
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayVotingAndDuelFlow));

        internal static void StartVotePhase(MatchRuntimeState state)
        {
            state.ResetSurpriseExam();

            state.IsInVotePhase = true;
            state.IsInDuelPhase = false;
            state.WeakestRivalUserId = null;
            state.DuelTargetUserId = null;
            state.VotersThisRound.Clear();
            state.VotesThisRound.Clear();
            state.BombQuestionId = 0;

            state.ActiveSpecialEvent = SpecialEventType.None;

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnVotePhaseStarted(state.MatchId, TimeSpan.FromSeconds(GameplayEngineConstants.VOTE_PHASE_TIME_LIMIT_SECONDS)),
                "GameplayEngine.StartVotePhase");
        }

        internal static bool CastVoteInternal(MatchRuntimeState state, int userId, int? targetUserId)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            lock (state.SyncRoot)
            {
                if (!state.IsInVotePhase)
                {
                    throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Not in vote phase.");
                }

                HashSet<int> alivePlayers = state.Players
                    .Where(p => !p.IsEliminated)
                    .Select(p => p.UserId)
                    .ToHashSet();

                if (targetUserId.HasValue && !alivePlayers.Contains(targetUserId.Value))
                {
                    throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Target player is not in match or already eliminated.");
                }

                state.VotesThisRound[userId] = targetUserId;
                state.VotersThisRound.Add(userId);

                int alivePlayersCount = alivePlayers.Count;
                if (alivePlayersCount <= 0)
                {
                    alivePlayersCount = state.Players.Count;
                }

                if (state.VotersThisRound.Count >= alivePlayersCount)
                {
                    ResolveEliminationOrStartDuel(state);
                }

                return true;
            }
        }

        internal static void ChooseDuelOpponentInternal(MatchRuntimeState state, int userId, int targetUserId)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            lock (state.SyncRoot)
            {
                if (!state.IsInDuelPhase || !state.WeakestRivalUserId.HasValue)
                {
                    throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_DUEL_NOT_ACTIVE, GameplayEngineConstants.ERROR_DUEL_NOT_ACTIVE_MESSAGE);
                }

                if (state.WeakestRivalUserId.Value != userId)
                {
                    throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_NOT_WEAKEST_RIVAL, GameplayEngineConstants.ERROR_NOT_WEAKEST_RIVAL_MESSAGE);
                }

                HashSet<int> alivePlayers = state.Players
                    .Where(p => !p.IsEliminated)
                    .Select(p => p.UserId)
                    .ToHashSet();

                if (!alivePlayers.Contains(targetUserId))
                {
                    throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_DUEL_TARGET, GameplayEngineConstants.ERROR_INVALID_DUEL_TARGET_MESSAGE);
                }

                HashSet<int> votersAgainstWeakest = state.VotesThisRound
                    .Where(kvp => kvp.Value.HasValue && kvp.Value.Value == state.WeakestRivalUserId.Value)
                    .Select(kvp => kvp.Key)
                    .ToHashSet();

                if (!votersAgainstWeakest.Contains(targetUserId))
                {
                    throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_DUEL_TARGET, GameplayEngineConstants.ERROR_INVALID_DUEL_TARGET_MESSAGE);
                }

                state.DuelTargetUserId = targetUserId;

                int weakestIndex = state.Players.FindIndex(
                    p => p.UserId == state.WeakestRivalUserId.Value && !p.IsEliminated);

                if (weakestIndex >= 0)
                {
                    state.CurrentPlayerIndex = weakestIndex;
                    GameplayTurnFlow.SendNextQuestion(state);
                }
            }
        }

        internal static bool ShouldHandleDuelTurn(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            if (!state.IsInDuelPhase ||
                !state.WeakestRivalUserId.HasValue ||
                !state.DuelTargetUserId.HasValue)
            {
                return false;
            }

            return currentPlayer.UserId == state.WeakestRivalUserId.Value ||
                   currentPlayer.UserId == state.DuelTargetUserId.Value;
        }

        internal static void HandleDuelTurn(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, bool isCorrect)
        {
            if (!isCorrect)
            {
                currentPlayer.IsEliminated = true;

                GameplayBroadcaster.Broadcast(
                    state,
                    cb => cb.OnElimination(state.MatchId, GameplayBroadcaster.BuildPlayerSummary(currentPlayer, isOnline: true)),
                    "GameplayEngine.DuelElimination");

                state.IsInDuelPhase = false;
                state.WeakestRivalUserId = null;
                state.DuelTargetUserId = null;
                state.BombQuestionId = 0;

                GameplayMatchFlow.FinishMatchWithWinnerIfApplicable(state);
                if (state.IsFinished)
                {
                    return;
                }

                GameplayMatchFlow.StartNextRound(state);
                return;
            }

            int nextUserId = currentPlayer.UserId == state.WeakestRivalUserId.Value
                ? state.DuelTargetUserId.Value
                : state.WeakestRivalUserId.Value;

            int nextIndex = state.Players.FindIndex(p => p.UserId == nextUserId && !p.IsEliminated);
            if (nextIndex >= 0)
            {
                state.CurrentPlayerIndex = nextIndex;
                GameplayTurnFlow.SendNextQuestion(state);
            }
        }

        private static void ResolveEliminationOrStartDuel(MatchRuntimeState state)
        {
            state.IsInVotePhase = false;
            state.BombQuestionId = 0;

            GameplaySpecialEvents.EndDarkModeIfActive(state);

            List<MatchPlayerRuntime> alivePlayers = state.Players
                .Where(p => !p.IsEliminated)
                .ToList();

            if (alivePlayers.Count < GameplayEngineConstants.MIN_PLAYERS_TO_CONTINUE)
            {
                GameplayMatchFlow.FinishMatchWithWinnerIfApplicable(state);
                return;
            }

            Dictionary<int, int> voteCounts = CountVotesForAlivePlayers(state, alivePlayers);
            if (voteCounts.Count == 0)
            {
                GameplayMatchFlow.StartNextRound(state);
                return;
            }

            int? weakestRivalUserId = ResolveWeakestConsideringShield(state, voteCounts);
            if (!weakestRivalUserId.HasValue)
            {
                GameplayMatchFlow.StartNextRound(state);
                return;
            }

            MatchPlayerRuntime weakestRivalPlayer = state.Players.FirstOrDefault(p => p.UserId == weakestRivalUserId.Value);
            if (weakestRivalPlayer == null)
            {
                GameplayMatchFlow.StartNextRound(state);
                return;
            }

            CoinFlipResolvedDto coinFlip = PerformCoinFlip(state, weakestRivalUserId.Value);

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnCoinFlipResolved(state.MatchId, coinFlip),
                "GameplayEngine.CoinFlip");

            if (!coinFlip.ShouldEnableDuel)
            {
                EliminatePlayerByVoteNoDuel(state, weakestRivalPlayer);
                return;
            }

            List<DuelCandidateDto> duelCandidates = BuildDuelCandidates(state, weakestRivalUserId.Value);
            if (duelCandidates.Count == 0)
            {
                EliminatePlayerByVoteNoDuel(state, weakestRivalPlayer);
                return;
            }

            StartDuel(state, weakestRivalPlayer, weakestRivalUserId.Value, duelCandidates);
        }

        private static int? ResolveWeakestConsideringShield(MatchRuntimeState state, Dictionary<int, int> voteCounts)
        {
            while (voteCounts.Count > 0)
            {
                int maxVotes = voteCounts.Values.Max();

                List<int> candidates = voteCounts
                    .Where(kvp => kvp.Value == maxVotes)
                    .Select(kvp => kvp.Key)
                    .ToList();

                int selected = candidates.Count == 1
                    ? candidates[0]
                    : candidates[GameplayRandom.Next(0, candidates.Count)];

                MatchPlayerRuntime selectedPlayer = state.Players.FirstOrDefault(p => p != null && p.UserId == selected);
                if (selectedPlayer == null || selectedPlayer.IsEliminated)
                {
                    voteCounts.Remove(selected);
                    continue;
                }

                if (!selectedPlayer.IsShieldActive)
                {
                    return selected;
                }

                selectedPlayer.IsShieldActive = false;

                GameplayBroadcaster.Broadcast(
                    state,
                    cb => cb.OnSpecialEvent(
                        state.MatchId,
                        GameplayEngineConstants.SPECIAL_EVENT_SHIELD_TRIGGERED_CODE,
                        string.Format(CultureInfo.CurrentCulture, GameplayEngineConstants.SPECIAL_EVENT_SHIELD_TRIGGERED_DESCRIPTION_TEMPLATE, selectedPlayer.DisplayName)),
                    "GameplayEngine.Shield.Triggered");

                voteCounts.Remove(selected);
            }

            return null;
        }

        private static Dictionary<int, int> CountVotesForAlivePlayers(MatchRuntimeState state, List<MatchPlayerRuntime> alivePlayers)
        {
            Dictionary<int, int> voteCounts = new Dictionary<int, int>();

            foreach (KeyValuePair<int, int?> kvp in state.VotesThisRound)
            {
                int? targetUserId = kvp.Value;
                if (!targetUserId.HasValue)
                {
                    continue;
                }

                if (!alivePlayers.Any(p => p.UserId == targetUserId.Value))
                {
                    continue;
                }

                if (!voteCounts.TryGetValue(targetUserId.Value, out int count))
                {
                    count = 0;
                }

                voteCounts[targetUserId.Value] = count + 1;
            }

            return voteCounts;
        }

        private static void EliminatePlayerByVoteNoDuel(MatchRuntimeState state, MatchPlayerRuntime weakestRivalPlayer)
        {
            weakestRivalPlayer.IsEliminated = true;

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnElimination(state.MatchId, GameplayBroadcaster.BuildPlayerSummary(weakestRivalPlayer, isOnline: true)),
                "GameplayEngine.Elimination");

            GameplayMatchFlow.FinishMatchWithWinnerIfApplicable(state);
            if (state.IsFinished)
            {
                return;
            }

            GameplayMatchFlow.StartNextRound(state);
        }

        private static List<DuelCandidateDto> BuildDuelCandidates(MatchRuntimeState state, int weakestRivalUserId)
        {
            List<int> votersAgainstWeakest = state.VotesThisRound
                .Where(kvp => kvp.Value.HasValue && kvp.Value.Value == weakestRivalUserId)
                .Select(kvp => kvp.Key)
                .ToList();

            List<DuelCandidateDto> duelCandidates = new List<DuelCandidateDto>();

            foreach (int voterUserId in votersAgainstWeakest)
            {
                MatchPlayerRuntime player = state.Players.FirstOrDefault(p => p.UserId == voterUserId);
                if (player == null || player.IsEliminated)
                {
                    continue;
                }

                duelCandidates.Add(new DuelCandidateDto
                {
                    UserId = player.UserId,
                    DisplayName = player.DisplayName,
                    Avatar = player.Avatar
                });
            }

            return duelCandidates;
        }

        private static void StartDuel(
            MatchRuntimeState state,
            MatchPlayerRuntime weakestRivalPlayer,
            int weakestRivalUserId,
            List<DuelCandidateDto> duelCandidates)
        {
            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = weakestRivalUserId;
            state.DuelTargetUserId = null;
            state.BombQuestionId = 0;

            DuelCandidatesDto duelDto = new DuelCandidatesDto
            {
                WeakestRivalUserId = weakestRivalUserId,
                Candidates = duelCandidates.ToArray()
            };

            try
            {
                weakestRivalPlayer.Callback.OnDuelCandidates(state.MatchId, duelDto);
            }
            catch (Exception ex)
            {
                Logger.Warn("Error OnDuelCandidates.", ex);
            }
        }

        private static CoinFlipResolvedDto PerformCoinFlip(MatchRuntimeState state, int weakestRivalUserId)
        {
            int randomValue = GameplayRandom.Next(GameplayEngineConstants.COIN_FLIP_RANDOM_MIN_VALUE, GameplayEngineConstants.COIN_FLIP_RANDOM_MAX_VALUE);
            bool shouldEnableDuel = randomValue >= GameplayEngineConstants.COIN_FLIP_THRESHOLD_VALUE;

            CoinFlipResultType result = shouldEnableDuel
                ? CoinFlipResultType.Heads
                : CoinFlipResultType.Tails;

            return new CoinFlipResolvedDto
            {
                RoundId = state.RoundNumber,
                WeakestRivalPlayerId = weakestRivalUserId,
                Result = result,
                ShouldEnableDuel = shouldEnableDuel
            };
        }
    }
}
