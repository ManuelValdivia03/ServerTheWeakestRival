using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Gameplay;
using System;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayDisconnectHubTests
    {
        [TestMethod]
        public void RegisterCurrentSession_WithInvalidArgs_DoesNotThrow()
        {
            GameplayDisconnectHub.RegisterCurrentSession(Guid.Empty, userId: 1, callback: new CallbackSpy());

            GameplayDisconnectHub.RegisterCurrentSession(Guid.NewGuid(), userId: 0, callback: new CallbackSpy());

            GameplayDisconnectHub.RegisterCurrentSession(Guid.NewGuid(), userId: 1, callback: null);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void CleanupMatch_EmptyGuid_DoesNotThrow()
        {
            GameplayDisconnectHub.CleanupMatch(Guid.Empty);
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void CleanupMatch_AnyGuid_DoesNotThrow()
        {
            GameplayDisconnectHub.CleanupMatch(Guid.NewGuid());
            Assert.IsTrue(true);
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
