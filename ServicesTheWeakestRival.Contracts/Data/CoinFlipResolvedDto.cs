using TheWeakestRival.Contracts.Enums;

namespace ServicesTheWeakestRival.Contracts.Data
{
    public sealed class CoinFlipResolvedDto
    {
        public int RoundId { get; set; }
        public int WeakestRivalPlayerId { get; set; }
        public CoinFlipResultType Result { get; set; }
        public bool ShouldEnableDuel { get; set; }
    }
}
