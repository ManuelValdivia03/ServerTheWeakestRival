using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayActionsFlowTests
    {
        private const int USER_ID_A = 10;
        private const int USER_ID_B = 20;

        private const int QUESTION_ID_1 = 101;
        private const int QUESTION_ID_2 = 102;

        private const string ANSWER_A = "A";
        private const string ANSWER_B = "B";

        [TestMethod]
        public void BankInternal_WhenNotInVotePhase_BanksChainAndBroadcastsBankUpdated()
        {
            Guid matchId = Guid.NewGuid();

            CallbackSpy cbA = new CallbackSpy();
            CallbackSpy cbB = new CallbackSpy();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.Players.Add(new MatchPlayerRuntime(USER_ID_A, "A", cbA));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_B, "B", cbB));

            state.CurrentPlayerIndex = 0;
            state.IsInVotePhase = false;
            state.CurrentChain = 0.50m;
            state.BankedPoints = 1.00m;
            state.CurrentStreak = 2;

            BankState bank = GameplayActionsFlow.BankInternal(state, USER_ID_A);

            Assert.IsNotNull(bank);
            Assert.AreEqual(matchId, bank.MatchId);
            Assert.AreEqual(0m, bank.CurrentChain);
            Assert.AreEqual(1.50m, bank.BankedPoints);

            Assert.AreEqual(1, cbA.BankUpdatedCalls);
            Assert.AreEqual(1, cbB.BankUpdatedCalls);
        }

        [TestMethod]
        public void BankInternal_WhenInVotePhase_ThrowsInvalidRequestFault()
        {
            Guid matchId = Guid.NewGuid();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.Players.Add(new MatchPlayerRuntime(USER_ID_A, "A", new CallbackSpy()));
            state.CurrentPlayerIndex = 0;
            state.IsInVotePhase = true;

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayActionsFlow.BankInternal(state, USER_ID_A));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void SubmitAnswerInternal_WhenNotPlayersTurn_ThrowsNotPlayerTurnFault()
        {
            Guid matchId = Guid.NewGuid();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.Players.Add(new MatchPlayerRuntime(USER_ID_A, "A", new CallbackSpy()));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_B, "B", new CallbackSpy()));

            state.CurrentPlayerIndex = 0;

            QuestionWithAnswersDto q1 = BuildQuestion(QUESTION_ID_1, correctAnswer: ANSWER_A);
            state.QuestionsById[QUESTION_ID_1] = q1;
            state.CurrentQuestionId = QUESTION_ID_1;

            SubmitAnswerRequest request = new SubmitAnswerRequest
            {
                MatchId = matchId,
                QuestionId = QUESTION_ID_1,
                AnswerText = ANSWER_A,
                Token = "t"
            };

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayActionsFlow.SubmitAnswerInternal(state, USER_ID_B, request));

            Assert.AreEqual(GameplayEngineConstants.ERROR_NOT_PLAYER_TURN, ex.Detail.Code);
        }

        [TestMethod]
        public void SubmitAnswerInternal_CorrectAnswer_BroadcastsAnswerEvaluated_AdvancesTurnAndSendsNextQuestion()
        {
            Guid matchId = Guid.NewGuid();

            CallbackSpy cbA = new CallbackSpy();
            CallbackSpy cbB = new CallbackSpy();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.Players.Add(new MatchPlayerRuntime(USER_ID_A, "A", cbA));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_B, "B", cbB));

            state.CurrentPlayerIndex = 0;
            state.IsInVotePhase = false;
            state.IsInDuelPhase = false;
            state.IsInFinalPhase = false;

            state.HasSpecialEventThisRound = true;
            state.QuestionsAskedThisRound = 0;

            QuestionWithAnswersDto q1 = BuildQuestion(QUESTION_ID_1, correctAnswer: ANSWER_A);
            QuestionWithAnswersDto q2 = BuildQuestion(QUESTION_ID_2, correctAnswer: ANSWER_B);

            state.QuestionsById[QUESTION_ID_1] = q1;
            state.QuestionsById[QUESTION_ID_2] = q2;

            state.CurrentQuestionId = QUESTION_ID_1;
            state.Questions.Enqueue(q2);

            SubmitAnswerRequest request = new SubmitAnswerRequest
            {
                MatchId = matchId,
                QuestionId = QUESTION_ID_1,
                AnswerText = ANSWER_A,
                Token = "t"
            };

            AnswerResult result = GameplayActionsFlow.SubmitAnswerInternal(state, USER_ID_A, request);

            Assert.IsNotNull(result);
            Assert.AreEqual(QUESTION_ID_1, result.QuestionId);
            Assert.IsTrue(result.IsCorrect);

            Assert.AreEqual(1, cbA.AnswerEvaluatedCalls);
            Assert.AreEqual(1, cbB.AnswerEvaluatedCalls);

            Assert.AreEqual(2, cbA.NextQuestionCalls + cbB.NextQuestionCalls);
            Assert.AreEqual(QUESTION_ID_2, state.CurrentQuestionId);
            Assert.AreEqual(USER_ID_B, state.GetCurrentPlayer().UserId);
        }

        [TestMethod]
        public void ApplyWildcardFromDbOrThrow_WhenNotPlayersTurn_ThrowsNotPlayerTurnFault()
        {
            Guid matchId = Guid.NewGuid();

            CallbackSpy cbA = new CallbackSpy();
            CallbackSpy cbB = new CallbackSpy();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.WildcardMatchId = 77;

            state.Players.Add(new MatchPlayerRuntime(USER_ID_A, "A", cbA));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_B, "B", cbB));

            state.CurrentPlayerIndex = 0;

            GameplayMatchRegistry.GetOrCreateMatch(matchId);
            GameplayMatchRegistry.MapWildcardMatchId(state.WildcardMatchId, matchId);

            MatchRuntimeState registryState = GameplayMatchRegistry.GetOrCreateMatch(matchId);
            if (!ReferenceEquals(registryState, state))
            {
                registryState.WildcardMatchId = state.WildcardMatchId;
                registryState.Players.Clear();
                registryState.Players.AddRange(state.Players);
                registryState.CurrentPlayerIndex = state.CurrentPlayerIndex;
                state = registryState;
            }

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayActionsFlow.ApplyWildcardFromDbOrThrow(
                    wildcardMatchId: 77,
                    userId: USER_ID_B,
                    wildcardCode: GameplayEngineConstants.WILDCARD_SHIELD,
                    clientRoundNumber: state.RoundNumber));

            Assert.AreEqual(GameplayEngineConstants.ERROR_NOT_PLAYER_TURN, ex.Detail.Code);
        }

        [TestMethod]
        public void ApplyWildcardFromDbOrThrow_Shield_SetsShieldAndBroadcastsWildcardUsed()
        {
            Guid matchId = Guid.NewGuid();

            CallbackSpy cbA = new CallbackSpy();
            CallbackSpy cbB = new CallbackSpy();

            MatchRuntimeState state = GameplayMatchRegistry.GetOrCreateMatch(matchId);
            state.WildcardMatchId = 88;

            state.Players.Clear();
            state.Players.Add(new MatchPlayerRuntime(USER_ID_A, "A", cbA));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_B, "B", cbB));

            state.CurrentPlayerIndex = 0;
            state.IsInVotePhase = false;
            state.IsInDuelPhase = false;
            state.IsInFinalPhase = false;
            state.HasSpecialEventThisRound = true;
            state.BombQuestionId = 0;

            GameplayMatchRegistry.MapWildcardMatchId(state.WildcardMatchId, state.MatchId);

            int roundReturned = GameplayActionsFlow.ApplyWildcardFromDbOrThrow(
                wildcardMatchId: 88,
                userId: USER_ID_A,
                wildcardCode: GameplayEngineConstants.WILDCARD_SHIELD,
                clientRoundNumber: state.RoundNumber);

            Assert.AreEqual(state.RoundNumber, roundReturned);
            Assert.IsTrue(state.Players[0].IsShieldActive);

            Assert.AreEqual(1, cbA.SpecialEventCalls);
            Assert.AreEqual(1, cbB.SpecialEventCalls);
            Assert.IsTrue(cbA.LastEventName.StartsWith("WILDCARD_USED_", StringComparison.Ordinal));
        }

        private static QuestionWithAnswersDto BuildQuestion(int questionId, string correctAnswer)
        {
            return new QuestionWithAnswersDto
            {
                QuestionId = questionId,
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { AnswerId = 1, Text = correctAnswer, IsCorrect = true, DisplayOrder = 1 },
                    new AnswerDto { AnswerId = 2, Text = correctAnswer == ANSWER_A ? ANSWER_B : ANSWER_A, IsCorrect = false, DisplayOrder = 2 }
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

        private sealed class CallbackSpy : IGameplayServiceCallback
        {
            public int NextQuestionCalls { get; private set; }
            public int AnswerEvaluatedCalls { get; private set; }
            public int BankUpdatedCalls { get; private set; }
            public int VotePhaseStartedCalls { get; private set; }
            public int EliminationCalls { get; private set; }
            public int SpecialEventCalls { get; private set; }
            public int CoinFlipResolvedCalls { get; private set; }
            public int DuelCandidatesCalls { get; private set; }
            public int MatchFinishedCalls { get; private set; }
            public int TurnOrderInitializedCalls { get; private set; }
            public int TurnOrderChangedCalls { get; private set; }

            public string LastEventName { get; private set; }

            public void OnNextQuestion(Guid matchId, PlayerSummary targetPlayer, QuestionWithAnswersDto question, decimal currentChain, decimal banked)
            {
                NextQuestionCalls++;
            }

            public void OnAnswerEvaluated(Guid matchId, PlayerSummary player, AnswerResult result)
            {
                AnswerEvaluatedCalls++;
            }

            public void OnBankUpdated(Guid matchId, BankState bank)
            {
                BankUpdatedCalls++;
            }

            public void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit)
            {
                VotePhaseStartedCalls++;
            }

            public void OnElimination(Guid matchId, PlayerSummary eliminatedPlayer)
            {
                EliminationCalls++;
            }

            public void OnSpecialEvent(Guid matchId, string eventName, string description)
            {
                SpecialEventCalls++;
                LastEventName = eventName;
            }

            public void OnCoinFlipResolved(Guid matchId, CoinFlipResolvedDto coinFlip)
            {
                CoinFlipResolvedCalls++;
            }

            public void OnDuelCandidates(Guid matchId, DuelCandidatesDto duelCandidates)
            {
                DuelCandidatesCalls++;
            }

            public void OnMatchFinished(Guid matchId, PlayerSummary winner)
            {
                MatchFinishedCalls++;
            }

            public void OnLightningChallengeStarted(Guid matchId, Guid roundId, PlayerSummary targetPlayer, int totalQuestions, int totalTimeSeconds) { }
            public void OnLightningChallengeQuestion(Guid matchId, Guid roundId, int questionIndex, QuestionWithAnswersDto question) { }
            public void OnLightningChallengeFinished(Guid matchId, Guid roundId, int correctAnswers, bool isSuccess) { }

            public void OnTurnOrderInitialized(Guid matchId, TurnOrderDto turnOrder)
            {
                TurnOrderInitializedCalls++;
            }

            public void OnTurnOrderChanged(Guid matchId, TurnOrderDto turnOrder, string reasonCode)
            {
                TurnOrderChangedCalls++;
            }
        }
    }
}
