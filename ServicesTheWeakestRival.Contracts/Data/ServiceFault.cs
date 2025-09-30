using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class ServiceFault
    {
        [DataMember(Order = 1, IsRequired = true)] public string Code { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string Message { get; set; }
        [DataMember(Order = 3, EmitDefaultValue = false)] public string Details { get; set; }
    }
}
