using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using System;
using System.Configuration;
using System.Net.Mail;
using System.Net.Sockets;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Email
{
    public sealed class AuthEmailDispatcher
    {
        private const string OPERATION_KEY_PREFIX_SEND_VERIFICATION = "Email.SendVerificationCode";
        private const string OPERATION_KEY_PREFIX_SEND_PASSWORD_RESET = "Email.SendPasswordResetCode";

        private readonly IEmailService emailService;

        public AuthEmailDispatcher(IEmailService emailService)
        {
            this.emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        public void SendVerificationCodeOrThrow(string email, string code)
        {
            SendOrThrow(
                () => emailService.SendVerificationCode(email, code, AuthServiceContext.CodeTtlMinutes),
                AuthServiceConstants.CTX_BEGIN_REGISTER,
                OPERATION_KEY_PREFIX_SEND_VERIFICATION);
        }

        public void SendPasswordResetCodeOrThrow(string email, string code)
        {
            SendOrThrow(
                () => emailService.SendPasswordResetCode(email, code, AuthServiceContext.CodeTtlMinutes),
                AuthServiceConstants.CTX_BEGIN_RESET,
                OPERATION_KEY_PREFIX_SEND_PASSWORD_RESET);
        }

        private static void SendOrThrow(Action sendAction, string context, string operationKeyPrefix)
        {
            if (sendAction == null) throw new ArgumentNullException(nameof(sendAction));
            if (string.IsNullOrWhiteSpace(context)) throw new ArgumentException("Context is required.", nameof(context));

            try
            {
                sendAction();
            }
            catch (SmtpFailedRecipientException ex)
            {
                ThrowEmailTechnicalFault(context, operationKeyPrefix, ex);
            }
            catch (SmtpException ex)
            {
                ThrowEmailTechnicalFault(context, operationKeyPrefix, ex);
            }
            catch (TimeoutException ex)
            {
                ThrowEmailTechnicalFault(context, operationKeyPrefix, ex);
            }
            catch (ConfigurationErrorsException ex)
            {
                ThrowEmailTechnicalFault(context, operationKeyPrefix, ex);
            }
            catch (SocketException ex)
            {
                ThrowEmailTechnicalFault(context, operationKeyPrefix, ex);
            }
            catch (Exception ex)
            {
                ThrowEmailTechnicalFault(context, operationKeyPrefix, ex);
            }
        }

        private static void ThrowEmailTechnicalFault(string context, string operationKeyPrefix, Exception ex)
        {
            string messageKey = EmailFaultKeyMapper.Map(operationKeyPrefix, ex);

            throw AuthServiceContext.ThrowTechnicalFault(
                AuthServiceConstants.ERROR_SMTP,
                messageKey,
                context,
                ex);
        }
    }
}
