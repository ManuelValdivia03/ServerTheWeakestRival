using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    [ServiceContract]
    public interface IStatsService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        GetLeaderboardResponse GetLeaderboard(GetLeaderboardRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        GetPlayerStatsResponse GetPlayerStats(GetPlayerStatsRequest request);
    }
}
