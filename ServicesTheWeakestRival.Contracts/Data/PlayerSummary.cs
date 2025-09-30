using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class PlayerSummary
    {
        [DataMember(Order = 1)] public Guid PlayerId { get; set; }
        [DataMember(Order = 2)] public string PlayerName { get; set; }
        [DataMember(Order = 3)] public bool IsOnline { get; set; }
    }
}
