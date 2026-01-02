using System.Collections.Generic;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Server.Services.Stats
{
    internal sealed class StatsLogic
    {
        internal GetLeaderboardResponse GetLeaderboard(GetLeaderboardRequest request)
        {
            return new GetLeaderboardResponse
            {
                Entries = new List<LeaderboardEntry>()
            };
        }

        internal GetPlayerStatsResponse GetPlayerStats(GetPlayerStatsRequest request)
        {
            return new GetPlayerStatsResponse
            {
                Stats = new LeaderboardEntry { PlayerName = request.PlayerName, Wins = 0, BestBank = 0m }
            };
        }
    }
}
