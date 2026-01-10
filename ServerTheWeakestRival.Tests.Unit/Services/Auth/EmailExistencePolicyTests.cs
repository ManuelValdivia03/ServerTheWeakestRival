using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class EmailExistencePolicyTests
    {
        [TestMethod]
        public void EnsureNotExistsOrThrow_WhenExists_ThrowsEmailTaken()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                EmailExistencePolicy.EnsureNotExistsOrThrow(true));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_TAKEN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_TAKEN, fault.Message);
        }

        [TestMethod]
        public void EnsureNotExistsOrThrow_WhenNotExists_DoesNotThrow()
        {
            EmailExistencePolicy.EnsureNotExistsOrThrow(false);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void EnsureExistsOrThrow_WhenNotExists_ThrowsEmailNotFound()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                EmailExistencePolicy.EnsureExistsOrThrow(false));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_NOT_FOUND, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_NOT_REGISTERED, fault.Message);
        }

        [TestMethod]
        public void EnsureExistsOrThrow_WhenExists_DoesNotThrow()
        {
            EmailExistencePolicy.EnsureExistsOrThrow(true);

            Assert.IsTrue(true);
        }
    }
}
