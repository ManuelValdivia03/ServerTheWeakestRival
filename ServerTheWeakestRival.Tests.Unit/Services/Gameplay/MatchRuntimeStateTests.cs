using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class MatchRuntimeStateTests
    {
        private static readonly Guid MATCH_ID = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private const byte DIFFICULTY = 1;
        private const string LOCALE = "es-MX";

        private const int USER_ID_A = 10;
        private const int USER_ID_B = 20;
        private const int USER_ID_C = 30;

        private const decimal INITIAL_BANKED = 5.00m;

        [TestMethod]
        public void Initialize_WhenCalled_ResetsRoundAndFlagsAndPlayersEffects()
        {
            MatchRuntimeState state = new MatchRuntimeState(MATCH_ID);

            state.Players.Add(new MatchPlayerRuntime(USER_ID_A, "A", callback: null) { IsShieldActive = true });
            state.Players.Add(new MatchPlayerRuntime(USER_ID_B, "B", callback: null) { IsDoublePointsActive = true });

            state.RoundNumber = 99;
            state.IsInVotePhase = true;
            state.IsInDuelPhase = true;
            state.BankedPoints = 123m;

            List<QuestionWithAnswersDto> questions = BuildQuestions(count: 2);

            state.Initialize(DIFFICULTY, LOCALE, questions, INITIAL_BANKED);

            Assert.IsTrue(state.IsInitialized);
            Assert.AreEqual(1, state.RoundNumber);
            Assert.AreEqual(0, state.QuestionsAskedThisRound);
            Assert.AreEqual(false, state.IsInVotePhase);
            Assert.AreEqual(false, state.IsInDuelPhase);
            Assert.AreEqual(INITIAL_BANKED, state.BankedPoints);
            Assert.AreEqual(0m, state.CurrentChain);
            Assert.AreEqual(0, state.CurrentStreak);

            Assert.IsFalse(state.Players[0].IsShieldActive);
            Assert.IsFalse(state.Players[1].IsDoublePointsActive);
        }

        [TestMethod]
        public void GetCurrentPlayer_WhenIndexOutOfRange_ResetsToZero()
        {
            MatchRuntimeState state = new MatchRuntimeState(MATCH_ID);

            state.Players.Add(new MatchPlayerRuntime(USER_ID_A, "A", callback: null));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_B, "B", callback: null));

            state.CurrentPlayerIndex = 999;

            MatchPlayerRuntime current = state.GetCurrentPlayer();

            Assert.IsNotNull(current);
            Assert.AreEqual(USER_ID_A, current.UserId);
            Assert.AreEqual(0, state.CurrentPlayerIndex);
        }

        [TestMethod]
        public void AdvanceTurn_WhenNotDuelPhase_SkipsEliminatedPlayers()
        {
            MatchRuntimeState state = new MatchRuntimeState(MATCH_ID);

            MatchPlayerRuntime a = new MatchPlayerRuntime(USER_ID_A, "A", callback: null);
            MatchPlayerRuntime b = new MatchPlayerRuntime(USER_ID_B, "B", callback: null) { IsEliminated = true };
            MatchPlayerRuntime c = new MatchPlayerRuntime(USER_ID_C, "C", callback: null);

            state.Players.Add(a);
            state.Players.Add(b);
            state.Players.Add(c);

            state.CurrentPlayerIndex = 0;

            state.AdvanceTurn();

            Assert.AreEqual(2, state.CurrentPlayerIndex);
            Assert.AreEqual(USER_ID_C, state.GetCurrentPlayer().UserId);
        }

        [TestMethod]
        public void OverrideTurnForLightning_ThenRestore_RestoresPreviousIndex()
        {
            MatchRuntimeState state = new MatchRuntimeState(MATCH_ID);

            state.Players.Add(new MatchPlayerRuntime(USER_ID_A, "A", callback: null));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_B, "B", callback: null));

            state.CurrentPlayerIndex = 1;

            state.OverrideTurnForLightning(targetPlayerIndex: 0);
            Assert.AreEqual(0, state.CurrentPlayerIndex);

            state.RestoreTurnAfterLightning();
            Assert.AreEqual(1, state.CurrentPlayerIndex);
        }

        private static List<QuestionWithAnswersDto> BuildQuestions(int count)
        {
            var list = new List<QuestionWithAnswersDto>();

            for (int i = 1; i <= count; i++)
            {
                list.Add(new QuestionWithAnswersDto
                {
                    QuestionId = i,
                    CategoryId = 1,
                    Difficulty = DIFFICULTY,
                    LocaleCode = LOCALE,
                    Body = "Q" + i,
                    Answers = new List<AnswerDto>
                    {
                        new AnswerDto { AnswerId = i * 10, Text = "A", IsCorrect = true, DisplayOrder = 1 }
                    }
                });
            }

            return list;
        }
    }
}