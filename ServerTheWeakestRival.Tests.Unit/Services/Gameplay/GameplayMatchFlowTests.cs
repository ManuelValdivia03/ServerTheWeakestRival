using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayMatchFlowTests
    {
        private const int USER_ID = 55;

        [TestMethod]
        public void JoinMatchInternal_CallbackNull_ThrowsInvalidRequestFault()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid());

            FaultException<ServicesTheWeakestRival.Contracts.Data.ServiceFault> ex = AssertThrowsFault(() =>
                GameplayMatchFlow.JoinMatchInternal(state, state.MatchId, USER_ID, callback: null));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void JoinMatchInternal_WhenPlayerAlreadyExists_UpdatesCallbackAndDoesNotThrow()
        {
            Guid matchId = Guid.NewGuid();

            CallbackSpy oldCb = new CallbackSpy();
            CallbackSpy newCb = new CallbackSpy();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.Players.Add(new MatchPlayerRuntime(USER_ID, "P", oldCb));

            GameplayMatchFlow.JoinMatchInternal(state, matchId, USER_ID, newCb);

            Assert.AreSame(newCb, state.Players[0].Callback);
        }

        [TestMethod]
        public void JoinMatchInternal_WhenPlayerEliminated_AndMatchStarted_ThrowsMatchAlreadyStartedFault()
        {
            Guid matchId = Guid.NewGuid();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.HasStarted = true;

            MatchPlayerRuntime player = new MatchPlayerRuntime(USER_ID, "P", new CallbackSpy())
            {
                IsEliminated = true
            };

            state.Players.Add(player);

            FaultException<ServicesTheWeakestRival.Contracts.Data.ServiceFault> ex = AssertThrowsFault(() =>
                GameplayMatchFlow.JoinMatchInternal(state, matchId, USER_ID, new CallbackSpy()));

            Assert.AreEqual(GameplayEngineConstants.ERROR_MATCH_ALREADY_STARTED, ex.Detail.Code);
        }

        [TestMethod]
        public void StartMatchInternal_WhenAlreadyStarted_DoesNotThrowAndDoesNotResetHasStarted()
        {
            Guid matchId = Guid.NewGuid();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.HasStarted = true;

            var request = new ServicesTheWeakestRival.Contracts.Data.GameplayStartMatchRequest
            {
                MatchId = matchId,
                Difficulty = 1,
                LocaleCode = "es-MX",
                MatchDbId = 1
            };

            GameplayMatchFlow.StartMatchInternal(state, request, hostUserId: USER_ID);

            Assert.IsTrue(state.HasStarted);
        }

        [TestMethod]
        public void StartNextRound_WhenOnlyOneAlive_FinishesMatchWithWinner()
        {
            Guid matchId = Guid.NewGuid();

            CallbackSpy cb = new CallbackSpy();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.HasStarted = true;

            MatchPlayerRuntime winner = new MatchPlayerRuntime(1, "W", cb);
            MatchPlayerRuntime loser = new MatchPlayerRuntime(2, "L", cb) { IsEliminated = true };

            state.Players.Add(winner);
            state.Players.Add(loser);

            GameplayMatchFlow.StartNextRound(state);

            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(1, state.WinnerUserId);
            Assert.IsTrue(winner.IsWinner);
        }

        [TestMethod]
        public void StartFinalPhaseIfApplicable_WhenExactlyTwoAlive_StartsFinalPhase()
        {
            Guid matchId = Guid.NewGuid();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.HasStarted = true;

            state.Players.Add(new MatchPlayerRuntime(1, "A", new CallbackSpy()));
            state.Players.Add(new MatchPlayerRuntime(2, "B", new CallbackSpy()));

            for (int i = 0; i < GameplayEngineConstants.FINAL_MIN_QUEUE_QUESTIONS; i++)
            {
                int qid = 1000 + i;
                state.Questions.Enqueue(new ServicesTheWeakestRival.Contracts.Data.QuestionWithAnswersDto { QuestionId = qid });
                state.QuestionsById[qid] = new ServicesTheWeakestRival.Contracts.Data.QuestionWithAnswersDto { QuestionId = qid };
            }

            bool started = GameplayMatchFlow.StartFinalPhaseIfApplicable(state);

            Assert.IsTrue(started);
            Assert.IsTrue(state.IsInFinalPhase);
            Assert.IsFalse(state.IsInVotePhase);
            Assert.IsFalse(state.IsInDuelPhase);
        }

        private static FaultException<ServicesTheWeakestRival.Contracts.Data.ServiceFault> AssertThrowsFault(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
                return null;
            }
            catch (FaultException<ServicesTheWeakestRival.Contracts.Data.ServiceFault> ex)
            {
                return ex;
            }
        }

        private sealed class CallbackSpy : IGameplayServiceCallback
        {
            public void OnNextQuestion(Guid matchId, ServicesTheWeakestRival.Contracts.Data.PlayerSummary targetPlayer, ServicesTheWeakestRival.Contracts.Data.QuestionWithAnswersDto question, decimal currentChain, decimal banked) { }
            public void OnAnswerEvaluated(Guid matchId, ServicesTheWeakestRival.Contracts.Data.PlayerSummary player, ServicesTheWeakestRival.Contracts.Data.AnswerResult result) { }
            public void OnBankUpdated(Guid matchId, ServicesTheWeakestRival.Contracts.Data.BankState bank) { }
            public void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit) { }
            public void OnElimination(Guid matchId, ServicesTheWeakestRival.Contracts.Data.PlayerSummary eliminatedPlayer) { }
            public void OnSpecialEvent(Guid matchId, string eventName, string description) { }
            public void OnCoinFlipResolved(Guid matchId, ServicesTheWeakestRival.Contracts.Data.CoinFlipResolvedDto coinFlip) { }
            public void OnDuelCandidates(Guid matchId, ServicesTheWeakestRival.Contracts.Data.DuelCandidatesDto duelCandidates) { }
            public void OnMatchFinished(Guid matchId, ServicesTheWeakestRival.Contracts.Data.PlayerSummary winner) { }
            public void OnLightningChallengeStarted(Guid matchId, Guid roundId, ServicesTheWeakestRival.Contracts.Data.PlayerSummary targetPlayer, int totalQuestions, int totalTimeSeconds) { }
            public void OnLightningChallengeQuestion(Guid matchId, Guid roundId, int questionIndex, ServicesTheWeakestRival.Contracts.Data.QuestionWithAnswersDto question) { }
            public void OnLightningChallengeFinished(Guid matchId, Guid roundId, int correctAnswers, bool isSuccess) { }
            public void OnTurnOrderInitialized(Guid matchId, ServicesTheWeakestRival.Contracts.Data.TurnOrderDto turnOrder) { }
            public void OnTurnOrderChanged(Guid matchId, ServicesTheWeakestRival.Contracts.Data.TurnOrderDto turnOrder, string reasonCode) { }
        }
    }
}
