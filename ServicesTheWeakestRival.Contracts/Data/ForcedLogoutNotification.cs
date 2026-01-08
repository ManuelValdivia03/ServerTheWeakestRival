using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class ForcedLogoutNotification
    {
        [DataMember(Order = 1, IsRequired = true)]
        public byte SanctionType { get; set; }

        [DataMember(Order = 2, IsRequired = false)]
        public DateTime? SanctionEndAtUtc { get; set; }

        [DataMember(Order = 3, IsRequired = true)]
        public string Code { get; set; }
    }
}
