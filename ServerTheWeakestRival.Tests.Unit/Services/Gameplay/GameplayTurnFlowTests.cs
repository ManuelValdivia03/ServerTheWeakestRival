using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayTurnFlowTests
    {
        private const int USER_ID_1 = 1;
        private const int USER_ID_2 = 2;

        [TestMethod]
        public void EnsureNotInVotePhase_IsInVotePhase_ThrowsInvalidRequest()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid())
            {
                IsInVotePhase = true
            };

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayTurnFlow.EnsureNotInVotePhase(state, "No questions."));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void EnsureNotInVotePhase_NotInVotePhase_DoesNotThrow()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid())
            {
                IsInVotePhase = false
            };

            GameplayTurnFlow.EnsureNotInVotePhase(state, "No questions.");
        }

        [TestMethod]
        public void GetCurrentPlayerOrThrow_CurrentPlayerNull_ThrowsNotPlayerTurn()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid());

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayTurnFlow.GetCurrentPlayerOrThrow(state, USER_ID_1));

            Assert.AreEqual(GameplayEngineConstants.ERROR_NOT_PLAYER_TURN, ex.Detail.Code);
        }

        [TestMethod]
        public void GetCurrentPlayerOrThrow_UserIdMismatch_ThrowsNotPlayerTurn()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid());
            state.Players.Add(new MatchPlayerRuntime(USER_ID_1, "P1", callback: null));
            state.CurrentPlayerIndex = 0;

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayTurnFlow.GetCurrentPlayerOrThrow(state, USER_ID_2));

            Assert.AreEqual(GameplayEngineConstants.ERROR_NOT_PLAYER_TURN, ex.Detail.Code);
        }

        [TestMethod]
        public void GetCurrentPlayerOrThrow_UserIdMatches_ReturnsPlayer()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid());
            MatchPlayerRuntime expected = new MatchPlayerRuntime(USER_ID_1, "P1", callback: null);

            state.Players.Add(expected);
            state.CurrentPlayerIndex = 0;

            MatchPlayerRuntime actual = GameplayTurnFlow.GetCurrentPlayerOrThrow(state, USER_ID_1);

            Assert.AreSame(expected, actual);
        }

        [TestMethod]
        public void GetCurrentQuestionOrThrow_CurrentQuestionMissing_ThrowsInvalidRequest()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid())
            {
                CurrentQuestionId = 99
            };

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayTurnFlow.GetCurrentQuestionOrThrow(state));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void GetCurrentQuestionOrThrow_QuestionExists_ReturnsQuestion()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid());
            QuestionWithAnswersDto q = BuildQuestion(questionId: 10, correctText: "A", wrongText: "B");

            state.QuestionsById[q.QuestionId] = q;
            state.CurrentQuestionId = q.QuestionId;

            QuestionWithAnswersDto actual = GameplayTurnFlow.GetCurrentQuestionOrThrow(state);

            Assert.AreSame(q, actual);
        }

        [TestMethod]
        public void EvaluateAnswerOrThrow_AnswerNullOrWhitespace_ReturnsFalse()
        {
            QuestionWithAnswersDto q = BuildQuestion(questionId: 1, correctText: "A", wrongText: "B");

            bool result1 = GameplayTurnFlow.EvaluateAnswerOrThrow(q, null);
            bool result2 = GameplayTurnFlow.EvaluateAnswerOrThrow(q, "   ");

            Assert.IsFalse(result1);
            Assert.IsFalse(result2);
        }

        [TestMethod]
        public void EvaluateAnswerOrThrow_AnswerWithWhitespace_TrimmedAndEvaluated()
        {
            QuestionWithAnswersDto q = BuildQuestion(questionId: 1, correctText: "A", wrongText: "B");

            bool isCorrect = GameplayTurnFlow.EvaluateAnswerOrThrow(q, "  A  ");

            Assert.IsTrue(isCorrect);
        }

        [TestMethod]
        public void EvaluateAnswerOrThrow_AnswerNotFound_ThrowsInvalidRequest()
        {
            QuestionWithAnswersDto q = new QuestionWithAnswersDto
            {
                QuestionId = 1,
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { AnswerId = 1, Text = "A", IsCorrect = true, DisplayOrder = 1 }
                }
            };

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayTurnFlow.EvaluateAnswerOrThrow(q, "B"));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void EvaluateAnswerOrThrow_MatchingAnswerIncorrect_ReturnsFalse()
        {
            QuestionWithAnswersDto q = BuildQuestion(questionId: 1, correctText: "A", wrongText: "B");

            bool isCorrect = GameplayTurnFlow.EvaluateAnswerOrThrow(q, "B");

            Assert.IsFalse(isCorrect);
        }

        [TestMethod]
        public void EvaluateAnswerOrThrow_MatchingAnswerCorrect_ReturnsTrue()
        {
            QuestionWithAnswersDto q = BuildQuestion(questionId: 1, correctText: "A", wrongText: "B");

            bool isCorrect = GameplayTurnFlow.EvaluateAnswerOrThrow(q, "A");

            Assert.IsTrue(isCorrect);
        }

        [TestMethod]
        public void UpdateChainState_Incorrect_ResetsChainStreakAndDoublePoints()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid())
            {
                CurrentChain = 1.23m,
                CurrentStreak = 3
            };

            MatchPlayerRuntime player = new MatchPlayerRuntime(USER_ID_1, "P1", callback: null)
            {
                IsDoublePointsActive = true
            };

            decimal inc = GameplayTurnFlow.UpdateChainState(state, player, isCorrect: false);

            Assert.AreEqual(0m, inc);
            Assert.AreEqual(0m, state.CurrentChain);
            Assert.AreEqual(0, state.CurrentStreak);
            Assert.IsFalse(player.IsDoublePointsActive);
        }

        [TestMethod]
        public void UpdateChainState_Correct_FirstStep_IncrementsChainAndStreak()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid());

            MatchPlayerRuntime player = new MatchPlayerRuntime(USER_ID_1, "P1", callback: null);

            decimal inc = GameplayTurnFlow.UpdateChainState(state, player, isCorrect: true);

            Assert.AreEqual(GameplayEngineConstants.CHAIN_STEPS[0], inc);
            Assert.AreEqual(1, state.CurrentStreak);
            Assert.AreEqual(inc, state.CurrentChain);
            Assert.IsFalse(player.IsDoublePointsActive);
        }

        [TestMethod]
        public void UpdateChainState_DoublePoints_DoublesIncrementAndResetsFlag()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid());
            MatchPlayerRuntime player = new MatchPlayerRuntime(userId: USER_ID_1, displayName: "P", callback: null)
            {
                IsDoublePointsActive = true
            };

            decimal inc = GameplayTurnFlow.UpdateChainState(state, player, isCorrect: true);

            Assert.AreEqual(GameplayEngineConstants.CHAIN_STEPS[0] * 2, inc);
            Assert.AreEqual(1, state.CurrentStreak);
            Assert.AreEqual(inc, state.CurrentChain);
            Assert.IsFalse(player.IsDoublePointsActive);
        }

        [TestMethod]
        public void UpdateChainState_WhenStreakAlreadyAtLimit_DoesNotIncrementAndResetsDoublePoints()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid())
            {
                CurrentStreak = GameplayEngineConstants.CHAIN_STEPS.Length,
                CurrentChain = 9.99m
            };

            MatchPlayerRuntime player = new MatchPlayerRuntime(USER_ID_1, "P1", callback: null)
            {
                IsDoublePointsActive = true
            };

            decimal inc = GameplayTurnFlow.UpdateChainState(state, player, isCorrect: true);

            Assert.AreEqual(0m, inc);
            Assert.AreEqual(9.99m, state.CurrentChain);
            Assert.AreEqual(GameplayEngineConstants.CHAIN_STEPS.Length, state.CurrentStreak);
            Assert.IsFalse(player.IsDoublePointsActive);
        }

        [TestMethod]
        public void BuildAnswerResult_MapsFieldsFromState()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid())
            {
                CurrentChain = 1.50m,
                BankedPoints = 7.25m
            };

            AnswerResult result = GameplayTurnFlow.BuildAnswerResult(
                questionId: 10,
                state: state,
                isCorrect: true,
                chainIncrement: 0.20m);

            Assert.AreEqual(10, result.QuestionId);
            Assert.IsTrue(result.IsCorrect);
            Assert.AreEqual(0.20m, result.ChainIncrement);
            Assert.AreEqual(1.50m, result.CurrentChain);
            Assert.AreEqual(7.25m, result.BankedPoints);
        }

        [TestMethod]
        public void CountAlivePlayersOrFallbackToTotal_WhenSomeAlive_ReturnsAliveCount()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid());

            state.Players.Add(new MatchPlayerRuntime(USER_ID_1, "P1", callback: null) { IsEliminated = false });
            state.Players.Add(new MatchPlayerRuntime(USER_ID_2, "P2", callback: null) { IsEliminated = true });

            int count = GameplayTurnFlow.CountAlivePlayersOrFallbackToTotal(state);

            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void CountAlivePlayersOrFallbackToTotal_WhenAllEliminated_ReturnsTotalPlayers()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid());

            state.Players.Add(new MatchPlayerRuntime(USER_ID_1, "P1", callback: null) { IsEliminated = true });
            state.Players.Add(new MatchPlayerRuntime(USER_ID_2, "P2", callback: null) { IsEliminated = true });

            int count = GameplayTurnFlow.CountAlivePlayersOrFallbackToTotal(state);

            Assert.AreEqual(2, count);
        }

        private static QuestionWithAnswersDto BuildQuestion(int questionId, string correctText, string wrongText)
        {
            return new QuestionWithAnswersDto
            {
                QuestionId = questionId,
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { AnswerId = 1, Text = correctText, IsCorrect = true, DisplayOrder = 1 },
                    new AnswerDto { AnswerId = 2, Text = wrongText, IsCorrect = false, DisplayOrder = 2 }
                }
            };
        }

        private static FaultException<ServiceFault> AssertThrowsFault(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
                return null;
            }
            catch (FaultException<ServiceFault> ex)
            {
                return ex;
            }
        }
    }
}
