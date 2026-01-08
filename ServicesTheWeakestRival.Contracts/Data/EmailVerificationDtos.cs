using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class BeginRegisterRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Email { get; set; }
    }

    [DataContract]
    public sealed class BeginRegisterResponse
    {
        [DataMember(Order = 1, IsRequired = true)]
        public DateTime ExpiresAtUtc { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public int ResendAfterSeconds { get; set; }
    }

    [DataContract]
    public sealed class CompleteRegisterRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Email { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string Code { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public string Password { get; set; }
        [DataMember(Order = 4, IsRequired = true)] public string DisplayName { get; set; }
        [DataMember(Order = 5, IsRequired = false)] public byte[] ProfileImageBytes { get; set; }
        [DataMember(Order = 6, IsRequired = false)] public string ProfileImageContentType { get; set; }
    }
}
