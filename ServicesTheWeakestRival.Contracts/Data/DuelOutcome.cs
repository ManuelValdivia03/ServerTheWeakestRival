using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public enum DuelOutcome
    {
        [EnumMember]
        Tie = 0,

        [EnumMember]
        WeakestRivalWins = 1,

        [EnumMember]
        VoterWins = 2
    }
}
