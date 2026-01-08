using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class AuthService_Register_Tests : IntegrationTestBase
    {
        private const string DISPLAY_NAME = "Test User";
        private const string VALID_PASSWORD = "Password123";
        private const string WEAK_PASSWORD = "123";

        [TestMethod]
        public void Register_WhenValidRequest_ReturnsToken()
        {
            RegisterResponse response = service.Register(new RegisterRequest
            {
                Email = testEmail,
                Password = VALID_PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageUrl = null
            });

            Assert.IsNotNull(response.Token);
        }

        [TestMethod]
        public void Register_WhenRequestIsNull_ThrowsInvalidRequest()
        {
            FaultAssert.AssertFaultCode(
                () => service.Register(null),
                AuthServiceConstants.ERROR_INVALID_REQUEST);
        }

        [TestMethod]
        public void Register_WhenPasswordIsWeak_ThrowsWeakPassword()
        {
            FaultAssert.AssertFaultCode(
                () => service.Register(new RegisterRequest
                {
                    Email = testEmail,
                    Password = WEAK_PASSWORD,
                    DisplayName = DISPLAY_NAME
                }),
                AuthServiceConstants.ERROR_WEAK_PASSWORD);
        }

        [TestMethod]
        public void Register_WhenEmailAlreadyExists_ThrowsEmailTaken()
        {
            service.Register(new RegisterRequest
            {
                Email = testEmail,
                Password = VALID_PASSWORD,
                DisplayName = DISPLAY_NAME
            });

            FaultAssert.AssertFaultCode(
                () => service.Register(new RegisterRequest
                {
                    Email = testEmail,
                    Password = VALID_PASSWORD,
                    DisplayName = DISPLAY_NAME
                }),
                AuthServiceConstants.ERROR_EMAIL_TAKEN);
        }
    }
}
