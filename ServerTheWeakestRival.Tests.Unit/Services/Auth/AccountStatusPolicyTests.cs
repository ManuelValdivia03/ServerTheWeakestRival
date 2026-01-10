using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class AccountStatusPolicyTests
    {
        private const byte STATUS_UNKNOWN = 0;
        private const byte STATUS_UNKNOWN_OTHER = 255;

        [TestMethod]
        public void EnsureAllowsLogin_WhenStatusIsActive_DoesNotThrow()
        {
            try
            {
                AccountStatusPolicy.EnsureAllowsLogin(AuthServiceConstants.ACCOUNT_STATUS_ACTIVE);

                Assert.IsTrue(true);
            }
            catch (System.Exception ex)
            {
                Assert.Fail("Expected no exception, but got: " + ex.GetType().Name);
            }
        }


        [TestMethod]
        public void EnsureAllowsLogin_WhenStatusIsInactive_ThrowsAccountInactive()
        {
            var fault = FaultAssert.Capture(() =>
                AccountStatusPolicy.EnsureAllowsLogin(AuthServiceConstants.ACCOUNT_STATUS_INACTIVE));

            Assert.AreEqual(AuthServiceConstants.ERROR_ACCOUNT_INACTIVE, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ACCOUNT_NOT_ACTIVE, fault.Message);
        }

        [TestMethod]
        public void EnsureAllowsLogin_WhenStatusIsSuspended_ThrowsAccountSuspended()
        {
            var fault = FaultAssert.Capture(() =>
                AccountStatusPolicy.EnsureAllowsLogin(AuthServiceConstants.ACCOUNT_STATUS_SUSPENDED));

            Assert.AreEqual(AuthServiceConstants.ERROR_ACCOUNT_SUSPENDED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ACCOUNT_SUSPENDED, fault.Message);
        }

        [TestMethod]
        public void EnsureAllowsLogin_WhenStatusIsBanned_ThrowsAccountBanned()
        {
            var fault = FaultAssert.Capture(() =>
                AccountStatusPolicy.EnsureAllowsLogin(AuthServiceConstants.ACCOUNT_STATUS_BANNED));

            Assert.AreEqual(AuthServiceConstants.ERROR_ACCOUNT_BANNED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ACCOUNT_BANNED, fault.Message);
        }

        [TestMethod]
        public void EnsureAllowsLogin_WhenStatusIsUnknown_ThrowsAccountInactive()
        {
            var fault = FaultAssert.Capture(() =>
                AccountStatusPolicy.EnsureAllowsLogin(STATUS_UNKNOWN));

            Assert.AreEqual(AuthServiceConstants.ERROR_ACCOUNT_INACTIVE, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ACCOUNT_NOT_ACTIVE, fault.Message);
        }

        [TestMethod]
        public void EnsureAllowsLogin_WhenStatusIsOtherUnknown_ThrowsAccountInactive()
        {
            var fault = FaultAssert.Capture(() =>
                AccountStatusPolicy.EnsureAllowsLogin(STATUS_UNKNOWN_OTHER));

            Assert.AreEqual(AuthServiceConstants.ERROR_ACCOUNT_INACTIVE, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ACCOUNT_NOT_ACTIVE, fault.Message);
        }
    }
}
