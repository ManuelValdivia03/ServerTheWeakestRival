using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Stats;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(
    InstanceContextMode = InstanceContextMode.Single,
    ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class StatsService : IStatsService
    {
        private readonly StatsLogic _statsLogic;

        public StatsService()
        {
            _statsLogic = new StatsLogic();
        }

        public GetLeaderboardResponse GetLeaderboard(GetLeaderboardRequest request)
        {
            return _statsLogic.GetLeaderboard(request);
        }

        public GetPlayerStatsResponse GetPlayerStats(GetPlayerStatsRequest request)
        {
            return _statsLogic.GetPlayerStats(request);
        }
    }
}
