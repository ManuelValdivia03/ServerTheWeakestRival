using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    // ----- SendFriendRequest -----
    [DataContract]
    public class SendFriendRequestRequest
    {
        [DataMember(Order = 1)] public string Token { get; set; } = string.Empty;
        [DataMember(Order = 2)] public int TargetAccountId { get; set; }
        [DataMember(Order = 3)] public string Message { get; set; }
    }

    [DataContract]
    public class SendFriendRequestResponse
    {
        [DataMember(Order = 1)] public int FriendRequestId { get; set; }
        [DataMember(Order = 2)] public FriendRequestStatus Status { get; set; }
    }

    // ----- AcceptFriendRequest -----
    [DataContract]
    public class AcceptFriendRequestRequest
    {
        [DataMember(Order = 1)] public string Token { get; set; } = string.Empty;
        [DataMember(Order = 2)] public int FriendRequestId { get; set; }
    }

    [DataContract]
    public class AcceptFriendRequestResponse
    {
        [DataMember(Order = 1)] public FriendSummary NewFriend { get; set; } = new FriendSummary();
    }

    // ----- RejectFriendRequest -----
    [DataContract]
    public class RejectFriendRequestRequest
    {
        [DataMember(Order = 1)] public string Token { get; set; } = string.Empty;
        [DataMember(Order = 2)] public int FriendRequestId { get; set; }
    }

    [DataContract]
    public class RejectFriendRequestResponse
    {
        [DataMember(Order = 1)] public FriendRequestStatus Status { get; set; }
    }

    // ----- RemoveFriend -----
    [DataContract]
    public class RemoveFriendRequest
    {
        [DataMember(Order = 1)] public string Token { get; set; } = string.Empty;
        [DataMember(Order = 2)] public int FriendAccountId { get; set; }
    }

    [DataContract]
    public class RemoveFriendResponse
    {
        [DataMember(Order = 1)] public bool Removed { get; set; }
    }

    // ----- ListFriends -----
    [DataContract]
    public class ListFriendsRequest
    {
        [DataMember(Order = 1)] public string Token { get; set; } = string.Empty;
        [DataMember(Order = 2)] public bool IncludePendingIncoming { get; set; } = true;
        [DataMember(Order = 3)] public bool IncludePendingOutgoing { get; set; } = true;
    }

    [DataContract]
    public class ListFriendsResponse
    {
        [DataMember(Order = 1)] public FriendSummary[] Friends { get; set; } = System.Array.Empty<FriendSummary>();
        [DataMember(Order = 2)] public FriendRequestSummary[] PendingIncoming { get; set; } = System.Array.Empty<FriendRequestSummary>();
        [DataMember(Order = 3)] public FriendRequestSummary[] PendingOutgoing { get; set; } = System.Array.Empty<FriendRequestSummary>();
    }
}
