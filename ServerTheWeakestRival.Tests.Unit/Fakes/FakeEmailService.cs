using System;
using ServicesTheWeakestRival.Server.Infrastructure;

namespace ServerTheWeakestRival.Tests.Unit.Fakes
{
    public sealed class FakeEmailService : IEmailService
    {
        private const string EMPTY = "";
        private const int DEFAULT_TTL_MINUTES = 0;

        public string LastVerificationEmail { get; private set; } = EMPTY;
        public string LastVerificationCode { get; private set; } = EMPTY;
        public int LastVerificationTtlMinutes { get; private set; } = DEFAULT_TTL_MINUTES;

        public string LastResetEmail { get; private set; } = EMPTY;
        public string LastResetCode { get; private set; } = EMPTY;
        public int LastResetTtlMinutes { get; private set; } = DEFAULT_TTL_MINUTES;

        public void SendVerificationCode(string email, string code, int ttlMinutes)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Code is required.", nameof(code));
            }

            if (ttlMinutes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ttlMinutes));
            }

            LastVerificationEmail = email;
            LastVerificationCode = code;
            LastVerificationTtlMinutes = ttlMinutes;
        }

        public void SendPasswordResetCode(string email, string code, int ttlMinutes)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Code is required.", nameof(code));
            }

            if (ttlMinutes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ttlMinutes));
            }

            LastResetEmail = email;
            LastResetCode = code;
            LastResetTtlMinutes = ttlMinutes;
        }

        public void Reset()
        {
            LastVerificationEmail = EMPTY;
            LastVerificationCode = EMPTY;
            LastVerificationTtlMinutes = DEFAULT_TTL_MINUTES;

            LastResetEmail = EMPTY;
            LastResetCode = EMPTY;
            LastResetTtlMinutes = DEFAULT_TTL_MINUTES;
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
