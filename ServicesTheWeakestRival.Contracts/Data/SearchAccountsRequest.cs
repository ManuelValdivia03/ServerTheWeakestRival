using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class SearchAccountsRequest
    {
        public const int DEFAULT_MAX_RESULTS = 20;

        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; } = string.Empty;

        [DataMember(Order = 2, IsRequired = true)]
        public string Query { get; set; } = string.Empty;

        [DataMember(Order = 3, IsRequired = true)]
        public int MaxResults { get; set; } = DEFAULT_MAX_RESULTS;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Token = Token ?? string.Empty;
            Query = Query ?? string.Empty;

            if (MaxResults <= 0)
            {
                MaxResults = DEFAULT_MAX_RESULTS;
            }
        }
    }

    [DataContract]
    public sealed class SearchAccountItem
    {
        [DataMember(Order = 1, IsRequired = true)]
        public int AccountId { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public string DisplayName { get; set; } = string.Empty;

        [DataMember(Order = 3, IsRequired = true)]
        public string Email { get; set; } = string.Empty;

        [DataMember(Order = 4, IsRequired = true)]
        public bool HasProfileImage { get; set; }

        [DataMember(Order = 5, IsRequired = true)]
        public string ProfileImageCode { get; set; } = string.Empty;

        [DataMember(Order = 6, IsRequired = true)]
        public bool IsFriend { get; set; }

        [DataMember(Order = 7, IsRequired = true)]
        public bool HasPendingOutgoing { get; set; }

        [DataMember(Order = 8, IsRequired = true)]
        public bool HasPendingIncoming { get; set; }

        [DataMember(Order = 9, IsRequired = false)]
        public int? PendingIncomingRequestId { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            DisplayName = DisplayName ?? string.Empty;
            Email = Email ?? string.Empty;
            ProfileImageCode = ProfileImageCode ?? string.Empty;
        }
    }

    [DataContract]
    public sealed class SearchAccountsResponse
    {
        [DataMember(Order = 1, IsRequired = true)]
        public SearchAccountItem[] Results { get; set; } = Array.Empty<SearchAccountItem>();

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Results = Results ?? Array.Empty<SearchAccountItem>();
        }
    }
}
