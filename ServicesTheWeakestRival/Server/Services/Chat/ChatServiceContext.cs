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
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ChatServiceContext));

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        public static string ResolveConnectionString(string name)
        {
            var setting = ConfigurationManager.ConnectionStrings[name];

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
                Logger.Warn(ChatServiceConstants.CTX_AUTH + ": missing auth token.");
                throw ThrowFault(ChatServiceConstants.ERROR_UNAUTHORIZED, ChatServiceConstants.MESSAGE_TOKEN_REQUIRED);
            }

            if (!TokenCache.TryGetValue(authToken, out var token) || token == null)
            {
                Logger.Warn(ChatServiceConstants.CTX_AUTH + ": invalid auth token.");
                throw ThrowFault(ChatServiceConstants.ERROR_UNAUTHORIZED, ChatServiceConstants.MESSAGE_TOKEN_INVALID);
            }

            if (token.ExpiresAtUtc <= DateTime.UtcNow)
            {
                Logger.WarnFormat(
                    "{0}: expired token for UserId={1}.",
                    ChatServiceConstants.CTX_AUTH,
                    token.UserId);

                throw ThrowFault(ChatServiceConstants.ERROR_UNAUTHORIZED, ChatServiceConstants.MESSAGE_TOKEN_EXPIRED);
            }

            if (token.UserId <= 0)
            {
                Logger.Warn(ChatServiceConstants.CTX_AUTH + ": token with invalid UserId.");
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

            var messageText = (request.MessageText ?? string.Empty).Trim();

            if (messageText.Length == 0)
            {
                throw ThrowFault(ChatServiceConstants.ERROR_VALIDATION, ChatServiceConstants.MESSAGE_TEXT_EMPTY);
            }

            if (messageText.Length > ChatServiceConstants.MAX_MESSAGE_LENGTH)
            {
                var message =
                    ChatServiceConstants.MESSAGE_TEXT_TOO_LONG_PREFIX
                    + ChatServiceConstants.MAX_MESSAGE_LENGTH
                    + ChatServiceConstants.MESSAGE_TEXT_TOO_LONG_SUFFIX;

                throw ThrowFault(ChatServiceConstants.ERROR_VALIDATION, message);
            }

            return messageText;
        }

        public static int ResolveSinceId(int? sinceChatMessageId)
        {
            var sinceId = sinceChatMessageId.GetValueOrDefault(0);
            return sinceId < 0 ? 0 : sinceId;
        }

        public static int ResolveMaxCount(int? maxCount)
        {
            var requested = maxCount.GetValueOrDefault(ChatServiceConstants.DEFAULT_PAGE_SIZE);

            if (requested < 1)
            {
                requested = 1;
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
                "ChatService fault. Code='{0}', Message='{1}'",
                code,
                message);

            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        public static void ThrowTechnicalFault(string code, string userMessage, string context, Exception ex)
        {
            Logger.Error(context, ex);

            var fault = new ServiceFault
            {
                Code = code,
                Message = userMessage
            };

            throw new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }
    }
}
