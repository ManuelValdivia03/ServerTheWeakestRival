using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class FriendSummary
    {
        [DataMember(Order = 1, IsRequired = true)]
        public int AccountId { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public string Username { get; set; } = string.Empty;

        [DataMember(Order = 3, IsRequired = true)]
        public string DisplayName { get; set; } = string.Empty;

        [DataMember(Order = 4, IsRequired = true)]
        public bool HasProfileImage { get; set; }

        [DataMember(Order = 5, IsRequired = true)]
        public DateTime SinceUtc { get; set; }

        [DataMember(Order = 6, IsRequired = true)]
        public bool IsOnline { get; set; }

        [DataMember(Order = 7, IsRequired = true)]
        public string ProfileImageCode { get; set; } = string.Empty;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Username = Username ?? string.Empty;
            DisplayName = DisplayName ?? string.Empty;
            ProfileImageCode = ProfileImageCode ?? string.Empty;
        }
    }

    [DataContract]
    public sealed class FriendRequestSummary
    {
        [DataMember(Order = 1)] public int FriendRequestId { get; set; }
        [DataMember(Order = 2)] public int FromAccountId { get; set; }
        [DataMember(Order = 3)] public int ToAccountId { get; set; }
        [DataMember(Order = 4)] public string Message { get; set; } = string.Empty;
        [DataMember(Order = 5)] public FriendRequestStatus Status { get; set; }
        [DataMember(Order = 6)] public DateTime CreatedUtc { get; set; }
        [DataMember(Order = 7)] public DateTime? ResolvedUtc { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Message = Message ?? string.Empty;
        }
    }
}
