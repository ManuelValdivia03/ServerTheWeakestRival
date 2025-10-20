using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public class SearchAccountsRequest
    {
        [DataMember(Order = 1)] public string Token { get; set; } = string.Empty;
        [DataMember(Order = 2)] public string Query { get; set; } = string.Empty; // nombre o correo (parcial)
        [DataMember(Order = 3)] public int MaxResults { get; set; } = 20;
    }

    [DataContract]
    public class SearchAccountItem
    {
        [DataMember(Order = 1)] public int AccountId { get; set; }
        [DataMember(Order = 2)] public string DisplayName { get; set; } = string.Empty;
        [DataMember(Order = 3)] public string Email { get; set; } = string.Empty;
        [DataMember(Order = 4)] public string AvatarUrl { get; set; }

        // flags para UI
        [DataMember(Order = 5)] public bool IsFriend { get; set; }
        [DataMember(Order = 6)] public bool HasPendingOutgoing { get; set; }
        [DataMember(Order = 7)] public bool HasPendingIncoming { get; set; }
        [DataMember(Order = 8)] public int? PendingIncomingRequestId { get; set; }
    }

    [DataContract]
    public class SearchAccountsResponse
    {
        [DataMember(Order = 1)] public SearchAccountItem[] Results { get; set; } = Array.Empty<SearchAccountItem>();
    }
}
