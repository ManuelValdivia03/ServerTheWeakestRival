using System;
using ServicesTheWeakestRival.Server.Infrastructure;

namespace ServerTheWeakestRival.Tests.Unit.Fakes
{
    public sealed class FakeEmailService : IEmailService
    {
        public string LastVerificationEmail { get; private set; }
        public string LastVerificationCode { get; private set; }

        public string LastResetEmail { get; private set; }
        public string LastResetCode { get; private set; }

        public void SendVerificationCode(string email, string code, int ttlMinutes)
        {
            LastVerificationEmail = email;
            LastVerificationCode = code;
        }

        public void SendPasswordResetCode(string email, string code, int ttlMinutes)
        {
            LastResetEmail = email;
            LastResetCode = code;
        }
    }

    public sealed class ThrowingEmailService : IEmailService
    {
        private readonly Exception exceptionToThrow;

        public ThrowingEmailService(Exception exceptionToThrow)
        {
            this.exceptionToThrow = exceptionToThrow ?? throw new ArgumentNullException(nameof(exceptionToThrow));
        }

        public void SendVerificationCode(string email, string code, int ttlMinutes)
        {
            throw exceptionToThrow;
        }

        public void SendPasswordResetCode(string email, string code, int ttlMinutes)
        {
            throw exceptionToThrow;
        }
    }
}
