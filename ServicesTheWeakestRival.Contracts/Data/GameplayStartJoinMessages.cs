using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class GameplayJoinMatchRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public Guid MatchId { get; set; }
    }

    [DataContract]
    public sealed class GameplayJoinMatchResponse
    {
        [DataMember(Order = 1, IsRequired = true)]
        public bool Accepted { get; set; }
    }

    [DataContract]
    public sealed class GameplayStartMatchRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public Guid MatchId { get; set; }

        [DataMember(Order = 3, IsRequired = true)]
        public byte Difficulty { get; set; }

        [DataMember(Order = 4, IsRequired = true)]
        public string LocaleCode { get; set; }

        [DataMember(Order = 5)]
        public int? MaxQuestions { get; set; }
    }

    [DataContract]
    public sealed class GameplayStartMatchResponse
    {
        [DataMember(Order = 1, IsRequired = true)]
        public bool Started { get; set; }
    }
}
