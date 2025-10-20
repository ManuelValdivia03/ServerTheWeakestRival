using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    /// <summary>Resumen de un amigo (relación aceptada)</summary>
    [DataContract]
    public class FriendSummary
    {
        [DataMember(Order = 1)] public int AccountId { get; set; }
        [DataMember(Order = 2)] public string Username { get; set; } = string.Empty;
        [DataMember(Order = 3)] public string DisplayName { get; set; }
        [DataMember(Order = 4)] public string AvatarUrl { get; set; }
        [DataMember(Order = 5)] public DateTime SinceUtc { get; set; }
        [DataMember(Order = 6)] public bool IsOnline { get; set; }
    }

    /// <summary>Resumen de una solicitud de amistad</summary>
    [DataContract]
    public class FriendRequestSummary
    {
        [DataMember(Order = 1)] public int FriendRequestId { get; set; }
        [DataMember(Order = 2)] public int FromAccountId { get; set; }
        [DataMember(Order = 3)] public int ToAccountId { get; set; }
        [DataMember(Order = 4)] public string Message { get; set; }
        [DataMember(Order = 5)] public FriendRequestStatus Status { get; set; }
        [DataMember(Order = 6)] public DateTime CreatedUtc { get; set; }
        [DataMember(Order = 7)] public DateTime? ResolvedUtc { get; set; }
    }
}
