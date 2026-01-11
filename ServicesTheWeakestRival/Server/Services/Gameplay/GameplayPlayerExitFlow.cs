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

        internal static void HandlePlayerExit(Guid matchId, int userId, PlayerExitReason reason)
        {
            if (matchId == Guid.Empty || userId <= 0)
            {
                return;
            }

            if (!GameplayMatchRegistry.TryGetMatch(matchId, out MatchRuntimeState state) || state == null)
            {
                return;
            }

            bool shouldFinish;
            bool shouldSendNextQuestion;
            PlayerSummary eliminatedSummary;
            int matchDbId;

            lock (state.SyncRoot)
            {
                if (state.IsFinished)
                {
                    return;
                }

                MatchPlayerRuntime leavingPlayer = state.Players.FirstOrDefault(p => p != null && p.UserId == userId);
                if (leavingPlayer == null)
                {
                    return;
                }

                if (leavingPlayer.IsEliminated)
                {
                    return;
                }

                MatchPlayerRuntime currentPlayer = state.GetCurrentPlayer();
                bool wasCurrentTurn = currentPlayer != null && currentPlayer.UserId == userId;

                leavingPlayer.IsEliminated = true;
                leavingPlayer.Callback = null;

                state.VotersThisRound.Remove(userId);
                state.VotesThisRound.Remove(userId);

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

                if (wasCurrentTurn)
                {
                    state.AdvanceTurn();
                }

                eliminatedSummary = GameplayBroadcaster.BuildPlayerSummary(leavingPlayer, isOnline: false);
                matchDbId = state.WildcardMatchId;

                int alivePlayers = state.Players.Count(p => p != null && !p.IsEliminated);
                shouldFinish = alivePlayers < GameplayEngineConstants.MIN_PLAYERS_TO_CONTINUE;

                shouldSendNextQuestion =
                    wasCurrentTurn &&
                    state.IsInitialized &&
                    !state.IsFinished &&
                    !state.IsInVotePhase &&
                    !state.IsInDuelPhase &&
                    !state.IsLightningActive &&
                    !state.IsSurpriseExamActive;
            }

            try
            {
                GameplayMatchRegistry.UntrackPlayerMatch(userId);
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayPlayerExitFlow.UntrackPlayerMatch failed.", ex);
            }

            try
            {
                if (matchDbId > 0)
                {
                    GameplayPlayerExitPersistence.TryMarkPlayerLeft(matchDbId, userId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GameplayPlayerExitFlow.Persistence", ex);
            }

            try
            {
                GameplayBroadcaster.Broadcast(
                    state,
                    cb => cb.OnElimination(matchId, eliminatedSummary),
                    "GameplayEngine.PlayerExit");
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayPlayerExitFlow.BroadcastElimination failed.", ex);
            }

            try
            {
                GameplayBroadcaster.BroadcastTurnOrderChanged(state, TURN_REASON_PLAYER_EXIT);
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayPlayerExitFlow.BroadcastTurnOrderChanged failed.", ex);
            }

            if (shouldFinish)
            {
                try
                {
                    GameplayMatchFlow.FinishMatchWithWinnerIfApplicable(state);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Error("GameplayPlayerExitFlow.FinishMatchWithWinnerIfApplicable", ex);
                    return;
                }
            }

            if (shouldSendNextQuestion)
            {
                try
                {
                    GameplayTurnFlow.SendNextQuestion(state);
                }
                catch (Exception ex)
                {
                    Logger.Error("GameplayPlayerExitFlow.SendNextQuestion", ex);
                }
            }
        }
    }
}
