using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using log4net;

namespace ServicesTheWeakestRival.Server.Services.Chat
{
    public static class ChatServiceContext
    {
        private const string AUTH_LOG_FORMAT = "{0}: {1}";

        private const string AUTH_REASON_MISSING_TOKEN = "missing auth token.";
        private const string AUTH_REASON_INVALID_TOKEN = "invalid auth token.";
        private const string AUTH_REASON_INVALID_USER_ID = "token with invalid UserId.";

        private const string AUTH_LOG_EXPIRED_TOKEN_TEMPLATE = "{0}: expired token for UserId={1}.";
        private const string LOG_CHAT_SERVICE_FAULT_TEMPLATE = "ChatService fault. Code='{0}', Message='{1}'";

        private const int DEFAULT_SINCE_ID = 0;
        private const int MIN_SINCE_ID = 0;

        private const int MIN_PAGE_SIZE = 1;

        private const int EMPTY_TEXT_LENGTH = 0;

        private const int MIN_VALID_USER_ID = 1;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(ChatServiceContext));

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        public static string ResolveConnectionString(string name)
        {
            ConnectionStringSettings setting = ConfigurationManager.ConnectionStrings[name];

            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                Logger.Error(ChatServiceConstants.MESSAGE_CONFIG_MISSING);
                throw ThrowFault(ChatServiceConstants.ERROR_CONFIG, ChatServiceConstants.MESSAGE_CONFIG_MISSING);
            }

            return setting.ConnectionString;
        }

        public static int Authenticate(string authToken)
        {
            if (string.IsNullOrWhiteSpace(authToken))
            {
                Logger.WarnFormat(AUTH_LOG_FORMAT, ChatServiceConstants.CTX_AUTH, AUTH_REASON_MISSING_TOKEN);
                throw ThrowFault(ChatServiceConstants.ERROR_UNAUTHORIZED, ChatServiceConstants.MESSAGE_TOKEN_REQUIRED);
            }

            if (!TokenCache.TryGetValue(authToken, out AuthToken token) || token == null)
            {
                Logger.WarnFormat(AUTH_LOG_FORMAT, ChatServiceConstants.CTX_AUTH, AUTH_REASON_INVALID_TOKEN);
                throw ThrowFault(ChatServiceConstants.ERROR_UNAUTHORIZED, ChatServiceConstants.MESSAGE_TOKEN_INVALID);
            }

            if (token.ExpiresAtUtc <= DateTime.UtcNow)
            {
                Logger.WarnFormat(
                    AUTH_LOG_EXPIRED_TOKEN_TEMPLATE,
                    ChatServiceConstants.CTX_AUTH,
                    token.UserId);

                throw ThrowFault(ChatServiceConstants.ERROR_UNAUTHORIZED, ChatServiceConstants.MESSAGE_TOKEN_EXPIRED);
            }

            if (token.UserId < MIN_VALID_USER_ID)
            {
                Logger.WarnFormat(AUTH_LOG_FORMAT, ChatServiceConstants.CTX_AUTH, AUTH_REASON_INVALID_USER_ID);
                throw ThrowFault(ChatServiceConstants.ERROR_UNAUTHORIZED, ChatServiceConstants.MESSAGE_TOKEN_INVALID);
            }

            return token.UserId;
        }

        public static string ValidateAndNormalizeMessageText(SendChatMessageRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ChatServiceConstants.ERROR_INVALID_REQUEST, ChatServiceConstants.MESSAGE_REQUEST_NULL);
            }

            string messageText = (request.MessageText ?? string.Empty).Trim();

            if (messageText.Length == EMPTY_TEXT_LENGTH)
            {
                throw ThrowFault(ChatServiceConstants.ERROR_VALIDATION, ChatServiceConstants.MESSAGE_TEXT_EMPTY);
            }

            if (messageText.Length > ChatServiceConstants.MAX_MESSAGE_LENGTH)
            {
                string message =
                    ChatServiceConstants.MESSAGE_TEXT_TOO_LONG_PREFIX
                    + ChatServiceConstants.MAX_MESSAGE_LENGTH
                    + ChatServiceConstants.MESSAGE_TEXT_TOO_LONG_SUFFIX;

                throw ThrowFault(ChatServiceConstants.ERROR_VALIDATION, message);
            }

            return messageText;
        }

        public static int ResolveSinceId(int? sinceChatMessageId)
        {
            int sinceId = sinceChatMessageId.GetValueOrDefault(DEFAULT_SINCE_ID);
            return sinceId < MIN_SINCE_ID ? MIN_SINCE_ID : sinceId;
        }

        public static int ResolveMaxCount(int? maxCount)
        {
            int requested = maxCount.GetValueOrDefault(ChatServiceConstants.DEFAULT_PAGE_SIZE);

            if (requested < MIN_PAGE_SIZE)
            {
                requested = MIN_PAGE_SIZE;
            }

            if (requested > ChatServiceConstants.MAX_PAGE_SIZE)
            {
                requested = ChatServiceConstants.MAX_PAGE_SIZE;
            }

            return requested;
        }

        public static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat(
                LOG_CHAT_SERVICE_FAULT_TEMPLATE,
                code,
                message);

            ServiceFault fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        public static void ThrowTechnicalFault(string code, string userMessage, string context, Exception ex)
        {
            Logger.Error(context, ex);

            ServiceFault fault = new ServiceFault
            {
                Code = code,
                Message = userMessage
            };

            throw new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }
    }
}
