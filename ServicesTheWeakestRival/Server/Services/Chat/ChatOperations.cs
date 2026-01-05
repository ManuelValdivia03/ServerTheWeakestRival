using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using log4net;

namespace ServicesTheWeakestRival.Server.Services.Chat
{
    public sealed class ChatOperations
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ChatOperations));

        private readonly ChatRepository chatRepository;

        public ChatOperations(ChatRepository chatRepository)
        {
            this.chatRepository = chatRepository ?? throw new ArgumentNullException(nameof(chatRepository));
        }

        public BasicResponse SendChatMessage(SendChatMessageRequest request)
        {
            var messageText = ChatServiceContext.ValidateAndNormalizeMessageText(request);

            var userId = ChatServiceContext.Authenticate(request.AuthToken);
            var displayName = chatRepository.GetUserDisplayName(userId);

            Logger.InfoFormat(
                "SendChatMessage: UserId={0}, DisplayName={1}, Length={2}",
                userId,
                displayName,
                messageText.Length);

            try
            {
                chatRepository.InsertChatMessage(userId, displayName, messageText);

                return new BasicResponse
                {
                    IsSuccess = true,
                    Message = "Message sent."
                };
            }
            catch (SqlException ex)
            {
                ChatServiceContext.ThrowTechnicalFault(
                    ChatServiceConstants.ERROR_DB,
                    ChatServiceConstants.MESSAGE_DB_SEND,
                    ChatServiceConstants.CTX_SEND,
                    ex);
            }
            catch (TimeoutException ex)
            {
                ChatServiceContext.ThrowTechnicalFault(
                    ChatServiceConstants.ERROR_TIMEOUT,
                    ChatServiceConstants.MESSAGE_TIMEOUT_SEND,
                    ChatServiceConstants.CTX_SEND,
                    ex);
            }
            catch (OperationCanceledException ex)
            {
                ChatServiceContext.ThrowTechnicalFault(
                    ChatServiceConstants.ERROR_CANCELLED,
                    ChatServiceConstants.MESSAGE_CANCEL_SEND,
                    ChatServiceConstants.CTX_SEND,
                    ex);
            }
            catch (InvalidOperationException ex)
            {
                ChatServiceContext.ThrowTechnicalFault(
                    ChatServiceConstants.ERROR_INVALID_OPERATION,
                    ChatServiceConstants.MESSAGE_INVALIDOP_SEND,
                    ChatServiceConstants.CTX_SEND,
                    ex);
            }
            catch (Exception ex)
            {
                ChatServiceContext.ThrowTechnicalFault(
                    ChatServiceConstants.ERROR_UNEXPECTED,
                    ChatServiceConstants.MESSAGE_UNEXPECTED_SEND,
                    ChatServiceConstants.CTX_SEND,
                    ex);
            }

            return new BasicResponse
            {
                IsSuccess = false,
                Message = "Unreachable."
            };
        }

        public GetChatMessagesResponse GetChatMessages(GetChatMessagesRequest request)
        {
            if (request == null)
            {
                throw ChatServiceContext.ThrowFault(ChatServiceConstants.ERROR_INVALID_REQUEST, ChatServiceConstants.MESSAGE_REQUEST_NULL);
            }

            _ = ChatServiceContext.Authenticate(request.AuthToken);

            var sinceId = ChatServiceContext.ResolveSinceId(request.SinceChatMessageId);
            var maxCount = ChatServiceContext.ResolveMaxCount(request.MaxCount);

            Logger.DebugFormat(
                "GetChatMessages: SinceId={0}, EffectiveMax={1}",
                sinceId,
                maxCount);

            try
            {
                var page = chatRepository.GetMessagesPaged(maxCount, sinceId);

                var messages = page.Messages ?? new List<ChatMessageDto>();
                var responseMessages = messages.Count == 0 ? Array.Empty<ChatMessageDto>() : messages.ToArray();

                Logger.InfoFormat(
                    "GetChatMessages: Returned {0} messages. SinceId={1}, LastId={2}",
                    responseMessages.Length,
                    sinceId,
                    page.LastChatMessageId);

                return new GetChatMessagesResponse
                {
                    Messages = responseMessages,
                    LastChatMessageId = page.LastChatMessageId
                };
            }
            catch (SqlException ex)
            {
                ChatServiceContext.ThrowTechnicalFault(
                    ChatServiceConstants.ERROR_DB,
                    ChatServiceConstants.MESSAGE_DB_GET,
                    ChatServiceConstants.CTX_GET,
                    ex);
            }
            catch (TimeoutException ex)
            {
                ChatServiceContext.ThrowTechnicalFault(
                    ChatServiceConstants.ERROR_TIMEOUT,
                    ChatServiceConstants.MESSAGE_TIMEOUT_GET,
                    ChatServiceConstants.CTX_GET,
                    ex);
            }
            catch (OperationCanceledException ex)
            {
                ChatServiceContext.ThrowTechnicalFault(
                    ChatServiceConstants.ERROR_CANCELLED,
                    ChatServiceConstants.MESSAGE_CANCEL_GET,
                    ChatServiceConstants.CTX_GET,
                    ex);
            }
            catch (InvalidOperationException ex)
            {
                ChatServiceContext.ThrowTechnicalFault(
                    ChatServiceConstants.ERROR_INVALID_OPERATION,
                    ChatServiceConstants.MESSAGE_INVALIDOP_GET,
                    ChatServiceConstants.CTX_GET,
                    ex);
            }
            catch (Exception ex)
            {
                ChatServiceContext.ThrowTechnicalFault(
                    ChatServiceConstants.ERROR_UNEXPECTED,
                    ChatServiceConstants.MESSAGE_UNEXPECTED_GET,
                    ChatServiceConstants.CTX_GET,
                    ex);
            }

            return new GetChatMessagesResponse
            {
                Messages = Array.Empty<ChatMessageDto>(),
                LastChatMessageId = sinceId
            };
        }
    }
}
