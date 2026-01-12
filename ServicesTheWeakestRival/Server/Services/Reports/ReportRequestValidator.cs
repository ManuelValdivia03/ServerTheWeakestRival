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
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.RequestNull, ReportConstants.MessageKey.RequestNull);
            }

            if (!Enum.IsDefined(typeof(ReportReasonCode), request.ReasonCode))
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.InvalidReason, ReportConstants.MessageKey.InvalidReason);
            }

            if (!string.IsNullOrWhiteSpace(request.Comment)
                && request.Comment.Length > ReportConstants.Sql.CommentMaxLength)
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.CommentTooLong, ReportConstants.MessageKey.CommentTooLong);
            }
        }

        public void ValidateReporterAndTarget(int reporterAccountId, int reportedAccountId)
        {
            if (reportedAccountId <= 0)
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.InvalidTarget, ReportConstants.MessageKey.InvalidTarget);
            }

            if (reporterAccountId == reportedAccountId)
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.SelfReport, ReportConstants.MessageKey.SelfReport);
            }
        }
    }
}
