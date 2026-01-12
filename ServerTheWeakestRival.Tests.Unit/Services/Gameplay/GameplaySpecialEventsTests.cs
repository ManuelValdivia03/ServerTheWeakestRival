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
    public sealed class GameplaySpecialEventsTests
    {
        private const int USER_ID_A = 1;
        private const int USER_ID_B = 2;

        private const int QUESTION_ID_A = 10;
        private const int QUESTION_ID_B = 11;

        private const string ANSWER_OK = "OK";
        private const string ANSWER_BAD = "BAD";

        [TestMethod]
        public void TryStartBombQuestionEvent_QuestionIdInvalid_ReturnsFalse()
        {
            MatchRuntimeState state = new MatchRuntimeState(Guid.NewGuid());
            MatchPlayerRuntime player = new MatchPlayerRuntime(USER_ID_A, "A", new CallbackSpy());

            bool started = GameplaySpecialEvents.TryStartBombQuestionEvent(state, player, questionId: 0);

            Assert.IsFalse(started);
        }

        [TestMethod]
        public void EndDarkModeIfActive_WhenVotesExist_SendsVoteRevealToEachVoter_AndEndsDarkMode()
        {
            Guid matchId = Guid.NewGuid();

            CallbackSpy cbA = new CallbackSpy();
            CallbackSpy cbB = new CallbackSpy();

            MatchRuntimeState state = new MatchRuntimeState(matchId);
            state.Players.Add(new MatchPlayerRuntime(USER_ID_A, "A", cbA));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_B, "B", cbB));

            state.IsDarkModeActive = true;
            state.DarkModeRoundNumber = 1;

            state.VotesThisRound[USER_ID_A] = USER_ID_B;
            state.VotesThisRound[USER_ID_B] = null;

            GameplaySpecialEvents.EndDarkModeIfActive(state);

            Assert.IsFalse(state.IsDarkModeActive);
            Assert.AreEqual(0, state.DarkModeRoundNumber);

            Assert.IsTrue(cbA.SpecialEventCalls >= 2);
            Assert.IsTrue(cbB.SpecialEventCalls >= 2);

            Assert.AreEqual(GameplayEngineConstants.SPECIAL_EVENT_DARK_MODE_ENDED_CODE, cbA.LastEventName);
            Assert.AreEqual(GameplayEngineConstants.SPECIAL_EVENT_DARK_MODE_ENDED_CODE, cbB.LastEventName);
        }

        private static QuestionWithAnswersDto BuildQuestion(int id, string correct)
        {
            return new QuestionWithAnswersDto
            {
                QuestionId = id,
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { AnswerId = 1, Text = correct, IsCorrect = true, DisplayOrder = 1 },
                    new AnswerDto { AnswerId = 2, Text = correct == ANSWER_OK ? ANSWER_BAD : ANSWER_OK, IsCorrect = false, DisplayOrder = 2 }
                }
            };
        }

        private sealed class CallbackSpy : IGameplayServiceCallback
        {
            public int NextQuestionCalls { get; private set; }
            public int BankUpdatedCalls { get; private set; }
            public int SpecialEventCalls { get; private set; }
            public string LastEventName { get; private set; }

            public void OnNextQuestion(Guid matchId, PlayerSummary targetPlayer, QuestionWithAnswersDto question, decimal currentChain, decimal banked)
            {
                NextQuestionCalls++;
            }

            public void OnBankUpdated(Guid matchId, BankState bank)
            {
                BankUpdatedCalls++;
            }

            public void OnSpecialEvent(Guid matchId, string eventName, string description)
            {
                SpecialEventCalls++;
                LastEventName = eventName;
            }

            public void OnAnswerEvaluated(Guid matchId, PlayerSummary player, AnswerResult result) { }
            public void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit) { }
            public void OnElimination(Guid matchId, PlayerSummary eliminatedPlayer) { }
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
