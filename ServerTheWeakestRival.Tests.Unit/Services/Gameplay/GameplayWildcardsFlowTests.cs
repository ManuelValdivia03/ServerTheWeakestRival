using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayWildcardsFlowTests
    {
        private const int USER_ID_P1 = 101;
        private const int USER_ID_P2 = 102;
        private const int USER_ID_P3 = 103;

        private const int CATEGORY_ID = 1;

        private const byte DIFFICULTY = 1;

        private const string LOCALE_CODE = "es-MX";

        private const int QUESTION_ID_1 = 1001;
        private const int QUESTION_ID_2 = 1002;
        private const int QUESTION_ID_3 = 1003;

        private const string ANSWER_A = "A";
        private const string ANSWER_B = "B";

        private const decimal BANKED_POINTS_INITIAL = 5.00m;
        private const decimal CHAIN_VALUE = 0.30m;

        private const string INVALID_WILDCARD = "NOPE";

        [TestMethod]
        public void ApplyWildcardLocked_WhenInvalidCode_ThrowsFault()
        {
            MatchRuntimeState state = CreateStateWithPlayersAndQuestion(out MatchPlayerRuntime currentPlayer);

            try
            {
                GameplayWildcardsFlow.ApplyWildcardLocked(state, currentPlayer, INVALID_WILDCARD);
                Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
            }
            catch (System.ServiceModel.FaultException<ServiceFault> ex)
            {
                Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
            }
        }

        [TestMethod]
        public void ApplyWildcardChangeQuestion_WhenNoQuestions_ThrowsFault()
        {
            MatchRuntimeState state = CreateStateWithPlayers(out MatchPlayerRuntime currentPlayer);
            state.CurrentQuestionId = QUESTION_ID_1;

            try
            {
                GameplayWildcardsFlow.ApplyWildcardLocked(state, currentPlayer, GameplayEngineConstants.WILDCARD_CHANGE_Q);
                Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
            }
            catch (System.ServiceModel.FaultException<ServiceFault> ex)
            {
                Assert.AreEqual(GameplayEngineConstants.ERROR_NO_QUESTIONS, ex.Detail.Code);
            }
        }

        [TestMethod]
        public void ApplyWildcardChangeQuestion_SetsCurrentQuestionId_AndBroadcastsNextQuestion()
        {
            MatchRuntimeState state = CreateStateWithPlayers(out MatchPlayerRuntime currentPlayer);

            var spy1 = (GameplayCallbackSpy)state.Players[0].Callback;
            var spy2 = (GameplayCallbackSpy)state.Players[1].Callback;

            EnqueueQuestion(state, CreateQuestion(QUESTION_ID_2));

            state.CurrentQuestionId = QUESTION_ID_1;

            GameplayWildcardsFlow.ApplyWildcardLocked(state, currentPlayer, GameplayEngineConstants.WILDCARD_CHANGE_Q);

            Assert.AreEqual(QUESTION_ID_2, state.CurrentQuestionId);
            Assert.AreEqual(1, spy1.NextQuestionCalls);
            Assert.AreEqual(1, spy2.NextQuestionCalls);

            Assert.IsNotNull(spy1.LastQuestion);
            Assert.AreEqual(QUESTION_ID_2, spy1.LastQuestion.QuestionId);
        }

        [TestMethod]
        public void ApplyWildcardPassQuestion_MovesTurnToNextAlivePlayer_AndBroadcastsTurnOrderAndQuestion()
        {
            MatchRuntimeState state = CreateStateWithPlayersAndQuestion(out MatchPlayerRuntime currentPlayer);

            var spy1 = (GameplayCallbackSpy)state.Players[0].Callback;
            var spy2 = (GameplayCallbackSpy)state.Players[1].Callback;

            state.CurrentPlayerIndex = 0;
            state.CurrentQuestionId = QUESTION_ID_1;

            GameplayWildcardsFlow.ApplyWildcardLocked(state, currentPlayer, GameplayEngineConstants.WILDCARD_PASS_Q);

            Assert.AreEqual(1, state.CurrentPlayerIndex);

            Assert.AreEqual(1, spy1.TurnOrderInitializedCalls);
            Assert.AreEqual(1, spy2.TurnOrderInitializedCalls);

            Assert.AreEqual(1, spy1.NextQuestionCalls);
            Assert.AreEqual(1, spy2.NextQuestionCalls);

            Assert.IsNotNull(spy1.LastTargetPlayer);
            Assert.AreEqual(USER_ID_P2, spy1.LastTargetPlayer.UserId);
        }

        [TestMethod]
        public void ApplyWildcardShield_SetsIsShieldActiveTrue()
        {
            MatchRuntimeState state = CreateStateWithPlayersAndQuestion(out MatchPlayerRuntime currentPlayer);

            Assert.IsFalse(currentPlayer.IsShieldActive);

            GameplayWildcardsFlow.ApplyWildcardLocked(state, currentPlayer, GameplayEngineConstants.WILDCARD_SHIELD);

            Assert.IsTrue(currentPlayer.IsShieldActive);
        }

        [TestMethod]
        public void ApplyWildcardForcedBank_MovesChainToBank_ResetsChainAndStreak_AndBroadcastsBank()
        {
            MatchRuntimeState state = CreateStateWithPlayersAndQuestion(out _);

            state.CurrentChain = CHAIN_VALUE;
            state.CurrentStreak = 3;
            state.BankedPoints = BANKED_POINTS_INITIAL;

            var spy1 = (GameplayCallbackSpy)state.Players[0].Callback;
            var spy2 = (GameplayCallbackSpy)state.Players[1].Callback;

            GameplayWildcardsFlow.ApplyWildcardLocked(state, state.GetCurrentPlayer(), GameplayEngineConstants.WILDCARD_FORCED_BANK);

            Assert.AreEqual(0m, state.CurrentChain);
            Assert.AreEqual(0, state.CurrentStreak);
            Assert.AreEqual(BANKED_POINTS_INITIAL + CHAIN_VALUE, state.BankedPoints);

            Assert.AreEqual(1, spy1.BankUpdatedCalls);
            Assert.AreEqual(1, spy2.BankUpdatedCalls);

            Assert.IsNotNull(spy1.LastBankState);
            Assert.AreEqual(state.MatchId, spy1.LastBankState.MatchId);
            Assert.AreEqual(state.BankedPoints, spy1.LastBankState.BankedPoints);
        }

        [TestMethod]
        public void ApplyWildcardDouble_SetsDoublePointsFlag()
        {
            MatchRuntimeState state = CreateStateWithPlayersAndQuestion(out MatchPlayerRuntime currentPlayer);

            Assert.IsFalse(currentPlayer.IsDoublePointsActive);

            GameplayWildcardsFlow.ApplyWildcardLocked(state, currentPlayer, GameplayEngineConstants.WILDCARD_DOUBLE);

            Assert.IsTrue(currentPlayer.IsDoublePointsActive);
        }

        [TestMethod]
        public void ApplyWildcardBlock_SetsTargetBlockWildcardsRoundNumberToCurrentRound()
        {
            MatchRuntimeState state = CreateStateWithThreePlayersAndQuestion(out MatchPlayerRuntime currentPlayer);

            state.RoundNumber = 7;
            MatchPlayerRuntime target = state.Players[1];

            Assert.AreEqual(0, target.BlockWildcardsRoundNumber);

            GameplayWildcardsFlow.ApplyWildcardLocked(state, currentPlayer, GameplayEngineConstants.WILDCARD_BLOCK);

            Assert.AreEqual(state.RoundNumber, target.BlockWildcardsRoundNumber);
        }

        [TestMethod]
        public void ApplyWildcardSabotage_DecreasesTargetPendingTimeDelta_AndBroadcastsSpecialEvent()
        {
            MatchRuntimeState state = CreateStateWithPlayersAndQuestion(out MatchPlayerRuntime currentPlayer);

            MatchPlayerRuntime target = state.Players[1];
            target.PendingTimeDeltaSeconds = 0;

            var spy1 = (GameplayCallbackSpy)state.Players[0].Callback;
            var spy2 = (GameplayCallbackSpy)state.Players[1].Callback;

            GameplayWildcardsFlow.ApplyWildcardLocked(state, currentPlayer, GameplayEngineConstants.WILDCARD_SABOTAGE);

            Assert.AreEqual(-GameplayEngineConstants.WILDCARD_TIME_PENALTY_SECONDS, target.PendingTimeDeltaSeconds);

            Assert.AreEqual(1, spy1.SpecialEventCalls);
            Assert.AreEqual(1, spy2.SpecialEventCalls);
            Assert.AreEqual(GameplayEngineConstants.SPECIAL_EVENT_TIME_PENALTY_CODE, spy1.LastSpecialEventName);
        }

        [TestMethod]
        public void ApplyWildcardExtraTime_IncreasesCurrentPlayerPendingTimeDelta_AndBroadcastsSpecialEvent()
        {
            MatchRuntimeState state = CreateStateWithPlayersAndQuestion(out MatchPlayerRuntime currentPlayer);

            currentPlayer.PendingTimeDeltaSeconds = 0;

            var spy1 = (GameplayCallbackSpy)state.Players[0].Callback;
            var spy2 = (GameplayCallbackSpy)state.Players[1].Callback;

            GameplayWildcardsFlow.ApplyWildcardLocked(state, currentPlayer, GameplayEngineConstants.WILDCARD_EXTRA_TIME);

            Assert.AreEqual(GameplayEngineConstants.WILDCARD_TIME_BONUS_SECONDS, currentPlayer.PendingTimeDeltaSeconds);

            Assert.AreEqual(1, spy1.SpecialEventCalls);
            Assert.AreEqual(1, spy2.SpecialEventCalls);
            Assert.AreEqual(GameplayEngineConstants.SPECIAL_EVENT_TIME_BONUS_CODE, spy1.LastSpecialEventName);
        }

        [TestMethod]
        public void ApplyWildcardPassQuestion_WhenNoOtherAlive_ThrowsFault()
        {
            MatchRuntimeState state = CreateStateWithPlayersAndQuestion(out MatchPlayerRuntime currentPlayer);

            state.Players[1].IsEliminated = true;

            try
            {
                GameplayWildcardsFlow.ApplyWildcardLocked(state, currentPlayer, GameplayEngineConstants.WILDCARD_PASS_Q);
                Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
            }
            catch (System.ServiceModel.FaultException<ServiceFault> ex)
            {
                Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
            }
        }

        private static MatchRuntimeState CreateStateWithPlayers(out MatchPlayerRuntime currentPlayer)
        {
            Guid matchId = Guid.NewGuid();
            var state = new MatchRuntimeState(matchId);

            var cb1 = new GameplayCallbackSpy();
            var cb2 = new GameplayCallbackSpy();

            state.Players.Add(new MatchPlayerRuntime(USER_ID_P1, "P1", cb1));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_P2, "P2", cb2));

            state.Initialize(
                DIFFICULTY,
                LOCALE_CODE,
                new List<QuestionWithAnswersDto>(),
                BANKED_POINTS_INITIAL);

            state.CurrentPlayerIndex = 0;

            currentPlayer = state.GetCurrentPlayer();
            return state;
        }

        private static MatchRuntimeState CreateStateWithPlayersAndQuestion(out MatchPlayerRuntime currentPlayer)
        {
            MatchRuntimeState state = CreateStateWithPlayers(out currentPlayer);

            QuestionWithAnswersDto q1 = CreateQuestion(QUESTION_ID_1);
            state.QuestionsById[q1.QuestionId] = q1;
            state.CurrentQuestionId = q1.QuestionId;

            return state;
        }

        private static MatchRuntimeState CreateStateWithThreePlayersAndQuestion(out MatchPlayerRuntime currentPlayer)
        {
            Guid matchId = Guid.NewGuid();
            var state = new MatchRuntimeState(matchId);

            var cb1 = new GameplayCallbackSpy();
            var cb2 = new GameplayCallbackSpy();
            var cb3 = new GameplayCallbackSpy();

            state.Players.Add(new MatchPlayerRuntime(USER_ID_P1, "P1", cb1));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_P2, "P2", cb2));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_P3, "P3", cb3));

            state.Initialize(
                DIFFICULTY,
                LOCALE_CODE,
                new List<QuestionWithAnswersDto>(),
                BANKED_POINTS_INITIAL);

            QuestionWithAnswersDto q1 = CreateQuestion(QUESTION_ID_1);
            state.QuestionsById[q1.QuestionId] = q1;
            state.CurrentQuestionId = q1.QuestionId;

            state.CurrentPlayerIndex = 0;

            currentPlayer = state.GetCurrentPlayer();
            return state;
        }

        private static void EnqueueQuestion(MatchRuntimeState state, QuestionWithAnswersDto question)
        {
            state.Questions.Enqueue(question);
            state.QuestionsById[question.QuestionId] = question;
        }

        private static QuestionWithAnswersDto CreateQuestion(int questionId)
        {
            return new QuestionWithAnswersDto
            {
                QuestionId = questionId,
                CategoryId = CATEGORY_ID,
                Difficulty = DIFFICULTY,
                LocaleCode = LOCALE_CODE,
                Body = "Q" + questionId.ToString(),
                Answers = new List<AnswerDto>
                {
                    new AnswerDto
                    {
                        AnswerId = 1,
                        Text = ANSWER_A,
                        IsCorrect = true,
                        DisplayOrder = 1
                    },
                    new AnswerDto
                    {
                        AnswerId = 2,
                        Text = ANSWER_B,
                        IsCorrect = false,
                        DisplayOrder = 2
                    }
                }
            };
        }
    }

    internal sealed class GameplayCallbackSpy : IGameplayServiceCallback
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
        public int LightningStartedCalls { get; private set; }
        public int LightningQuestionCalls { get; private set; }
        public int LightningFinishedCalls { get; private set; }
        public int TurnOrderInitializedCalls { get; private set; }
        public int TurnOrderChangedCalls { get; private set; }

        public PlayerSummary LastTargetPlayer { get; private set; }
        public QuestionWithAnswersDto LastQuestion { get; private set; }
        public AnswerResult LastAnswerResult { get; private set; }
        public BankState LastBankState { get; private set; }
        public string LastSpecialEventName { get; private set; }
        public string LastSpecialEventDescription { get; private set; }
        public TurnOrderDto LastTurnOrder { get; private set; }

        public void OnNextQuestion(Guid matchId, PlayerSummary targetPlayer, QuestionWithAnswersDto question, decimal currentChain, decimal banked)
        {
            NextQuestionCalls++;
            LastTargetPlayer = targetPlayer;
            LastQuestion = question;
        }

        public void OnAnswerEvaluated(Guid matchId, PlayerSummary player, AnswerResult result)
        {
            AnswerEvaluatedCalls++;
            LastAnswerResult = result;
        }

        public void OnBankUpdated(Guid matchId, BankState bank)
        {
            BankUpdatedCalls++;
            LastBankState = bank;
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
            LastSpecialEventName = eventName;
            LastSpecialEventDescription = description;
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

        public void OnLightningChallengeStarted(Guid matchId, Guid roundId, PlayerSummary targetPlayer, int totalQuestions, int totalTimeSeconds)
        {
            LightningStartedCalls++;
        }

        public void OnLightningChallengeQuestion(Guid matchId, Guid roundId, int questionIndex, QuestionWithAnswersDto question)
        {
            LightningQuestionCalls++;
        }

        public void OnLightningChallengeFinished(Guid matchId, Guid roundId, int correctAnswers, bool isSuccess)
        {
            LightningFinishedCalls++;
        }

        public void OnTurnOrderInitialized(Guid matchId, TurnOrderDto turnOrder)
        {
            TurnOrderInitializedCalls++;
            LastTurnOrder = turnOrder;
        }

        public void OnTurnOrderChanged(Guid matchId, TurnOrderDto turnOrder, string reasonCode)
        {
            TurnOrderChangedCalls++;
            LastTurnOrder = turnOrder;
        }
    }
}
