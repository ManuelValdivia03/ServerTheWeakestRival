using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Fakes;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Email;
using System.Net.Mail;

namespace ServerTheWeakestRival.Tests.Unit.Infrastructure
{
    [TestClass]
    public sealed class AuthEmailDispatcherTests
    {
        private const string EMAIL = "user@test.local";
        private const string CODE = "123456";

        [TestMethod]
        public void SendVerificationCodeOrThrow_WhenEmailServiceSucceeds_CallsEmailService()
        {
            var fakeEmailService = new FakeEmailService();
            var dispatcher = new AuthEmailDispatcher(fakeEmailService);

            dispatcher.SendVerificationCodeOrThrow(EMAIL, CODE);

            Assert.AreEqual(EMAIL, fakeEmailService.LastVerificationEmail);
            Assert.AreEqual(CODE, fakeEmailService.LastVerificationCode);
            Assert.AreEqual(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES, fakeEmailService.LastVerificationTtlMinutes);
        }

        [TestMethod]
        public void SendPasswordResetCodeOrThrow_WhenEmailServiceSucceeds_CallsEmailService()
        {
            var fakeEmailService = new FakeEmailService();
            var dispatcher = new AuthEmailDispatcher(fakeEmailService);

            dispatcher.SendPasswordResetCodeOrThrow(EMAIL, CODE);

            Assert.AreEqual(EMAIL, fakeEmailService.LastResetEmail);
            Assert.AreEqual(CODE, fakeEmailService.LastResetCode);
            Assert.AreEqual(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES, fakeEmailService.LastResetTtlMinutes);
        }

        [TestMethod]
        public void SendVerificationCodeOrThrow_WhenSmtpExceptionThrown_ThrowsSmtpFault()
        {
            var emailService = new ThrowingEmailService(new SmtpException("SMTP fail"));
            var dispatcher = new AuthEmailDispatcher(emailService);

            ServiceFault fault = FaultAssert.Capture(() =>
                dispatcher.SendVerificationCodeOrThrow(EMAIL, CODE));

            Assert.AreEqual(AuthServiceConstants.ERROR_SMTP, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_EMAIL_FAILED, fault.Message);
            Assert.IsTrue(string.IsNullOrWhiteSpace(fault.Details));
        }

        [TestMethod]
        public void SendPasswordResetCodeOrThrow_WhenSmtpExceptionThrown_ThrowsSmtpFault()
        {
            var emailService = new ThrowingEmailService(new SmtpException("SMTP fail"));
            var dispatcher = new AuthEmailDispatcher(emailService);

            ServiceFault fault = FaultAssert.Capture(() =>
                dispatcher.SendPasswordResetCodeOrThrow(EMAIL, CODE));

            Assert.AreEqual(AuthServiceConstants.ERROR_SMTP, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PASSWORD_RESET_EMAIL_FAILED, fault.Message);
            Assert.IsTrue(string.IsNullOrWhiteSpace(fault.Details));
        }
    }
}
