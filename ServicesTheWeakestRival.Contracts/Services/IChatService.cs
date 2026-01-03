using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    [ServiceContract]
    public interface IChatService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        BasicResponse SendChatMessage(SendChatMessageRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        GetChatMessagesResponse GetChatMessages(GetChatMessagesRequest request);
    }
}
