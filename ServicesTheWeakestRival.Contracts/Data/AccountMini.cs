using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class AccountMini
    {
        [DataMember(Order = 1, IsRequired = true)]
        public int AccountId { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public string DisplayName { get; set; } = string.Empty;

        [DataMember(Order = 3, IsRequired = true)]
        public string Email { get; set; } = string.Empty;

        [DataMember(Order = 4, IsRequired = true)]
        public bool HasProfileImage { get; set; }

        [DataMember(Order = 5, IsRequired = true)]
        public string ProfileImageCode { get; set; } = string.Empty;

        [DataMember(Order = 6, IsRequired = false)]
        public AvatarAppearanceDto Avatar { get; set; } = new AvatarAppearanceDto();

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            DisplayName = DisplayName ?? string.Empty;
            Email = Email ?? string.Empty;
            ProfileImageCode = ProfileImageCode ?? string.Empty;
            Avatar = Avatar ?? new AvatarAppearanceDto();
        }
    }

    [DataContract]
    public sealed class GetAccountsByIdsRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; } = string.Empty;

        [DataMember(Order = 2, IsRequired = true)]
        public int[] AccountIds { get; set; } = Array.Empty<int>();

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Token = Token ?? string.Empty;
            AccountIds = AccountIds ?? Array.Empty<int>();
        }
    }

    [DataContract]
    public sealed class GetAccountsByIdsResponse
    {
        [DataMember(Order = 1, IsRequired = true)]
        public AccountMini[] Accounts { get; set; } = Array.Empty<AccountMini>();

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Accounts = Accounts ?? Array.Empty<AccountMini>();
        }
    }
}
