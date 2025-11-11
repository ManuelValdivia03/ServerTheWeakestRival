using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesTheWeakestRival.Contracts.Data
{
    public sealed class WildcardTypeDto
    {
        public int WildcardTypeId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public byte MaxUsesPerMatch { get; set; }
    }

    public sealed class PlayerWildcardDto
    {
        public int PlayerWildcardId { get; set; }

        public int MatchId { get; set; }

        public int UserId { get; set; }

        public int WildcardTypeId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public byte MaxUsesPerMatch { get; set; }

        public DateTime GrantedAt { get; set; }

        public DateTime? ConsumedAt { get; set; }

        public int? ConsumedInRound { get; set; }
    }

    // ===== Requests / Responses del servicio =====

    public sealed class ListWildcardTypesRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    public sealed class ListWildcardTypesResponse
    {
        public WildcardTypeDto[] Types { get; set; } = Array.Empty<WildcardTypeDto>();
    }

    public sealed class GetPlayerWildcardsRequest
    {
        public string Token { get; set; } = string.Empty;

        public int MatchId { get; set; }
    }

    public sealed class GetPlayerWildcardsResponse
    {
        public PlayerWildcardDto[] Wildcards { get; set; } = Array.Empty<PlayerWildcardDto>();
    }

    public sealed class UseWildcardRequest
    {
        public string Token { get; set; } = string.Empty;

        public int MatchId { get; set; }

        public int PlayerWildcardId { get; set; }

        public int RoundNumber { get; set; }
    }

    public sealed class UseWildcardResponse
    {
        public bool IsConsumed { get; set; }

        public PlayerWildcardDto Wildcard { get; set; }
    }
}
