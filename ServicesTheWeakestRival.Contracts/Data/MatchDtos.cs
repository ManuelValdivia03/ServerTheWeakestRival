using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class MatchInfo
    {
        [DataMember(Order = 1)]
        public Guid MatchId { get; set; }

        [DataMember(Order = 2)]
        public string MatchCode { get; set; }

        [DataMember(Order = 3)]
        public int MatchDbId { get; set; }

        [DataMember(Order = 4)]
        public List<PlayerSummary> Players { get; set; } = new List<PlayerSummary>();

        [DataMember(Order = 5)]
        public string State { get; set; }

        [DataMember(Order = 6, EmitDefaultValue = false)]
        public MatchConfigDto Config { get; set; }
    }

    [DataContract]
    public sealed class MatchConfigDto
    {
        [DataMember(Order = 1)] public decimal StartingScore { get; set; }
        [DataMember(Order = 2)] public decimal MaxScore { get; set; }
        [DataMember(Order = 3)] public decimal PointsPerCorrect { get; set; }
        [DataMember(Order = 4)] public decimal PointsPerWrong { get; set; }
        [DataMember(Order = 5)] public decimal PointsPerEliminationGain { get; set; }
        [DataMember(Order = 6)] public bool AllowTiebreakCoinflip { get; set; }

        [DataMember(Order = 7)]
        public string DifficultyCode { get; set; }

        [DataMember(Order = 8)]
        public string CharacterCode { get; set; }
    }


    [DataContract]
    public sealed class CreateMatchRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public int MaxPlayers { get; set; }

        [DataMember(Order = 3, IsRequired = true)]
        public bool IsPrivate { get; set; }

        [DataMember(Order = 4, IsRequired = true)]
        public MatchConfigDto Config { get; set; }
    }

    [DataContract]
    public sealed class CreateMatchResponse
    {
        [DataMember(Order = 1, IsRequired = true)]
        public MatchInfo Match { get; set; }
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
        [DataMember(Order = 1, IsRequired = true)]
        public List<MatchInfo> Matches { get; set; } = new List<MatchInfo>();
    }
}
