using System.ServiceModel;

namespace ServicesTheWeakestRival
{
    [ServiceContract]
    public interface IGameService
    {
        [OperationContract]
        string Ping(string message);

        [OperationContract]
        string Register(string email, string password ,string playerName);

    }
    }
