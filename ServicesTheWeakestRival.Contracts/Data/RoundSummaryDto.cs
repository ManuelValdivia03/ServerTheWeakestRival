using System.Collections.Generic;
using System.Runtime.Serialization;
using TheWeakestRival.Contracts.Enums;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class RoundSummaryDto
    {
        [DataMember(Order = 1)]
        public int MatchId { get; set; }

        [DataMember(Order = 2)]
        public int RoundNumber { get; set; }

        [DataMember(Order = 3)]
        public PlayerRoundScoreDto[] PlayerScores { get; set; }
    }

    [DataContract]
    public sealed class PlayerRoundScoreDto
    {
        [DataMember(Order = 1)]
        public int UserId { get; set; }

        [DataMember(Order = 2)]
        public int CorrectAnswers { get; set; }

        [DataMember(Order = 3)]
        public int WrongAnswers { get; set; }

        [DataMember(Order = 4)]
        public decimal BankedPoints { get; set; }
    }

    [DataContract]
    public sealed class VoteCountDto
    {
        [DataMember(Order = 1)]
        public int TargetUserId { get; set; }

        [DataMember(Order = 2)]
        public int VotesCount { get; set; }
    }

    [DataContract]
    public sealed class VoteResolutionResultDto
    {
        [DataMember(Order = 1)]
        public int MatchId { get; set; }

        [DataMember(Order = 2)]
        public int WeakestRivalUserId { get; set; }

        [DataMember(Order = 3)]
        public IReadOnlyCollection<VoteCountDto> VoteCounts { get; set; }
    }

    [DataContract]
    public sealed class VoteRevealEntryDto
    {
        [DataMember(Order = 1)]
        public int VoterUserId { get; set; }

        [DataMember(Order = 2)]
        public int? TargetUserId { get; set; }
    }

    [DataContract]
    public sealed class VoteRevealDto
    {
        [DataMember(Order = 1)]
        public int MatchId { get; set; }

        [DataMember(Order = 2)]
        public VoteRevealEntryDto[] Entries { get; set; }
    }

    [DataContract]
    public sealed class CoinTossResultDto
    {
        [DataMember(Order = 1)]
        public int MatchId { get; set; }

        [DataMember(Order = 2)]
        public CoinFlipResultType Result { get; set; }

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public VoteRevealDto Reveal { get; set; }
    }

    [DataContract]
    public sealed class DuelStartInfoDto
    {
        [DataMember(Order = 1)]
        public int MatchId { get; set; }

        [DataMember(Order = 2)]
        public int WeakestRivalUserId { get; set; }

        [DataMember(Order = 3)]
        public int[] VoterUserIds { get; set; }
    }

    [DataContract]
    public sealed class DuelResolutionDto
    {
        [DataMember(Order = 1)]
        public int MatchId { get; set; }

        [DataMember(Order = 2)]
        public DuelOutcome Outcome { get; set; }

        [DataMember(Order = 3)]
        public int EliminatedUserId { get; set; }
    }

    [DataContract]
    public sealed class EliminationResultDto
    {
        [DataMember(Order = 1)]
        public int MatchId { get; set; }

        [DataMember(Order = 2)]
        public int EliminatedUserId { get; set; }
    }
}
