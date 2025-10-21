using System;

namespace ServicesTheWeakestRival.Contracts.Data
{
    public sealed class ChatMessageDto
    {
        public int ChatMessageId { get; set; }
        public int UserId { get; set; }
        public string DisplayName { get; set; }
        public string MessageText { get; set; }
        public DateTime SentUtc { get; set; }
    }
    public sealed class SendChatMessageRequest
    {
        public string AuthToken { get; set; }
        public string MessageText { get; set; }
    }

    public sealed class GetChatMessagesRequest
    {
        public string AuthToken { get; set; }
        public int? SinceChatMessageId { get; set; }
        public int? MaxCount { get; set; }
    }
    public sealed class GetChatMessagesResponse
    {
        public ChatMessageDto[] Messages { get; set; }
        public int LastChatMessageId { get; set; }
    }
    public sealed class BasicResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
    }
}
