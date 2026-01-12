using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayMatchRegistryTests
    {
        private static readonly Guid MATCH_ID = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid MATCH_ID_2 = Guid.Parse("44444444-4444-4444-4444-444444444444");

        private const int USER_ID = 101;
        private const int USER_ID_2 = 202;
        private const int INVALID_USER_ID = 0;

        private const int WILDCARD_MATCH_ID_VALID = 999;
        private const int WILDCARD_MATCH_ID_INVALID = 0;

        [TestInitialize]
        public void TestInitialize()
        {
            CleanupKnownMatches();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanupKnownMatches();
        }

        [TestMethod]
        public void GetOrCreateMatch_SameId_ReturnsSameInstance()
        {
            MatchRuntimeState a = GameplayMatchRegistry.GetOrCreateMatch(MATCH_ID);
            MatchRuntimeState b = GameplayMatchRegistry.GetOrCreateMatch(MATCH_ID);

            Assert.IsNotNull(a);
            Assert.AreSame(a, b);
        }

        [TestMethod]
        public void TryGetMatch_WhenMissing_ReturnsFalseAndNull()
        {
            bool found = GameplayMatchRegistry.TryGetMatch(MATCH_ID_2, out MatchRuntimeState state);

            Assert.IsFalse(found);
            Assert.IsNull(state);
        }

        [TestMethod]
        public void TryGetMatch_WhenPresent_ReturnsTrueAndState()
        {
            MatchRuntimeState created = GameplayMatchRegistry.GetOrCreateMatch(MATCH_ID);

            bool found = GameplayMatchRegistry.TryGetMatch(MATCH_ID, out MatchRuntimeState state);

            Assert.IsTrue(found);
            Assert.IsNotNull(state);
            Assert.AreSame(created, state);
        }

        [TestMethod]
        public void GetMatchOrThrow_WhenMissing_ThrowsMatchNotFoundFault()
        {
            Guid missing = Guid.Parse("33333333-3333-3333-3333-333333333333");

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayMatchRegistry.GetMatchOrThrow(missing));

            Assert.AreEqual(GameplayEngineConstants.ERROR_MATCH_NOT_FOUND, ex.Detail.Code);
        }

        [TestMethod]
        public void TrackPlayerMatch_ThenResolveMatchIdForUser_ReturnsMatchId()
        {
            GameplayMatchRegistry.TrackPlayerMatch(USER_ID, MATCH_ID);

            Guid resolved = GameplayMatchRegistry.ResolveMatchIdForUserOrThrow(USER_ID);

            Assert.AreEqual(MATCH_ID, resolved);
        }

        [TestMethod]
        public void ResolveMatchIdForUserOrThrow_WhenUntracked_ThrowsMatchNotFoundFault()
        {
            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayMatchRegistry.ResolveMatchIdForUserOrThrow(USER_ID));

            Assert.AreEqual(GameplayEngineConstants.ERROR_MATCH_NOT_FOUND, ex.Detail.Code);
        }

        [TestMethod]
        public void TrackPlayerMatch_SecondCall_OverridesPreviousMapping()
        {
            GameplayMatchRegistry.TrackPlayerMatch(USER_ID, MATCH_ID);
            GameplayMatchRegistry.TrackPlayerMatch(USER_ID, MATCH_ID_2);

            Guid resolved = GameplayMatchRegistry.ResolveMatchIdForUserOrThrow(USER_ID);

            Assert.AreEqual(MATCH_ID_2, resolved);
        }

        [TestMethod]
        public void UntrackPlayerMatch_InvalidUserId_DoesNothingAndDoesNotThrow()
        {
            GameplayMatchRegistry.UntrackPlayerMatch(INVALID_USER_ID);
        }

        [TestMethod]
        public void UntrackPlayerMatch_WhenTracked_RemovesMapping()
        {
            GameplayMatchRegistry.TrackPlayerMatch(USER_ID, MATCH_ID);

            GameplayMatchRegistry.UntrackPlayerMatch(USER_ID);

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayMatchRegistry.ResolveMatchIdForUserOrThrow(USER_ID));
            Assert.AreEqual(GameplayEngineConstants.ERROR_MATCH_NOT_FOUND, ex.Detail.Code);
        }

        [TestMethod]
        public void StoreOrMergeExpectedPlayers_MatchEmptyGuid_DoesNothing()
        {
            Guid empty = Guid.Empty;

            GameplayMatchRegistry.StoreOrMergeExpectedPlayers(empty, new[] { USER_ID, USER_ID_2 }, USER_ID);

            bool ok = GameplayMatchRegistry.TryGetExpectedPlayers(empty, out _);
            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void StoreOrMergeExpectedPlayers_AddsCallerAndValidExpectedPlayers_AndSkipsInvalid()
        {
            GameplayMatchRegistry.StoreOrMergeExpectedPlayers(MATCH_ID, new[] { USER_ID, USER_ID_2, 0, -1 }, callerUserId: USER_ID);

            bool ok = GameplayMatchRegistry.TryGetExpectedPlayers(MATCH_ID, out ConcurrentDictionary<int, byte> expected);
            Assert.IsTrue(ok);
            Assert.IsNotNull(expected);

            Assert.IsTrue(expected.ContainsKey(USER_ID));
            Assert.IsTrue(expected.ContainsKey(USER_ID_2));
            Assert.IsFalse(expected.ContainsKey(0));
            Assert.IsFalse(expected.ContainsKey(-1));
        }

        [TestMethod]
        public void StoreOrMergeExpectedPlayers_CalledMultipleTimes_MergesAndKeepsAll()
        {
            GameplayMatchRegistry.StoreOrMergeExpectedPlayers(MATCH_ID, new[] { USER_ID }, callerUserId: USER_ID);
            GameplayMatchRegistry.StoreOrMergeExpectedPlayers(MATCH_ID, new[] { USER_ID_2 }, callerUserId: USER_ID_2);

            bool ok = GameplayMatchRegistry.TryGetExpectedPlayers(MATCH_ID, out ConcurrentDictionary<int, byte> expected);
            Assert.IsTrue(ok);

            Assert.IsTrue(expected.ContainsKey(USER_ID));
            Assert.IsTrue(expected.ContainsKey(USER_ID_2));
        }

        [TestMethod]
        public void MapWildcardMatchId_WhenValid_AllowsLookupByWildcardDbId()
        {
            MatchRuntimeState state = GameplayMatchRegistry.GetOrCreateMatch(MATCH_ID);
            state.WildcardMatchId = WILDCARD_MATCH_ID_VALID;

            GameplayMatchRegistry.MapWildcardMatchId(WILDCARD_MATCH_ID_VALID, MATCH_ID);

            MatchRuntimeState resolved = GameplayMatchRegistry.GetMatchByWildcardDbIdOrThrow(WILDCARD_MATCH_ID_VALID);

            Assert.AreSame(state, resolved);
        }

        [TestMethod]
        public void MapWildcardMatchId_WhenInvalid_DoesNotCreateMapping_AndLookupThrowsInvalidRequest()
        {
            MatchRuntimeState state = GameplayMatchRegistry.GetOrCreateMatch(MATCH_ID);
            state.WildcardMatchId = WILDCARD_MATCH_ID_VALID;

            GameplayMatchRegistry.MapWildcardMatchId(WILDCARD_MATCH_ID_INVALID, MATCH_ID);

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayMatchRegistry.GetMatchByWildcardDbIdOrThrow(WILDCARD_MATCH_ID_INVALID));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void GetMatchByWildcardDbIdOrThrow_WhenNoMappingButMatchHasWildcardId_FallsBackAndCaches()
        {
            MatchRuntimeState state = GameplayMatchRegistry.GetOrCreateMatch(MATCH_ID);
            state.WildcardMatchId = WILDCARD_MATCH_ID_VALID;

            MatchRuntimeState resolved1 = GameplayMatchRegistry.GetMatchByWildcardDbIdOrThrow(WILDCARD_MATCH_ID_VALID);
            MatchRuntimeState resolved2 = GameplayMatchRegistry.GetMatchByWildcardDbIdOrThrow(WILDCARD_MATCH_ID_VALID);

            Assert.AreSame(state, resolved1);
            Assert.AreSame(state, resolved2);
        }

        [TestMethod]
        public void CleanupFinishedMatch_RemovesMatch_ExpectedPlayers_Mappings_AndPlayerTracking()
        {
            MatchRuntimeState state = GameplayMatchRegistry.GetOrCreateMatch(MATCH_ID);
            state.WildcardMatchId = WILDCARD_MATCH_ID_VALID;

            GameplayMatchRegistry.MapWildcardMatchId(WILDCARD_MATCH_ID_VALID, MATCH_ID);
            GameplayMatchRegistry.StoreOrMergeExpectedPlayers(MATCH_ID, new[] { USER_ID, USER_ID_2 }, callerUserId: USER_ID);

            state.Players.Add(new MatchPlayerRuntime(USER_ID, "P1", callback: null));
            state.Players.Add(new MatchPlayerRuntime(USER_ID_2, "P2", callback: null));

            GameplayMatchRegistry.TrackPlayerMatch(USER_ID, MATCH_ID);
            GameplayMatchRegistry.TrackPlayerMatch(USER_ID_2, MATCH_ID);

            GameplayMatchRegistry.CleanupFinishedMatch(state);

            bool matchFound = GameplayMatchRegistry.TryGetMatch(MATCH_ID, out _);
            Assert.IsFalse(matchFound);

            bool expectedFound = GameplayMatchRegistry.TryGetExpectedPlayers(MATCH_ID, out _);
            Assert.IsFalse(expectedFound);

            FaultException<ServiceFault> user1Ex = AssertThrowsFault(() => GameplayMatchRegistry.ResolveMatchIdForUserOrThrow(USER_ID));
            Assert.AreEqual(GameplayEngineConstants.ERROR_MATCH_NOT_FOUND, user1Ex.Detail.Code);

            FaultException<ServiceFault> user2Ex = AssertThrowsFault(() => GameplayMatchRegistry.ResolveMatchIdForUserOrThrow(USER_ID_2));
            Assert.AreEqual(GameplayEngineConstants.ERROR_MATCH_NOT_FOUND, user2Ex.Detail.Code);

            FaultException<ServiceFault> wildcardEx = AssertThrowsFault(() => GameplayMatchRegistry.GetMatchByWildcardDbIdOrThrow(WILDCARD_MATCH_ID_VALID));
            Assert.AreEqual(GameplayEngineConstants.ERROR_MATCH_NOT_FOUND, wildcardEx.Detail.Code);
        }

        private static void CleanupKnownMatches()
        {
            TryCleanupById(MATCH_ID);
            TryCleanupById(MATCH_ID_2);
        }

        private static void TryCleanupById(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                return;
            }

            if (GameplayMatchRegistry.TryGetMatch(matchId, out MatchRuntimeState state) && state != null)
            {
                GameplayMatchRegistry.CleanupFinishedMatch(state);
            }
            else
            {
            }
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
