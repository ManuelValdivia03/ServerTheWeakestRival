using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class UpdateAccountRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; }

        [DataMember(Order = 2, IsRequired = false, EmitDefaultValue = false)]
        public string DisplayName { get; set; }

        [DataMember(Order = 3, IsRequired = false, EmitDefaultValue = false)]
        public byte[] ProfileImageBytes { get; set; }

        [DataMember(Order = 4, IsRequired = false, EmitDefaultValue = false)]
        public string ProfileImageContentType { get; set; }

        [DataMember(Order = 5, IsRequired = false)]
        public bool RemoveProfileImage { get; set; }

        [DataMember(Order = 6, IsRequired = false, EmitDefaultValue = false)]
        public string Email { get; set; }
    }

    [DataContract]
    public sealed class UpdateAccountResponse
    {
        [DataMember(Order = 1, IsRequired = true)]
        public int UserId { get; set; }

        [DataMember(Order = 2, IsRequired = false, EmitDefaultValue = false)]
        public string DisplayName { get; set; }

        [DataMember(Order = 3, IsRequired = false, EmitDefaultValue = false)]
        public byte[] ProfileImageBytes { get; set; }

        [DataMember(Order = 4, IsRequired = false, EmitDefaultValue = false)]
        public string ProfileImageContentType { get; set; }

        [DataMember(Order = 5, IsRequired = true)]
        public DateTime CreatedAtUtc { get; set; }

        [DataMember(Order = 6, IsRequired = true)]
        public string Email { get; set; }

        [DataMember(Order = 7, EmitDefaultValue = false)]
        public AvatarAppearanceDto Avatar { get; set; }
    }
}
