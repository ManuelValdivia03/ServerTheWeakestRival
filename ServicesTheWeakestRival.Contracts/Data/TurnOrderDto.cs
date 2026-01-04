using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class TurnOrderDto
    {
        [DataMember(Order = 1)]
        public int[] OrderedAliveUserIds { get; set; }

        [DataMember(Order = 2)]
        public int CurrentTurnUserId { get; set; }

        [DataMember(Order = 3)]
        public long ServerUtcTicks { get; set; }
    }
}
