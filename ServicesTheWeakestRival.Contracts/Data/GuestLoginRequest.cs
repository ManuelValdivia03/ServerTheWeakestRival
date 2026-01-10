using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class GuestLoginRequest
    {
        [DataMember(Order = 1, IsRequired = false)]
        public string DisplayName { get; set; } = string.Empty;
    }
}
