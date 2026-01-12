using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayVotingAndDuelFlowTests
    {
        private const int USER_ID_P1 = 201;
        private const int USER_ID_P2 = 202;
        private const int USER_ID_P3 = 203;

        private const string P1_NAME = "P1";
        private const string P2_NAME = "P2";
        private const string P3_NAME = "P3";

        private const byte DIFFICULTY = 1;
        private const string LOCALE_CODE = "es-MX";

        private const decimal INITIAL_BANKED_POINTS = 5.00m;

        private const int ROUND_NUMBER = 3;

        private const int QUESTION_ID_BASE = 9000;
        private const int QUESTIONS_POOL_COUNT = 30;

        [TestMethod]
        public void StartVotePhase_ResetsFlagsAndVotes_AndBroadcastsVotePhaseStarted()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();

            state.RoundNumber = ROUND_NUMBER;

            state.IsInVotePhase = false;
            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = USER_ID_P2;
            state.DuelTargetUserId = USER_ID_P3;

            state.VotersThisRound.Add(USER_ID_P1);
            state.VotesThisRound[USER_ID_P1] = USER_ID_P2;

            state.BombQuestionId = 123;
            state.ActiveSpecialEvent = SpecialEventType.LightningChallenge;

            GameplayVotingAndDuelFlow.StartVotePhase(state);

            Assert.IsTrue(state.IsInVotePhase);
            Assert.IsFalse(state.IsInDuelPhase);
            Assert.IsNull(state.WeakestRivalUserId);
            Assert.IsNull(state.DuelTargetUserId);

            Assert.AreEqual(0, state.VotersThisRound.Count);
            Assert.AreEqual(0, state.VotesThisRound.Count);

            Assert.AreEqual(0, state.BombQuestionId);
            Assert.AreEqual(SpecialEventType.None, state.ActiveSpecialEvent);

            GameplayCallbackSpy cb1 = (GameplayCallbackSpy)state.Players[0].Callback;
            GameplayCallbackSpy cb2 = (GameplayCallbackSpy)state.Players[1].Callback;
            GameplayCallbackSpy cb3 = (GameplayCallbackSpy)state.Players[2].Callback;

            Assert.AreEqual(1, cb1.VotePhaseStartedCalls);
            Assert.AreEqual(1, cb2.VotePhaseStartedCalls);
            Assert.AreEqual(1, cb3.VotePhaseStartedCalls);

            Assert.AreEqual(GameplayEngineConstants.VOTE_PHASE_TIME_LIMIT_SECONDS, cb1.LastVotePhaseSeconds);
        }

        [TestMethod]
        public void CastVoteInternal_WhenNotInVotePhase_ThrowsFault()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            state.IsInVotePhase = false;

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayVotingAndDuelFlow.CastVoteInternal(state, USER_ID_P1, USER_ID_P2));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void CastVoteInternal_WhenTargetIsEliminated_ThrowsFault()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            state.IsInVotePhase = true;

            state.Players.First(p => p.UserId == USER_ID_P2).IsEliminated = true;

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayVotingAndDuelFlow.CastVoteInternal(state, USER_ID_P1, USER_ID_P2));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void CastVoteInternal_AcceptsVote_TracksVoterAndVote()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            state.IsInVotePhase = true;

            bool accepted = GameplayVotingAndDuelFlow.CastVoteInternal(state, USER_ID_P1, USER_ID_P2);

            Assert.IsTrue(accepted);
            Assert.IsTrue(state.VotersThisRound.Contains(USER_ID_P1));
            Assert.IsTrue(state.VotesThisRound.ContainsKey(USER_ID_P1));
            Assert.AreEqual(USER_ID_P2, state.VotesThisRound[USER_ID_P1]);
        }

        [TestMethod]
        public void CastVoteInternal_WhenAllAliveVoted_EndsVotePhase()
        {

            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            state.IsInVotePhase = true;

            GameplayVotingAndDuelFlow.CastVoteInternal(state, USER_ID_P1, USER_ID_P2);
            GameplayVotingAndDuelFlow.CastVoteInternal(state, USER_ID_P2, USER_ID_P2);
            GameplayVotingAndDuelFlow.CastVoteInternal(state, USER_ID_P3, USER_ID_P2);

            Assert.IsFalse(state.IsInVotePhase);
        }

        [TestMethod]
        public void ChooseDuelOpponentInternal_WhenNotInDuelPhase_ThrowsFault()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            state.IsInDuelPhase = false;
            state.WeakestRivalUserId = null;

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayVotingAndDuelFlow.ChooseDuelOpponentInternal(state, USER_ID_P1, USER_ID_P2));

            Assert.AreEqual(GameplayEngineConstants.ERROR_DUEL_NOT_ACTIVE, ex.Detail.Code);
        }

        [TestMethod]
        public void ChooseDuelOpponentInternal_WhenCallerNotWeakest_ThrowsFault()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = USER_ID_P2;

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayVotingAndDuelFlow.ChooseDuelOpponentInternal(state, USER_ID_P1, USER_ID_P3));

            Assert.AreEqual(GameplayEngineConstants.ERROR_NOT_WEAKEST_RIVAL, ex.Detail.Code);
        }

        [TestMethod]
        public void ChooseDuelOpponentInternal_WhenTargetNotAlive_ThrowsFault()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = USER_ID_P2;

            state.Players.First(p => p.UserId == USER_ID_P3).IsEliminated = true;

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayVotingAndDuelFlow.ChooseDuelOpponentInternal(state, USER_ID_P2, USER_ID_P3));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_DUEL_TARGET, ex.Detail.Code);
        }

        [TestMethod]
        public void ChooseDuelOpponentInternal_WhenTargetDidNotVoteAgainstWeakest_ThrowsFault()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = USER_ID_P2;

            state.VotesThisRound[USER_ID_P1] = USER_ID_P3;
            state.VotesThisRound[USER_ID_P3] = null;

            FaultException<ServiceFault> ex = AssertThrowsFault(() =>
                GameplayVotingAndDuelFlow.ChooseDuelOpponentInternal(state, USER_ID_P2, USER_ID_P1));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_DUEL_TARGET, ex.Detail.Code);
        }

        [TestMethod]
        public void ChooseDuelOpponentInternal_SetsTarget_AndSetsTurnToWeakest()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            EnsureCurrentQuestionIsValid(state);

            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = USER_ID_P2;

            state.VotesThisRound[USER_ID_P1] = USER_ID_P2;
            state.VotesThisRound[USER_ID_P3] = USER_ID_P2;

            GameplayVotingAndDuelFlow.ChooseDuelOpponentInternal(state, USER_ID_P2, USER_ID_P1);

            Assert.AreEqual(USER_ID_P1, state.DuelTargetUserId);

            MatchPlayerRuntime current = state.GetCurrentPlayer();
            Assert.IsNotNull(current);
            Assert.AreEqual(USER_ID_P2, current.UserId);

        }

        [TestMethod]
        public void ShouldHandleDuelTurn_WhenDuelMissingIds_ReturnsFalse()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = null;
            state.DuelTargetUserId = null;

            bool result = GameplayVotingAndDuelFlow.ShouldHandleDuelTurn(state, state.Players[0]);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldHandleDuelTurn_WhenPlayerIsDuelParticipant_ReturnsTrue()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = USER_ID_P2;
            state.DuelTargetUserId = USER_ID_P1;

            Assert.IsTrue(GameplayVotingAndDuelFlow.ShouldHandleDuelTurn(state, state.Players.First(p => p.UserId == USER_ID_P2)));
            Assert.IsTrue(GameplayVotingAndDuelFlow.ShouldHandleDuelTurn(state, state.Players.First(p => p.UserId == USER_ID_P1)));
            Assert.IsFalse(GameplayVotingAndDuelFlow.ShouldHandleDuelTurn(state, state.Players.First(p => p.UserId == USER_ID_P3)));
        }

        [TestMethod]
        public void HandleDuelTurn_WhenIncorrect_EliminatesCurrent_EndsDuel()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            EnsureCurrentQuestionIsValid(state);

            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = USER_ID_P2;
            state.DuelTargetUserId = USER_ID_P1;

            state.CurrentPlayerIndex = state.Players.FindIndex(p => p.UserId == USER_ID_P1);

            MatchPlayerRuntime current = state.GetCurrentPlayer();
            Assert.AreEqual(USER_ID_P1, current.UserId);

            GameplayVotingAndDuelFlow.HandleDuelTurn(state, current, isCorrect: false);

            Assert.IsTrue(current.IsEliminated);
            Assert.IsFalse(state.IsInDuelPhase);
            Assert.IsNull(state.WeakestRivalUserId);
            Assert.IsNull(state.DuelTargetUserId);
            Assert.AreEqual(0, state.BombQuestionId);
        }

        [TestMethod]
        public void HandleDuelTurn_WhenCorrect_SwitchesTurnToOtherDuelPlayer()
        {
            MatchRuntimeState state = CreateState3PlayersWithQuestions();
            EnsureCurrentQuestionIsValid(state);

            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = USER_ID_P2;
            state.DuelTargetUserId = USER_ID_P1;

            state.CurrentPlayerIndex = state.Players.FindIndex(p => p.UserId == USER_ID_P1);

            MatchPlayerRuntime current = state.GetCurrentPlayer();
            Assert.AreEqual(USER_ID_P1, current.UserId);

            GameplayVotingAndDuelFlow.HandleDuelTurn(state, current, isCorrect: true);

            MatchPlayerRuntime newCurrent = state.GetCurrentPlayer();
            Assert.IsNotNull(newCurrent);
            Assert.AreEqual(USER_ID_P2, newCurrent.UserId);

            // No asumas NextQuestion callbacks (tu implementación podría mandar pregunta solo al jugador en turno).
        }

        private static MatchRuntimeState CreateState3PlayersWithQuestions()
        {
            var state = new MatchRuntimeState(Guid.NewGuid());

            state.Players.Add(new MatchPlayerRuntime(USER_ID_P1, P1_NAME, new GameplayCallbackSpy()));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_P2, P2_NAME, new GameplayCallbackSpy()));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_P3, P3_NAME, new GameplayCallbackSpy()));

            state.IsInVotePhase = false;
            state.IsInDuelPhase = false;
            state.IsInFinalPhase = false;
            state.HasSpecialEventThisRound = true; 

            List<QuestionWithAnswersDto> questions = BuildQuestionsPool();

            state.Initialize(DIFFICULTY, LOCALE_CODE, questions, INITIAL_BANKED_POINTS);

            return state;
        }

        private static void EnsureCurrentQuestionIsValid(MatchRuntimeState state)
        {
            if (state.CurrentQuestionId > 0 && state.QuestionsById.ContainsKey(state.CurrentQuestionId))
            {
                return;
            }

            QuestionWithAnswersDto q = BuildQuestion(QUESTION_ID_BASE + 1);
            state.QuestionsById[q.QuestionId] = q;
            state.CurrentQuestionId = q.QuestionId;

            // También deja al menos una pregunta “siguiente” en cola por si el flujo la consume.
            QuestionWithAnswersDto next = BuildQuestion(QUESTION_ID_BASE + 2);
            state.QuestionsById[next.QuestionId] = next;
            state.Questions.Enqueue(next);
        }

        private static List<QuestionWithAnswersDto> BuildQuestionsPool()
        {
            var list = new List<QuestionWithAnswersDto>(QUESTIONS_POOL_COUNT);

            for (int i = 0; i < QUESTIONS_POOL_COUNT; i++)
            {
                list.Add(BuildQuestion(QUESTION_ID_BASE + i + 1));
            }

            return list;
        }

        private static QuestionWithAnswersDto BuildQuestion(int questionId)
        {
            return new QuestionWithAnswersDto
            {
                QuestionId = questionId,
                CategoryId = 1,
                Difficulty = DIFFICULTY,
                LocaleCode = LOCALE_CODE,
                Body = "Q",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { AnswerId = 1, Text = "A", IsCorrect = true, DisplayOrder = 1 },
                    new AnswerDto { AnswerId = 2, Text = "B", IsCorrect = false, DisplayOrder = 2 }
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
                Assert.IsNotNull(ex.Detail);
                Assert.IsFalse(string.IsNullOrWhiteSpace(ex.Detail.Code));
                return ex;
            }
        }

        private sealed class GameplayCallbackSpy : IGameplayServiceCallback
        {
            public int VotePhaseStartedCalls { get; private set; }
            public int LastVotePhaseSeconds { get; private set; }

            public void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit)
            {
                VotePhaseStartedCalls++;
                LastVotePhaseSeconds = (int)timeLimit.TotalSeconds;
            }

            public void OnNextQuestion(Guid matchId, PlayerSummary targetPlayer, QuestionWithAnswersDto question, decimal currentChain, decimal banked) { }
            public void OnAnswerEvaluated(Guid matchId, PlayerSummary player, AnswerResult result) { }
            public void OnBankUpdated(Guid matchId, BankState bank) { }
            public void OnElimination(Guid matchId, PlayerSummary eliminatedPlayer) { }
            public void OnSpecialEvent(Guid matchId, string eventName, string description) { }
            public void OnCoinFlipResolved(Guid matchId, CoinFlipResolvedDto coinFlip) { }
            public void OnDuelCandidates(Guid matchId, DuelCandidatesDto duelCandidates) { }
            public void OnMatchFinished(Guid matchId, PlayerSummary winner) { }
            public void OnLightningChallengeStarted(Guid matchId, Guid roundId, PlayerSummary targetPlayer, int totalQuestions, int totalTimeSeconds) { }
            public void OnLightningChallengeQuestion(Guid matchId, Guid roundId, int questionIndex, QuestionWithAnswersDto question) { }
            public void OnLightningChallengeFinished(Guid matchId, Guid roundId, int correctAnswers, bool isSuccess) { }
            public void OnTurnOrderInitialized(Guid matchId, TurnOrderDto turnOrder) { }
            public void OnTurnOrderChanged(Guid matchId, TurnOrderDto turnOrder, string reasonCode) { }
        }
    }
}
