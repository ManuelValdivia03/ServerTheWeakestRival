using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public class HeartbeatRequest
    {
        [DataMember(Order = 1)] public string Token { get; set; }
        [DataMember(Order = 2)] public string Device { get; set; }
    }

    [DataContract]
    public class HeartbeatResponse
    {
        [DataMember(Order = 1)] public DateTime Utc { get; set; }
    }

    [DataContract]
    public class FriendPresence
    {
        [DataMember(Order = 1)] public int AccountId { get; set; }
        [DataMember(Order = 2)] public bool IsOnline { get; set; }
        [DataMember(Order = 3)] public DateTime? LastSeenUtc { get; set; }
    }

    [DataContract]
    public class GetFriendsPresenceRequest
    {
        [DataMember(Order = 1)] public string Token { get; set; }
    }

    [DataContract]
    public class GetFriendsPresenceResponse
    {
        [DataMember(Order = 1)] public FriendPresence[] Friends { get; set; }
    }
}