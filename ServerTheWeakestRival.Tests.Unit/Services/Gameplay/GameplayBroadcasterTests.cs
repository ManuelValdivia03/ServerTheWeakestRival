using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayBroadcasterTests
    {
        private const int USER_ID = 7;

        [TestMethod]
        public void BuildPlayerSummary_MapsFields()
        {
            MatchPlayerRuntime player = new MatchPlayerRuntime(USER_ID, "X", new CallbackSpy());

            PlayerSummary summary = GameplayBroadcaster.BuildPlayerSummary(player, isOnline: true);

            Assert.AreEqual(USER_ID, summary.UserId);
            Assert.AreEqual("X", summary.DisplayName);
            Assert.IsTrue(summary.IsOnline);
        }

        [TestMethod]
        public void BuildTurnOrderDto_UsesAlivePlayersAndCurrentTurn()
        {
            Guid matchId = Guid.NewGuid();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.Players.Add(new MatchPlayerRuntime(1, "A", new CallbackSpy()));
            state.Players.Add(new MatchPlayerRuntime(2, "B", new CallbackSpy()) { IsEliminated = true });
            state.Players.Add(new MatchPlayerRuntime(3, "C", new CallbackSpy()));

            state.CurrentPlayerIndex = 2;

            TurnOrderDto dto = GameplayBroadcaster.BuildTurnOrderDto(state);

            Assert.AreEqual(3, dto.CurrentTurnUserId);
            Assert.AreEqual(2, dto.OrderedAliveUserIds.Length);
        }

        [TestMethod]
        public void Broadcast_WhenCallbackThrows_DoesNotThrow()
        {
            Guid matchId = Guid.NewGuid();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.Players.Add(new MatchPlayerRuntime(1, "A", new ThrowingCallbackSpy()));
            state.Players.Add(new MatchPlayerRuntime(2, "B", new CallbackSpy()));

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(matchId, "E", "D"),
                "CTX");

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void TrySendSnapshotToJoiningPlayer_WhenInVotePhase_SendsVotePhaseStarted()
        {
            Guid matchId = Guid.NewGuid();

            CallbackSpy cb = new CallbackSpy();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.Players.Add(new MatchPlayerRuntime(USER_ID, "P", cb));

            state.IsInVotePhase = true;

            GameplayBroadcaster.TrySendSnapshotToJoiningPlayer(state, USER_ID);

            Assert.AreEqual(1, cb.VotePhaseStartedCalls);
        }

        private sealed class CallbackSpy : IGameplayServiceCallback
        {
            public int VotePhaseStartedCalls { get; private set; }

            public void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit)
            {
                VotePhaseStartedCalls++;
            }

            public void OnSpecialEvent(Guid matchId, string eventName, string description) { }
            public void OnTurnOrderInitialized(Guid matchId, TurnOrderDto turnOrder) { }
            public void OnNextQuestion(Guid matchId, PlayerSummary targetPlayer, QuestionWithAnswersDto question, decimal currentChain, decimal banked) { }
            public void OnAnswerEvaluated(Guid matchId, PlayerSummary player, AnswerResult result) { }
            public void OnBankUpdated(Guid matchId, BankState bank) { }
            public void OnElimination(Guid matchId, PlayerSummary eliminatedPlayer) { }
            public void OnCoinFlipResolved(Guid matchId, CoinFlipResolvedDto coinFlip) { }
            public void OnDuelCandidates(Guid matchId, DuelCandidatesDto duelCandidates) { }
            public void OnMatchFinished(Guid matchId, PlayerSummary winner) { }
            public void OnLightningChallengeStarted(Guid matchId, Guid roundId, PlayerSummary targetPlayer, int totalQuestions, int totalTimeSeconds) { }
            public void OnLightningChallengeQuestion(Guid matchId, Guid roundId, int questionIndex, QuestionWithAnswersDto question) { }
            public void OnLightningChallengeFinished(Guid matchId, Guid roundId, int correctAnswers, bool isSuccess) { }
            public void OnTurnOrderChanged(Guid matchId, TurnOrderDto turnOrder, string reasonCode) { }
        }

        private sealed class ThrowingCallbackSpy : IGameplayServiceCallback
        {
            public void OnSpecialEvent(Guid matchId, string eventName, string description)
            {
                throw new InvalidOperationException("boom");
            }

            public void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit) { }
            public void OnTurnOrderInitialized(Guid matchId, TurnOrderDto turnOrder) { }
            public void OnNextQuestion(Guid matchId, PlayerSummary targetPlayer, QuestionWithAnswersDto question, decimal currentChain, decimal banked) { }
            public void OnAnswerEvaluated(Guid matchId, PlayerSummary player, AnswerResult result) { }
            public void OnBankUpdated(Guid matchId, BankState bank) { }
            public void OnElimination(Guid matchId, PlayerSummary eliminatedPlayer) { }
            public void OnCoinFlipResolved(Guid matchId, CoinFlipResolvedDto coinFlip) { }
            public void OnDuelCandidates(Guid matchId, DuelCandidatesDto duelCandidates) { }
            public void OnMatchFinished(Guid matchId, PlayerSummary winner) { }
            public void OnLightningChallengeStarted(Guid matchId, Guid roundId, PlayerSummary targetPlayer, int totalQuestions, int totalTimeSeconds) { }
            public void OnLightningChallengeQuestion(Guid matchId, Guid roundId, int questionIndex, QuestionWithAnswersDto question) { }
            public void OnLightningChallengeFinished(Guid matchId, Guid roundId, int correctAnswers, bool isSuccess) { }
            public void OnTurnOrderChanged(Guid matchId, TurnOrderDto turnOrder, string reasonCode) { }
        }
    }
}
