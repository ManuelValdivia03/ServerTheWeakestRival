using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class ResendCooldownPolicyTests
    {
        private const int COOLDOWN_SECONDS = 60;

        [TestMethod]
        public void EnsureNotTooSoonOrThrow_WhenLastRequestIsNull_DoesNotThrow()
        {
            AssertDoesNotThrow(() =>
                ResendCooldownPolicy.EnsureNotTooSoonOrThrow(null, COOLDOWN_SECONDS));
        }

        [TestMethod]
        public void EnsureNotTooSoonOrThrow_WhenLastRequestHasNoValue_DoesNotThrow()
        {
            AssertDoesNotThrow(() =>
                ResendCooldownPolicy.EnsureNotTooSoonOrThrow(LastRequestUtcResult.None(), COOLDOWN_SECONDS));
        }

        [TestMethod]
        public void EnsureNotTooSoonOrThrow_WhenEnoughTimePassed_DoesNotThrow()
        {
            DateTime oldUtc = DateTime.UtcNow.AddSeconds(-(COOLDOWN_SECONDS + 5));

            AssertDoesNotThrow(() =>
                ResendCooldownPolicy.EnsureNotTooSoonOrThrow(
                    LastRequestUtcResult.From(oldUtc),
                    COOLDOWN_SECONDS));
        }

        [TestMethod]
        public void EnsureNotTooSoonOrThrow_WhenTooSoon_ThrowsTooSoon()
        {
            DateTime recentUtc = DateTime.UtcNow.AddSeconds(-(COOLDOWN_SECONDS - 1));

            ServiceFault fault = FaultAssert.Capture(() =>
                ResendCooldownPolicy.EnsureNotTooSoonOrThrow(
                    LastRequestUtcResult.From(recentUtc),
                    COOLDOWN_SECONDS));

            Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);
        }

        [TestMethod]
        public void EnsureNotTooSoonOrThrow_WhenExactlyAtCooldownBoundary_DoesNotThrow()
        {
            DateTime boundaryUtc = DateTime.UtcNow.AddSeconds(-COOLDOWN_SECONDS);

            AssertDoesNotThrow(() =>
                ResendCooldownPolicy.EnsureNotTooSoonOrThrow(
                    LastRequestUtcResult.From(boundaryUtc),
                    COOLDOWN_SECONDS));
        }

        [TestMethod]
        public void EnsureNotTooSoonOrThrow_WhenLastRequestIsNow_ThrowsTooSoon()
        {
            DateTime nowUtc = DateTime.UtcNow;

            ServiceFault fault = FaultAssert.Capture(() =>
                ResendCooldownPolicy.EnsureNotTooSoonOrThrow(
                    LastRequestUtcResult.From(nowUtc),
                    COOLDOWN_SECONDS));

            Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);
        }

        [TestMethod]
        public void EnsureNotTooSoonOrThrow_WhenLastRequestIsInFuture_ThrowsTooSoon()
        {
            DateTime futureUtc = DateTime.UtcNow.AddSeconds(10);

            ServiceFault fault = FaultAssert.Capture(() =>
                ResendCooldownPolicy.EnsureNotTooSoonOrThrow(
                    LastRequestUtcResult.From(futureUtc),
                    COOLDOWN_SECONDS));

            Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);
        }

        [TestMethod]
        public void EnsureNotTooSoonOrThrow_WhenLastRequestIsVeryOld_DoesNotThrow()
        {
            AssertDoesNotThrow(() =>
                ResendCooldownPolicy.EnsureNotTooSoonOrThrow(
                    LastRequestUtcResult.From(DateTime.MinValue),
                    COOLDOWN_SECONDS));
        }

        private static void AssertDoesNotThrow(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Assert.Fail("Expected no exception, but got: " + ex.GetType().Name + " - " + ex.Message);
            }
        }
    }
}
