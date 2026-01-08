using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class AuthService_Login_Tests : IntegrationTestBase
    {
        private const string DISPLAY_NAME = "Test User";
        private const string VALID_PASSWORD = "Password123";
        private const string WRONG_PASSWORD = "WrongPassword123";

        [TestMethod]
        public void Login_WhenValidCredentials_ReturnsToken()
        {
            service.Register(new RegisterRequest
            {
                Email = testEmail,
                Password = VALID_PASSWORD,
                DisplayName = DISPLAY_NAME
            });

            LoginResponse response = service.Login(new LoginRequest
            {
                Email = testEmail,
                Password = VALID_PASSWORD
            });

            Assert.IsNotNull(response.Token);
        }

        [TestMethod]
        public void Login_WhenRequestIsNull_ThrowsInvalidRequest()
        {
            FaultAssert.AssertFaultCode(
                () => service.Login(null),
                AuthServiceConstants.ERROR_INVALID_REQUEST);
        }

        [TestMethod]
        public void Login_WhenEmailDoesNotExist_ThrowsInvalidCredentials()
        {
            FaultAssert.AssertFaultCode(
                () => service.Login(new LoginRequest
                {
                    Email = testEmail,
                    Password = VALID_PASSWORD
                }),
                AuthServiceConstants.ERROR_INVALID_CREDENTIALS);
        }

        [TestMethod]
        public void Login_WhenPasswordIsWrong_ThrowsInvalidCredentials()
        {
            service.Register(new RegisterRequest
            {
                Email = testEmail,
                Password = VALID_PASSWORD,
                DisplayName = DISPLAY_NAME
            });

            FaultAssert.AssertFaultCode(
                () => service.Login(new LoginRequest
                {
                    Email = testEmail,
                    Password = WRONG_PASSWORD
                }),
                AuthServiceConstants.ERROR_INVALID_CREDENTIALS);
        }
    }
}
