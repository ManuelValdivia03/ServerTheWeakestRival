using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using System.Collections.Generic;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(
    InstanceContextMode = InstanceContextMode.Single,
    ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class StatsService : IStatsService
    {
        public GetLeaderboardResponse GetLeaderboard(GetLeaderboardRequest request)
        {
            return new GetLeaderboardResponse
            {
                Entries = new List<LeaderboardEntry>()
            };
        }

        public GetPlayerStatsResponse GetPlayerStats(GetPlayerStatsRequest request)
        {
            return new GetPlayerStatsResponse
            {
                Stats = new LeaderboardEntry { PlayerName = request.PlayerName, Wins = 0, BestBank = 0m }
            };
        }
    }
}
