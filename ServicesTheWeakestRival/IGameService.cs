using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

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
