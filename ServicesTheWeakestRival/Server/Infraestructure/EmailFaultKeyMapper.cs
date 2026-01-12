using System;
using System.Configuration;
using System.Net.Mail;
using System.Net.Sockets;

namespace ServicesTheWeakestRival.Server.Infrastructure
{
    internal static class EmailFaultKeyMapper
    {
        private const string DEFAULT_OPERATION_KEY_PREFIX = "Email.SendVerificationCode";

        private const string SUFFIX_INVALID_RECIPIENT = ".InvalidRecipient";
        private const string SUFFIX_CONFIGURATION = ".Configuration";
        private const string SUFFIX_UNEXPECTED = ".Unexpected";

        private const string SUFFIX_SOCKET = ".Socket.";
        private const string SUFFIX_SMTP = ".Smtp.";

        private const int SOCKET_TIMEOUT_ERROR_CODE = 10060;

        internal static string Map(string operationKeyPrefix, Exception ex)
        {
            string prefix = NormalizePrefix(operationKeyPrefix);

            if (ex == null)
            {
                return prefix + SUFFIX_UNEXPECTED;
            }

            if (ex is ConfigurationErrorsException)
            {
                return prefix + SUFFIX_CONFIGURATION;
            }

            if (ex is FormatException || ex is ArgumentException)
            {
                return prefix + SUFFIX_INVALID_RECIPIENT;
            }

            if (ex is SmtpFailedRecipientException failedRecipientEx)
            {
                return prefix + SUFFIX_SMTP + failedRecipientEx.StatusCode;
            }

            if (ex is SmtpFailedRecipientsException)
            {
                return prefix + SUFFIX_SMTP + SmtpStatusCode.MailboxUnavailable;
            }

            if (ex is SmtpException smtpEx)
            {
                SocketException socketEx = smtpEx.InnerException as SocketException;
                if (socketEx != null)
                {
                    return prefix + SUFFIX_SOCKET + socketEx.ErrorCode;
                }

                return prefix + SUFFIX_SMTP + smtpEx.StatusCode;
            }

            if (ex is TimeoutException)
            {
                return prefix + SUFFIX_SOCKET + SOCKET_TIMEOUT_ERROR_CODE;
            }

            return prefix + SUFFIX_UNEXPECTED;
        }

        private static string NormalizePrefix(string operationKeyPrefix)
        {
            string safePrefix = (operationKeyPrefix ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(safePrefix))
            {
                return DEFAULT_OPERATION_KEY_PREFIX;
            }

            return safePrefix;
        }
    }
}
