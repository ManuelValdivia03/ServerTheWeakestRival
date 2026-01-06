using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class AuthService_BeginReset_CompleteReset_Tests : IntegrationTestBase
    {
        private const string DISPLAY_NAME = "Test User";
        private const string ORIGINAL_PASSWORD = "Password123";
        private const string NEW_PASSWORD = "NewPassword123";
        private const string INVALID_CODE = "000000";
        private const string WEAK_PASSWORD = "123";
        private const string MANUAL_CODE = "123456";

        [TestMethod]
        public void BeginPasswordReset_WhenEmailDoesNotExist_ThrowsEmailNotFound()
        {
            FaultAssert.AssertFaultCode(
                () => service.BeginPasswordReset(new BeginPasswordResetRequest { Email = testEmail }),
                AuthServiceConstants.ERROR_EMAIL_NOT_FOUND);
        }

        [TestMethod]
        public void BeginPasswordReset_WhenValidEmail_SendsResetCode()
        {
            service.Register(new RegisterRequest
            {
                Email = testEmail,
                Password = ORIGINAL_PASSWORD,
                DisplayName = DISPLAY_NAME
            });

            service.BeginPasswordReset(new BeginPasswordResetRequest { Email = testEmail });

            Assert.IsFalse(string.IsNullOrWhiteSpace(emailService.LastResetCode));
        }

        [TestMethod]
        public void CompletePasswordReset_WhenValidCode_UpdatesPassword()
        {
            service.Register(new RegisterRequest
            {
                Email = testEmail,
                Password = ORIGINAL_PASSWORD,
                DisplayName = DISPLAY_NAME
            });

            service.BeginPasswordReset(new BeginPasswordResetRequest { Email = testEmail });

            service.CompletePasswordReset(new CompletePasswordResetRequest
            {
                Email = testEmail,
                Code = emailService.LastResetCode,
                NewPassword = NEW_PASSWORD
            });

            LoginResponse response = service.Login(new LoginRequest
            {
                Email = testEmail,
                Password = NEW_PASSWORD
            });

            Assert.IsNotNull(response.Token);
        }

        [TestMethod]
        public void CompletePasswordReset_WhenNoResetWasRequested_ThrowsCodeMissing()
        {
            FaultAssert.AssertFaultCode(
                () => service.CompletePasswordReset(new CompletePasswordResetRequest
                {
                    Email = testEmail,
                    Code = MANUAL_CODE,
                    NewPassword = NEW_PASSWORD
                }),
                AuthServiceConstants.ERROR_CODE_MISSING);
        }

        [TestMethod]
        public void CompletePasswordReset_WhenCodeIsInvalid_ThrowsCodeInvalid()
        {
            service.Register(new RegisterRequest
            {
                Email = testEmail,
                Password = ORIGINAL_PASSWORD,
                DisplayName = DISPLAY_NAME
            });

            service.BeginPasswordReset(new BeginPasswordResetRequest { Email = testEmail });

            FaultAssert.AssertFaultCode(
                () => service.CompletePasswordReset(new CompletePasswordResetRequest
                {
                    Email = testEmail,
                    Code = INVALID_CODE,
                    NewPassword = NEW_PASSWORD
                }),
                AuthServiceConstants.ERROR_CODE_INVALID);
        }

        [TestMethod]
        public void CompletePasswordReset_WhenPasswordIsWeak_ThrowsWeakPassword()
        {
            service.Register(new RegisterRequest
            {
                Email = testEmail,
                Password = ORIGINAL_PASSWORD,
                DisplayName = DISPLAY_NAME
            });

            service.BeginPasswordReset(new BeginPasswordResetRequest { Email = testEmail });

            FaultAssert.AssertFaultCode(
                () => service.CompletePasswordReset(new CompletePasswordResetRequest
                {
                    Email = testEmail,
                    Code = emailService.LastResetCode,
                    NewPassword = WEAK_PASSWORD
                }),
                AuthServiceConstants.ERROR_WEAK_PASSWORD);
        }
    }
}
