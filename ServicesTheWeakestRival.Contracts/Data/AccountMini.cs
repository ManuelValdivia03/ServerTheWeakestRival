using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public class AccountMini
    {
        [DataMember(Order = 1)] public int AccountId { get; set; }
        [DataMember(Order = 2)] public string DisplayName { get; set; } = string.Empty;
        [DataMember(Order = 3)] public string Email { get; set; } = string.Empty;
        [DataMember(Order = 4)] public string AvatarUrl { get; set; } = null;
        [DataMember(Order = 5)] public AvatarAppearanceDto Avatar { get; set; } = null;
    }

    [DataContract]
    public class GetAccountsByIdsRequest
    {
        [DataMember(Order = 1)] public string Token { get; set; } = string.Empty;
        [DataMember(Order = 2)] public int[] AccountIds { get; set; } = new int[0];
    }

    [DataContract]
    public class GetAccountsByIdsResponse
    {
        [DataMember(Order = 1)] public AccountMini[] Accounts { get; set; } = new AccountMini[0];
    }
}
