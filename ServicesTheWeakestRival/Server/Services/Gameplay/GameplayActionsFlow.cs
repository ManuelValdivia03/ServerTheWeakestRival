using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplayActionsFlow
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayActionsFlow));

        internal static AnswerResult SubmitAnswerInternal(MatchRuntimeState state, int userId, SubmitAnswerRequest request)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            lock (state.SyncRoot)
            {
                GameplayTurnFlow.EnsureNotInVotePhase(state, "Round is in vote phase. No questions available.");

                if (state.IsSurpriseExamActive)
                {
                    return GameplaySpecialEvents.HandleSurpriseExamSubmitAnswer(state, userId, request);
                }

                MatchPlayerRuntime currentPlayer = GameplayTurnFlow.GetCurrentPlayerOrThrow(state, userId);

                if (GameplaySpecialEvents.IsLightningActive(state))
                {
                    return GameplaySpecialEvents.HandleLightningSubmitAnswer(state, currentPlayer, request);
                }

                QuestionWithAnswersDto question = GameplayTurnFlow.GetCurrentQuestionOrThrow(state);
                bool isCorrect = GameplayTurnFlow.EvaluateAnswerOrThrow(question, request.AnswerText);

                decimal chainIncrement = GameplayTurnFlow.UpdateChainState(state, currentPlayer, isCorrect);
                GameplaySpecialEvents.ApplyBombQuestionEffectIfNeeded(state, currentPlayer, isCorrect);

                AnswerResult result = GameplayTurnFlow.BuildAnswerResult(question.QuestionId, state, isCorrect, chainIncrement);

                GameplayBroadcaster.Broadcast(
                    state,
                    cb => cb.OnAnswerEvaluated(
                        state.MatchId,
                        GameplayBroadcaster.BuildPlayerSummary(currentPlayer, isOnline: true),
                        result),
                    "GameplayEngine.SubmitAnswerInternal");

                if (state.IsInFinalPhase)
                {
                    GameplayMatchFlow.ProcessFinalAnswerAndContinue(state, currentPlayer.UserId, isCorrect);
                    return result;
                }

                if (GameplayVotingAndDuelFlow.ShouldHandleDuelTurn(state, currentPlayer))
                {
                    GameplayVotingAndDuelFlow.HandleDuelTurn(state, currentPlayer, isCorrect);
                    return result;
                }

                state.QuestionsAskedThisRound++;

                int alivePlayersCount = GameplayTurnFlow.CountAlivePlayersOrFallbackToTotal(state);
                int maxQuestionsThisRound = alivePlayersCount * GameplayEngineConstants.QUESTIONS_PER_PLAYER_PER_ROUND;
                bool hasNoMoreQuestions = state.Questions.Count == 0;

                if (state.QuestionsAskedThisRound >= maxQuestionsThisRound || hasNoMoreQuestions)
                {
                    if (alivePlayersCount == GameplayEngineConstants.FINAL_PLAYERS_COUNT)
                    {
                        GameplayMatchFlow.StartFinalPhaseIfApplicable(state);
                        return result;
                    }

                    GameplayVotingAndDuelFlow.StartVotePhase(state);
                    return result;
                }

                state.AdvanceTurn();
                GameplayTurnFlow.SendNextQuestion(state);

                return result;
            }
        }

        internal static BankState BankInternal(MatchRuntimeState state, int userId)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            lock (state.SyncRoot)
            {
                GameplayTurnFlow.EnsureNotInVotePhase(state, "Round is in vote phase. Banking is not allowed.");

                if (state.IsSurpriseExamActive)
                {
                    throw GameplayFaults.ThrowFault(
                        GameplayEngineConstants.ERROR_INVALID_REQUEST,
                        GameplayEngineConstants.SURPRISE_EXAM_BANKING_NOT_ALLOWED_MESSAGE);
                }

                GameplayTurnFlow.GetCurrentPlayerOrThrow(state, userId);

                state.BankedPoints += state.CurrentChain;
                state.CurrentChain = 0m;
                state.CurrentStreak = 0;

                BankState bankState = new BankState
                {
                    MatchId = state.MatchId,
                    CurrentChain = state.CurrentChain,
                    BankedPoints = state.BankedPoints
                };

                GameplayBroadcaster.Broadcast(
                    state,
                    cb => cb.OnBankUpdated(state.MatchId, bankState),
                    "GameplayEngine.BankInternal");

                return bankState;
            }
        }

        internal static bool CastVoteInternal(MatchRuntimeState state, int userId, int? targetUserId)
        {
            return GameplayVotingAndDuelFlow.CastVoteInternal(state, userId, targetUserId);
        }

        internal static void ChooseDuelOpponentInternal(MatchRuntimeState state, int userId, int targetUserId)
        {
            GameplayVotingAndDuelFlow.ChooseDuelOpponentInternal(state, userId, targetUserId);
        }

        internal static int ApplyWildcardFromDbOrThrow(int wildcardMatchId, int userId, string wildcardCode, int clientRoundNumber)
        {
            MatchRuntimeState state = GameplayMatchRegistry.GetMatchByWildcardDbIdOrThrow(wildcardMatchId);

            lock (state.SyncRoot)
            {
                int serverRoundNumber = state.RoundNumber;

                int effectiveRoundNumber = clientRoundNumber;
                if (effectiveRoundNumber != serverRoundNumber)
                {
                    Logger.WarnFormat(
                        "ApplyWildcardFromDbOrThrow: round mismatch. MatchId={0}, UserId={1}, Code={2}, ClientRound={3}, ServerRound={4}. Using server round.",
                        wildcardMatchId,
                        userId,
                        wildcardCode ?? string.Empty,
                        effectiveRoundNumber,
                        serverRoundNumber);

                    effectiveRoundNumber = serverRoundNumber;
                }

                if (state.IsInVotePhase || state.IsInDuelPhase || state.IsSurpriseExamActive || GameplaySpecialEvents.IsLightningActive(state) || state.IsInFinalPhase)
                {
                    throw GameplayFaults.ThrowFault(
                        GameplayEngineConstants.ERROR_WILDCARD_INVALID_TIMING,
                        GameplayEngineConstants.ERROR_WILDCARD_INVALID_TIMING_MESSAGE);
                }

                MatchPlayerRuntime actor = state.Players.FirstOrDefault(p => p != null && p.UserId == userId);
                if (actor == null || actor.IsEliminated)
                {
                    throw GameplayFaults.ThrowFault(
                        GameplayEngineConstants.ERROR_INVALID_REQUEST,
                        "Player not in match or eliminated.");
                }

                if (actor.BlockWildcardsRoundNumber == state.RoundNumber)
                {
                    throw GameplayFaults.ThrowFault(
                        GameplayEngineConstants.ERROR_WILDCARDS_BLOCKED,
                        GameplayEngineConstants.ERROR_WILDCARDS_BLOCKED_MESSAGE);
                }

                MatchPlayerRuntime current = GameplayTurnFlow.GetCurrentPlayerOrThrow(state, userId);

                string normalizedCode = (wildcardCode ?? string.Empty).Trim().ToUpperInvariant();
                GameplayWildcardsFlow.ApplyWildcardLocked(state, current, normalizedCode);

                GameplayWildcardsFlow.BroadcastWildcardUsed(state, current, normalizedCode);

                return state.RoundNumber;
            }
        }
    }
}