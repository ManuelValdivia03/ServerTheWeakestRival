using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class SendLobbyInviteEmailRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public int TargetAccountId { get; set; }

        [DataMember(Order = 3, IsRequired = true)]
        public string LobbyCode { get; set; }
    }

    [DataContract]
    public sealed class SendLobbyInviteEmailResponse
    {
        [DataMember(Order = 1)]
        public bool Sent { get; set; }
    }
}
