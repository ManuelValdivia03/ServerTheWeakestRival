using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class PlayerSummary
    {
        [DataMember(Order = 1)]
        public int UserId { get; set; }

        [DataMember(Order = 2)]
        public string DisplayName { get; set; }

        [DataMember(Order = 3)]
        public bool IsOnline { get; set; }

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public AvatarAppearanceDto Avatar { get; set; }
    }
}
