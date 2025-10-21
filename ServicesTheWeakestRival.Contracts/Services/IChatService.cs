using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    /// <summary>
    /// Exposes operations for the global lobby chat (pre-match).
    /// </summary>
    [ServiceContract]
    public interface IChatService
    {
        /// <summary>Sends a message to the global lobby chat.</summary>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        BasicResponse SendChatMessage(SendChatMessageRequest request);

        /// <summary>Gets recent messages; supports incremental polling via SinceChatMessageId.</summary>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        GetChatMessagesResponse GetChatMessages(GetChatMessagesRequest request);
    }
}
