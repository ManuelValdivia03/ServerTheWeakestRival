using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class SubmitAnswerRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public Guid MatchId { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public int QuestionId { get; set; }
        [DataMember(Order = 4, IsRequired = true)] public string AnswerText { get; set; }
        [DataMember(Order = 5)] public TimeSpan ResponseTime { get; set; }
    }

    [DataContract]
    public sealed class SubmitAnswerResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public AnswerResult Result { get; set; }
    }

    [DataContract]
    public sealed class AnswerResult
    {
        [DataMember(Order = 1)] public int QuestionId { get; set; }
        [DataMember(Order = 2)] public bool IsCorrect { get; set; }
        [DataMember(Order = 3)] public decimal ChainIncrement { get; set; }
        [DataMember(Order = 4)] public decimal CurrentChain { get; set; }
        [DataMember(Order = 5)] public decimal BankedPoints { get; set; }
    }

    [DataContract]
    public sealed class BankRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public Guid MatchId { get; set; }
    }

    [DataContract]
    public sealed class BankResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public BankState Bank { get; set; }
    }

    [DataContract]
    public sealed class BankState
    {
        [DataMember(Order = 1)] public Guid MatchId { get; set; }
        [DataMember(Order = 2)] public decimal CurrentChain { get; set; }
        [DataMember(Order = 3)] public decimal BankedPoints { get; set; }
    }

    [DataContract]
    public sealed class UseLifelineRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public Guid MatchId { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public string Lifeline { get; set; }
        [DataMember(Order = 4)] public Guid? TargetPlayerId { get; set; }
    }

    [DataContract]
    public sealed class UseLifelineResponse
    {
        [DataMember(Order = 1)] public string Outcome { get; set; }
    }

    [DataContract]
    public sealed class CastVoteRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public Guid MatchId { get; set; }

        [DataMember(Order = 3, EmitDefaultValue = true)]
        public int? TargetUserId { get; set; }
    }

    [DataContract]
    public sealed class CastVoteResponse
    {
        [DataMember(Order = 1)]
        public bool Accepted { get; set; }
    }


    [DataContract]
    public sealed class AckEventSeenRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public Guid MatchId { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public string EventName { get; set; }
    }

    [DataContract]
    public sealed class AckEventSeenResponse
    {
        [DataMember(Order = 1)] public bool Acknowledged { get; set; }
    }
}
