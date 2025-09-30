using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class MatchInfo
    {
        [DataMember(Order = 1)] public Guid MatchId { get; set; }
        [DataMember(Order = 2)] public string MatchCode { get; set; }
        [DataMember(Order = 3)]public List<PlayerSummary> Players { get; set; } = new List<PlayerSummary>();
        [DataMember(Order = 4)] public string State { get; set; }
    }

    [DataContract]
    public sealed class CreateMatchRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public int MaxPlayers { get; set; }
    }

    [DataContract]
    public sealed class CreateMatchResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public MatchInfo Match { get; set; }
    }

    [DataContract]
    public sealed class JoinMatchRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string MatchCode { get; set; }
    }

    [DataContract]
    public sealed class JoinMatchResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public MatchInfo Match { get; set; }
    }

    [DataContract]
    public sealed class LeaveMatchRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public Guid MatchId { get; set; }
    }

    [DataContract]
    public sealed class StartMatchRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public Guid MatchId { get; set; }
    }

    [DataContract]
    public sealed class StartMatchResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public MatchInfo Match { get; set; }
    }

    [DataContract]
    public sealed class ListOpenMatchesRequest
    {
        [DataMember(Order = 1)] public int? Top { get; set; }
    }

    [DataContract]
    public sealed class ListOpenMatchesResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public List<MatchInfo> Matches { get; set; } = new List<MatchInfo>();

    }
}
