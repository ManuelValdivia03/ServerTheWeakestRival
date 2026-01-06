using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class AuthService_Logout_Tests : IntegrationTestBase
    {
        [TestMethod]
        public void Logout_WhenRequestIsNull_DoesNothing()
        {
            service.Logout(null);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void Logout_WhenTokenIsBlank_DoesNothing()
        {
            service.Logout(new LogoutRequest { Token = null });
            service.Logout(new LogoutRequest { Token = "" });
            service.Logout(new LogoutRequest { Token = "   " });

            Assert.IsTrue(true);
        }
    }
}
