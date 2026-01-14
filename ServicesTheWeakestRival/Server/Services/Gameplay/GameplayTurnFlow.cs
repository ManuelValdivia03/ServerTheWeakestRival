using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Globalization;
using System.Linq;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplayTurnFlow
    {
        internal static void EnsureNotInVotePhase(MatchRuntimeState state, string message)
        {
            if (state.IsInVotePhase)
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, message);
            }
        }

        internal static MatchPlayerRuntime GetCurrentPlayerOrThrow(MatchRuntimeState state, int userId)
        {
            MatchPlayerRuntime currentPlayer = state.GetCurrentPlayer();
            if (currentPlayer == null || currentPlayer.UserId != userId)
            {
                throw GameplayFaults.ThrowFault(
                    GameplayEngineConstants.ERROR_NOT_PLAYER_TURN,
                    GameplayEngineConstants.ERROR_NOT_PLAYER_TURN_MESSAGE);
            }

            return currentPlayer;
        }

        internal static QuestionWithAnswersDto GetCurrentQuestionOrThrow(MatchRuntimeState state)
        {
            if (!state.QuestionsById.TryGetValue(state.CurrentQuestionId, out QuestionWithAnswersDto question))
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Current question not found for this match.");
            }

            return question;
        }

        internal static bool EvaluateAnswerOrThrow(QuestionWithAnswersDto question, string answerText)
        {
            if (string.IsNullOrWhiteSpace(answerText))
            {
                return false;
            }

            string safeAnswer = answerText.Trim();

            AnswerDto selectedAnswer = question.Answers.Find(a =>
                string.Equals(a.Text, safeAnswer, StringComparison.Ordinal));

            if (selectedAnswer == null)
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Answer not found for current question.");
            }

            return selectedAnswer.IsCorrect;
        }

        internal static decimal UpdateChainState(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, bool isCorrect)
        {
            if (!isCorrect)
            {
                state.CurrentChain = 0m;
                state.CurrentStreak = 0;

                currentPlayer.IsDoublePointsActive = false;

                return 0m;
            }

            if (state.CurrentStreak >= GameplayEngineConstants.CHAIN_STEPS.Length)
            {
                currentPlayer.IsDoublePointsActive = false;
                return 0m;
            }

            decimal baseIncrement = GameplayEngineConstants.CHAIN_STEPS[state.CurrentStreak];
            decimal increment = currentPlayer.IsDoublePointsActive
                ? baseIncrement + baseIncrement
                : baseIncrement;

            state.CurrentChain += increment;
            state.CurrentStreak++;

            currentPlayer.IsDoublePointsActive = false;

            return increment;
        }

        internal static AnswerResult BuildAnswerResult(int questionId, MatchRuntimeState state, bool isCorrect, decimal chainIncrement)
        {
            return new AnswerResult
            {
                QuestionId = questionId,
                IsCorrect = isCorrect,
                ChainIncrement = chainIncrement,
                CurrentChain = state.CurrentChain,
                BankedPoints = state.BankedPoints
            };
        }

        internal static int CountAlivePlayersOrFallbackToTotal(MatchRuntimeState state)
        {
            int alivePlayersCount = state.Players.Count(p => p != null && !p.IsEliminated && p.IsOnline);
            if (alivePlayersCount > 0)
            {
                return alivePlayersCount;
            }

            int aliveCount = state.Players.Count(p => p != null && !p.IsEliminated);
            return aliveCount > 0 ? aliveCount : state.Players.Count;

        }

        internal static void SendNextQuestion(MatchRuntimeState state)
        {
            if (state.Questions.Count == 0)
            {
                return;
            }

            QuestionWithAnswersDto question = state.Questions.Dequeue();
            state.CurrentQuestionId = question.QuestionId;

            MatchPlayerRuntime targetPlayer = state.GetCurrentPlayer();
            if (targetPlayer == null)
            {
                return;
            }

            state.BombQuestionId = 0;
            GameplaySpecialEvents.TryStartBombQuestionEvent(state, targetPlayer, question.QuestionId);
            GameplayBroadcaster.NotifyAndClearPendingTimeDeltaIfAny(state, targetPlayer);

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnNextQuestion(
                    state.MatchId,
                    GameplayBroadcaster.BuildPlayerSummary(targetPlayer, isOnline: true),
                    question,
                    state.CurrentChain,
                    state.BankedPoints),
                "GameplayEngine.SendNextQuestion");
        }
    }
}
