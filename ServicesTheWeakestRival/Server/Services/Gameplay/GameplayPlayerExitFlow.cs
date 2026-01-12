using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Linq;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal static class GameplayPlayerExitFlow
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayPlayerExitFlow));

        private const string TURN_REASON_PLAYER_EXIT = "PLAYER_EXIT";

        private const string CTX_UNTRACK_PLAYER = "GameplayPlayerExitFlow.UntrackPlayerMatch";
        private const string CTX_PERSISTENCE = "GameplayPlayerExitFlow.Persistence";
        private const string CTX_BROADCAST_ELIMINATION = "GameplayPlayerExitFlow.BroadcastElimination";
        private const string CTX_BROADCAST_TURN_ORDER = "GameplayPlayerExitFlow.BroadcastTurnOrderChanged";
        private const string CTX_FINISH_MATCH = "GameplayPlayerExitFlow.FinishMatchWithWinnerIfApplicable";
        private const string CTX_SEND_NEXT_QUESTION = "GameplayPlayerExitFlow.SendNextQuestion";

        internal static void HandlePlayerExit(Guid matchId, int userId, PlayerExitReason reason)
        {
            if (!IsValidExitRequest(matchId, userId))
            {
                return;
            }

            if (!TryGetMatch(matchId, out MatchRuntimeState state))
            {
                return;
            }

            PlayerExitOutcome outcome = ApplyExitLocked(state, matchId, userId);
            if (!outcome.Applied)
            {
                return;
            }

            TryUntrackPlayer(userId);

            TryPersistPlayerLeft(outcome.MatchDbId, userId);

            TryBroadcastElimination(state, matchId, outcome.EliminatedSummary);

            TryBroadcastTurnOrderChanged(state);

            TryFinishMatchOrSendNextQuestion(state, outcome);
        }

        private static bool IsValidExitRequest(Guid matchId, int userId)
        {
            return matchId != Guid.Empty && userId > 0;
        }

        private static bool TryGetMatch(Guid matchId, out MatchRuntimeState state)
        {
            state = null;

            if (!GameplayMatchRegistry.TryGetMatch(matchId, out MatchRuntimeState matchState) || matchState == null)
            {
                return false;
            }

            state = matchState;
            return true;
        }

        private static PlayerExitOutcome ApplyExitLocked(MatchRuntimeState state, Guid matchId, int userId)
        {
            lock (state.SyncRoot)
            {
                if (state.IsFinished)
                {
                    return PlayerExitOutcome.NotApplied();
                }

                MatchPlayerRuntime leavingPlayer = state.Players.FirstOrDefault(p => p != null && p.UserId == userId);
                if (leavingPlayer == null || leavingPlayer.IsEliminated)
                {
                    return PlayerExitOutcome.NotApplied();
                }

                bool wasCurrentTurn = IsCurrentTurn(state, userId);

                EliminatePlayer(state, leavingPlayer);

                ClearVoteAndDuelStateForLeavingPlayer(state, userId);

                if (wasCurrentTurn)
                {
                    state.AdvanceTurn();
                }

                int alivePlayers = state.Players.Count(p => p != null && !p.IsEliminated);

                return PlayerExitOutcome.AppliedOutcome(
                    eliminatedSummary: GameplayBroadcaster.BuildPlayerSummary(leavingPlayer, isOnline: false),
                    matchDbId: state.WildcardMatchId,
                    shouldFinish: alivePlayers < GameplayEngineConstants.MIN_PLAYERS_TO_CONTINUE,
                    shouldSendNextQuestion: ShouldSendNextQuestion(state, wasCurrentTurn));
            }
        }

        private static bool IsCurrentTurn(MatchRuntimeState state, int userId)
        {
            MatchPlayerRuntime currentPlayer = state.GetCurrentPlayer();
            return currentPlayer != null && currentPlayer.UserId == userId;
        }

        private static void EliminatePlayer(MatchRuntimeState state, MatchPlayerRuntime leavingPlayer)
        {
            leavingPlayer.IsEliminated = true;
            leavingPlayer.Callback = null;

            state.VotersThisRound.Remove(leavingPlayer.UserId);
            state.VotesThisRound.Remove(leavingPlayer.UserId);
        }

        private static void ClearVoteAndDuelStateForLeavingPlayer(MatchRuntimeState state, int userId)
        {
            if (state.WeakestRivalUserId.HasValue && state.WeakestRivalUserId.Value == userId)
            {
                state.WeakestRivalUserId = null;
            }

            if (state.DuelTargetUserId.HasValue && state.DuelTargetUserId.Value == userId)
            {
                state.DuelTargetUserId = null;
            }

            if (state.IsInDuelPhase &&
                (!state.WeakestRivalUserId.HasValue || !state.DuelTargetUserId.HasValue))
            {
                state.IsInDuelPhase = false;
            }
        }

        private static bool ShouldSendNextQuestion(MatchRuntimeState state, bool wasCurrentTurn)
        {
            return wasCurrentTurn &&
                   state.IsInitialized &&
                   !state.IsFinished &&
                   !state.IsInVotePhase &&
                   !state.IsInDuelPhase &&
                   !state.IsLightningActive &&
                   !state.IsSurpriseExamActive;
        }

        private static void TryUntrackPlayer(int userId)
        {
            try
            {
                GameplayMatchRegistry.UntrackPlayerMatch(userId);
            }
            catch (Exception ex)
            {
                Logger.Warn(CTX_UNTRACK_PLAYER, ex);
            }
        }

        private static void TryPersistPlayerLeft(int matchDbId, int userId)
        {
            if (matchDbId <= 0)
            {
                return;
            }

            try
            {
                GameplayPlayerExitPersistence.TryMarkPlayerLeft(matchDbId, userId);
            }
            catch (Exception ex)
            {
                Logger.Error(CTX_PERSISTENCE, ex);
            }
        }

        private static void TryBroadcastElimination(MatchRuntimeState state, Guid matchId, PlayerSummary eliminatedSummary)
        {
            try
            {
                GameplayBroadcaster.Broadcast(
                    state,
                    cb => cb.OnElimination(matchId, eliminatedSummary),
                    "GameplayEngine.PlayerExit");
            }
            catch (Exception ex)
            {
                Logger.Warn(CTX_BROADCAST_ELIMINATION, ex);
            }
        }

        private static void TryBroadcastTurnOrderChanged(MatchRuntimeState state)
        {
            try
            {
                GameplayBroadcaster.BroadcastTurnOrderChanged(state, TURN_REASON_PLAYER_EXIT);
            }
            catch (Exception ex)
            {
                Logger.Warn(CTX_BROADCAST_TURN_ORDER, ex);
            }
        }

        private static void TryFinishMatchOrSendNextQuestion(MatchRuntimeState state, PlayerExitOutcome outcome)
        {
            if (outcome.ShouldFinish)
            {
                try
                {
                    GameplayMatchFlow.FinishMatchWithWinnerIfApplicable(state);
                }
                catch (Exception ex)
                {
                    Logger.Error(CTX_FINISH_MATCH, ex);
                }

                return;
            }

            if (!outcome.ShouldSendNextQuestion)
            {
                return;
            }

            try
            {
                GameplayTurnFlow.SendNextQuestion(state);
            }
            catch (Exception ex)
            {
                Logger.Error(CTX_SEND_NEXT_QUESTION, ex);
            }
        }

        private sealed class PlayerExitOutcome
        {
            private PlayerExitOutcome()
            {
            }

            internal bool Applied { get; private set; }
            internal bool ShouldFinish { get; private set; }
            internal bool ShouldSendNextQuestion { get; private set; }
            internal PlayerSummary EliminatedSummary { get; private set; }
            internal int MatchDbId { get; private set; }

            internal static PlayerExitOutcome NotApplied()
            {
                return new PlayerExitOutcome
                {
                    Applied = false,
                    ShouldFinish = false,
                    ShouldSendNextQuestion = false,
                    EliminatedSummary = null,
                    MatchDbId = 0
                };
            }

            internal static PlayerExitOutcome AppliedOutcome(
                PlayerSummary eliminatedSummary,
                int matchDbId,
                bool shouldFinish,
                bool shouldSendNextQuestion)
            {
                if (eliminatedSummary == null) throw new ArgumentNullException(nameof(eliminatedSummary));

                return new PlayerExitOutcome
                {
                    Applied = true,
                    ShouldFinish = shouldFinish,
                    ShouldSendNextQuestion = shouldSendNextQuestion,
                    EliminatedSummary = eliminatedSummary,
                    MatchDbId = matchDbId
                };
            }
        }
    }
}
