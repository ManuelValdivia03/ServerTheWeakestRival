using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Enums
{
    [DataContract]
    public enum SpecialEventType : byte
    {
        [EnumMember]
        None = 0,

        [EnumMember]
        LightningChallenge = 1

    }
}
