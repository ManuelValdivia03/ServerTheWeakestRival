using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class UpdateAccountRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        // null/empty => no cambiar
        [DataMember(Order = 2, IsRequired = false)] public string DisplayName { get; set; }
        [DataMember(Order = 3, IsRequired = false)] public string ProfileImageUrl { get; set; }
        [DataMember(Order = 4, IsRequired = false)] public string Email { get; set; }
    }

    [DataContract]
    public sealed class UpdateAccountResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public int UserId { get; set; }
        [DataMember(Order = 2, IsRequired = false)] public string DisplayName { get; set; }
        [DataMember(Order = 3, IsRequired = false)] public string ProfileImageUrl { get; set; }
        [DataMember(Order = 4, IsRequired = true)] public DateTime CreatedAtUtc { get; set; }
        [DataMember(Order = 5, IsRequired = true)] public string Email { get; set; }
        [DataMember(Order = 6)] public AvatarAppearanceDto Avatar { get; set; }
    }
}
