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
        private readonly ChatOperations chatOperations;

        public ChatService()
        {
            var repository = new ChatRepository(
                () => ChatServiceContext.ResolveConnectionString(ChatServiceConstants.MAIN_CONNECTION_STRING_NAME));

            chatOperations = new ChatOperations(repository);
        }

        public BasicResponse SendChatMessage(SendChatMessageRequest request)
        {
            return chatOperations.SendChatMessage(request);
        }

        public GetChatMessagesResponse GetChatMessages(GetChatMessagesRequest request)
        {
            return chatOperations.GetChatMessages(request);
        }
    }
}
