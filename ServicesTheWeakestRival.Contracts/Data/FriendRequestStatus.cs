using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public enum FriendRequestStatus
    {
        [EnumMember] Pending = 0,
        [EnumMember] Accepted = 1,
        [EnumMember] Rejected = 2,
        [EnumMember] Cancelled = 3
    }
}
