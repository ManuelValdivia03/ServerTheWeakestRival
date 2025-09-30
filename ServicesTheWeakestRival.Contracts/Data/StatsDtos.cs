using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class LeaderboardEntry
    {
        [DataMember(Order = 1)] public Guid PlayerId { get; set; }
        [DataMember(Order = 2)] public string PlayerName { get; set; }
        [DataMember(Order = 3)] public int Wins { get; set; }
        [DataMember(Order = 4)] public decimal BestBank { get; set; }
    }

    [DataContract]
    public sealed class GetLeaderboardRequest
    {
        [DataMember(Order = 1)] public int Top { get; set; } = 10;
    }

    [DataContract]
    public sealed class GetLeaderboardResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public List<LeaderboardEntry> Entries { get; set; } = new List<LeaderboardEntry>();
    }

    [DataContract]
    public sealed class GetPlayerStatsRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string PlayerName { get; set; }
    }

    [DataContract]
    public sealed class GetPlayerStatsResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public LeaderboardEntry Stats { get; set; }
    }
}
