using System;

namespace ServicesTheWeakestRival.Server.Infrastructure
{
    public interface IEmailService
    {
        void SendVerificationCode(string email, string code, int ttlMinutes);

        void SendPasswordResetCode(string email, string code, int ttlMinutes);
    }

    public sealed class SmtpEmailService : IEmailService
    {
        public void SendVerificationCode(string email, string code, int ttlMinutes)
        {
            EmailSender.SendVerificationCode(email, code, ttlMinutes);
        }

        public void SendPasswordResetCode(string email, string code, int ttlMinutes)
        {
            EmailSender.SendPasswordResetCode(email, code, ttlMinutes);
        }
    }
}
