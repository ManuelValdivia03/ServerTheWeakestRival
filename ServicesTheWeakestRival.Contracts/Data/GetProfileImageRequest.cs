using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class GetProfileImageRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public int UserId { get; set; }
    }

    [DataContract]
    public sealed class GetProfileImageResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public int UserId { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public bool HasImage { get; set; }

        [DataMember(Order = 3, EmitDefaultValue = false)] public byte[] ImageBytes { get; set; }
        [DataMember(Order = 4, EmitDefaultValue = false)] public string ContentType { get; set; }

        [DataMember(Order = 5)] public DateTime? UpdatedAtUtc { get; set; }
    }
}
