using ServicesTheWeakestRival.Contracts.Enums;
using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class SubmitPlayerReportRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public int ReportedAccountId { get; set; }
        [DataMember(Order = 3, IsRequired = false)] public Guid? LobbyId { get; set; }
        [DataMember(Order = 4, IsRequired = true)] public ReportReasonCode ReasonCode { get; set; }
        [DataMember(Order = 5, IsRequired = false)] public string Comment { get; set; }
    }

    [DataContract]
    public sealed class SubmitPlayerReportResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public long ReportId { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public bool SanctionApplied { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public byte SanctionType { get; set; }
        [DataMember(Order = 4, IsRequired = false)] public DateTime? SanctionEndAtUtc { get; set; }
    }
}
