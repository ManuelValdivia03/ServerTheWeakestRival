namespace ServicesTheWeakestRival.Contracts.Data
{
    public sealed class DuelCandidateDto
    {
        public int UserId { get; set; }

        public string DisplayName { get; set; }

        public AvatarAppearanceDto Avatar { get; set; }
    }

    public sealed class DuelCandidatesDto
    {
        public int WeakestRivalUserId { get; set; }

        public DuelCandidateDto[] Candidates { get; set; }
    }

    public sealed class ChooseDuelOpponentRequest
    {
        public string Token { get; set; }

        public int MatchId { get; set; }

        public int TargetUserId { get; set; }
    }

    public sealed class ChooseDuelOpponentResponse
    {
        public bool Accepted { get; set; }

        public int TargetUserId { get; set; }
    }
}
