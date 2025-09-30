using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class PingRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Message { get; set; }
    }

    [DataContract]
    public sealed class PingResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public string Echo { get; set; }
        [DataMember(Order = 2)] public DateTime Utc { get; set; }
    }
}