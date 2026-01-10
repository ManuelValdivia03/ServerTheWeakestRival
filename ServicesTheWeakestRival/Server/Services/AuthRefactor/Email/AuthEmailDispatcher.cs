using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using System;
using System.Net.Mail;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Email
{
    public sealed class AuthEmailDispatcher
    {
        private readonly IEmailService emailService;

        public AuthEmailDispatcher(IEmailService emailService)
        {
            this.emailService = emailService;
        }

        public void SendVerificationCodeOrThrow(string email, string code)
        {
            SendOrThrow(
                () => emailService.SendVerificationCode(email, code, AuthServiceContext.CodeTtlMinutes),
                AuthServiceConstants.CTX_BEGIN_REGISTER);
        }

        public void SendPasswordResetCodeOrThrow(string email, string code)
        {
            SendOrThrow(
                () => emailService.SendPasswordResetCode(email, code, AuthServiceContext.CodeTtlMinutes),
                AuthServiceConstants.CTX_BEGIN_RESET);
        }

        private static void SendOrThrow(Action sendAction, string context)
        {
            try
            {
                sendAction();
            }
            catch (SmtpException ex)
            {
                throw AuthServiceContext.ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_SMTP,
                    context == AuthServiceConstants.CTX_BEGIN_REGISTER
                        ? AuthServiceConstants.MESSAGE_VERIFICATION_EMAIL_FAILED
                        : AuthServiceConstants.MESSAGE_PASSWORD_RESET_EMAIL_FAILED,
                    context,
                    ex);
            }
        }
    }
}
