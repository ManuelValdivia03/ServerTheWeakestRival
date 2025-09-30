using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using System.Collections.Generic;

namespace ServicesTheWeakestRival.Server.Services
{
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
