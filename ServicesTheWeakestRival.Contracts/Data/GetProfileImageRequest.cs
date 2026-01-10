using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class GetProfileImageRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; } = string.Empty;

        [DataMember(Order = 2, IsRequired = true)]
        public int AccountId { get; set; }

        [DataMember(Order = 3, IsRequired = true)]
        public string ProfileImageCode { get; set; } = string.Empty;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Token = Token ?? string.Empty;
            ProfileImageCode = ProfileImageCode ?? string.Empty;
        }
    }

    [DataContract]
    public sealed class GetProfileImageResponse
    {
        [DataMember(Order = 1, IsRequired = true)]
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();

        [DataMember(Order = 2, IsRequired = true)]
        public string ContentType { get; set; } = string.Empty;

        [DataMember(Order = 3, IsRequired = false)]
        public DateTime? UpdatedAtUtc { get; set; }

        [DataMember(Order = 4, IsRequired = true)]
        public string ProfileImageCode { get; set; } = string.Empty;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            ImageBytes = ImageBytes ?? Array.Empty<byte>();
            ContentType = ContentType ?? string.Empty;
            ProfileImageCode = ProfileImageCode ?? string.Empty;
        }
    }
}
