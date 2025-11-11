using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Threading.Tasks;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    [ServiceContract]
    public interface IWildcardService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        ListWildcardTypesResponse ListWildcardTypes(ListWildcardTypesRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        GetPlayerWildcardsResponse GetPlayerWildcards(GetPlayerWildcardsRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        UseWildcardResponse UseWildcard(UseWildcardRequest request);
    }
}
