using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using System;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportRequestValidator : IReportRequestValidator
    {
        public void ValidateSubmitPlayerReportRequest(SubmitPlayerReportRequest request)
        {
            if (request == null)
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.REQUEST_NULL, ReportConstants.MessageKey.REQUEST_NULL);
            }

            if (!Enum.IsDefined(typeof(ReportReasonCode), request.ReasonCode))
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.INVALID_REASON, ReportConstants.MessageKey.INVALID_REASON);
            }

            if (!string.IsNullOrWhiteSpace(request.Comment)
                && request.Comment.Length > ReportConstants.Sql.COMMENT_MAX_LENGTH)
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.COMMENT_TOO_LONG, ReportConstants.MessageKey.COMMENT_TOO_LONG);
            }
        }

        public void ValidateReporterAndTarget(int reporterAccountId, int reportedAccountId)
        {
            if (reportedAccountId <= 0)
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.INVALID_TARGET, ReportConstants.MessageKey.INVALID_TARGET);
            }

            if (reporterAccountId == reportedAccountId)
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.SELFREPORT, ReportConstants.MessageKey.SELF_REPORT);
            }
        }
    }
}
