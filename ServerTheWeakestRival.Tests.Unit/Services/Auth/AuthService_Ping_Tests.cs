using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class AuthService_Ping_Tests : IntegrationTestBase
    {
        private const string DEFAULT_PONG = "pong";
        private const string ECHO_MESSAGE = "hello";

        [TestMethod]
        public void Ping_WhenRequestIsNull_ReturnsPong()
        {
            PingResponse response = service.Ping(null);

            Assert.AreEqual(DEFAULT_PONG, response.Echo);
        }

        [DataTestMethod]
        [DataRow("", DisplayName = "Ping_WhenMessageIsEmpty_ReturnsPong")]
        [DataRow("   ", DisplayName = "Ping_WhenMessageIsWhitespace_ReturnsPong")]
        public void Ping_WhenMessageIsBlank_ReturnsPong(string message)
        {
            PingResponse response = service.Ping(new PingRequest { Message = message });

            Assert.AreEqual(DEFAULT_PONG, response.Echo);
        }

        [TestMethod]
        public void Ping_WhenMessageHasContent_EchoesMessage()
        {
            PingResponse response = service.Ping(new PingRequest { Message = ECHO_MESSAGE });

            Assert.AreEqual(ECHO_MESSAGE, response.Echo);
        }
    }
}
