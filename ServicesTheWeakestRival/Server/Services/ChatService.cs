using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Chat;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(
    InstanceContextMode = InstanceContextMode.Single,
    ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class ChatService : IChatService
    {
        private readonly ChatLogic _logic;

        public ChatService()
        {
            _logic = new ChatLogic();
        }

        public BasicResponse SendChatMessage(SendChatMessageRequest request)
        {
            return _logic.SendChatMessage(request);
        }

        public GetChatMessagesResponse GetChatMessages(GetChatMessagesRequest request)
        {
            return _logic.GetChatMessages(request);
        }
    }
}
