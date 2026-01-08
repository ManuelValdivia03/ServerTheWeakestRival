using System.Net.Mail;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Fakes;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class AuthService_BeginRegister_CompleteRegister_Tests : IntegrationTestBase
    {
        private const string DISPLAY_NAME = "Test User";
        private const string VALID_PASSWORD = "Password123";
        private const string INVALID_CODE = "000000";
        private const string WEAK_PASSWORD = "123";
        private const string MANUAL_CODE = "123456";

        [TestMethod]
        public void BeginRegister_WhenValidEmail_SendsCode()
        {
            service.BeginRegister(new BeginRegisterRequest { Email = testEmail });

            Assert.IsFalse(string.IsNullOrWhiteSpace(emailService.LastVerificationCode));
        }

        [TestMethod]
        public void BeginRegister_WhenRequestIsNull_ThrowsInvalidRequest()
        {
            FaultAssert.AssertFaultCode(
                () => service.BeginRegister(null),
                AuthServiceConstants.ERROR_INVALID_REQUEST);
        }

        [DataTestMethod]
        [DataRow("", DisplayName = "BeginRegister_WhenEmailIsEmpty_ThrowsInvalidRequest")]
        [DataRow("   ", DisplayName = "BeginRegister_WhenEmailIsWhitespace_ThrowsInvalidRequest")]
        public void BeginRegister_WhenEmailIsBlank_ThrowsInvalidRequest(string email)
        {
            FaultAssert.AssertFaultCode(
                () => service.BeginRegister(new BeginRegisterRequest { Email = email }),
                AuthServiceConstants.ERROR_INVALID_REQUEST);
        }

        [TestMethod]
        public void BeginRegister_WhenEmailAlreadyRegistered_ThrowsEmailTaken()
        {
            service.Register(new RegisterRequest
            {
                Email = testEmail,
                Password = VALID_PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            });

            FaultAssert.AssertFaultCode(
                () => service.BeginRegister(new BeginRegisterRequest { Email = testEmail }),
                AuthServiceConstants.ERROR_EMAIL_TAKEN);
        }

        [TestMethod]
        public void BeginRegister_WhenRequestedTooSoon_ThrowsTooSoon()
        {
            service.BeginRegister(new BeginRegisterRequest { Email = testEmail });

            FaultAssert.AssertFaultCode(
                () => service.BeginRegister(new BeginRegisterRequest { Email = testEmail }),
                AuthServiceConstants.ERROR_TOO_SOON);
        }

        [TestMethod]
        public void BeginRegister_WhenSmtpFails_ThrowsSmtpError()
        {
            var throwing = new ServicesTheWeakestRival.Server.Services.AuthService(
                new PasswordService(AuthServiceConstants.PASSWORD_MIN_LENGTH),
                new ThrowingEmailService(new SmtpException("SMTP FAIL")));

            FaultAssert.AssertFaultCode(
                () => throwing.BeginRegister(new BeginRegisterRequest { Email = testEmail }),
                AuthServiceConstants.ERROR_SMTP);
        }

        [TestMethod]
        public void CompleteRegister_WhenValidCode_ReturnsToken()
        {
            service.BeginRegister(new BeginRegisterRequest { Email = testEmail });

            RegisterResponse response = service.CompleteRegister(new CompleteRegisterRequest
            {
                Email = testEmail,
                DisplayName = DISPLAY_NAME,
                Password = VALID_PASSWORD,
                Code = emailService.LastVerificationCode,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            });

            Assert.IsNotNull(response.Token);
        }

        [TestMethod]
        public void CompleteRegister_WhenNoCodeWasRequested_ThrowsCodeMissing()
        {
            FaultAssert.AssertFaultCode(
                () => service.CompleteRegister(new CompleteRegisterRequest
                {
                    Email = testEmail,
                    DisplayName = DISPLAY_NAME,
                    Password = VALID_PASSWORD,
                    Code = MANUAL_CODE,
                    ProfileImageBytes = null,
                    ProfileImageContentType = null
                }),
                AuthServiceConstants.ERROR_CODE_MISSING);
        }

        [TestMethod]
        public void CompleteRegister_WhenCodeIsInvalid_ThrowsCodeInvalid()
        {
            service.BeginRegister(new BeginRegisterRequest { Email = testEmail });

            FaultAssert.AssertFaultCode(
                () => service.CompleteRegister(new CompleteRegisterRequest
                {
                    Email = testEmail,
                    DisplayName = DISPLAY_NAME,
                    Password = VALID_PASSWORD,
                    Code = INVALID_CODE,
                    ProfileImageBytes = null,
                    ProfileImageContentType = null
                }),
                AuthServiceConstants.ERROR_CODE_INVALID);
        }

        [TestMethod]
        public void CompleteRegister_WhenPasswordIsWeak_ThrowsWeakPassword()
        {
            service.BeginRegister(new BeginRegisterRequest { Email = testEmail });

            FaultAssert.AssertFaultCode(
                () => service.CompleteRegister(new CompleteRegisterRequest
                {
                    Email = testEmail,
                    DisplayName = DISPLAY_NAME,
                    Password = WEAK_PASSWORD,
                    Code = emailService.LastVerificationCode,
                    ProfileImageBytes = null,
                    ProfileImageContentType = null
                }),
                AuthServiceConstants.ERROR_WEAK_PASSWORD);
        }
    }
}
