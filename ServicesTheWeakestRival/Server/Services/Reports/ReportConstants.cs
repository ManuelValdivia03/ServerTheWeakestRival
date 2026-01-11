namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal static class ReportConstants
    {
        internal static class Context
        {
            internal const string SubmitPlayerReport = "ReportCoordinator.SubmitPlayerReport";
            internal const string SqlSubmitPlayerReport = "ReportRepository.SubmitPlayerReport";
            internal const string TokenAuth = "ReportTokenAuthenticator.AuthenticateOrThrow";
            internal const string UnexpectedSubmit = "ReportCoordinator.SubmitPlayerReport.Unexpected";
        }

        internal static class OperationKeyPrefix
        {
            internal const string SubmitPlayerReport = "Report.SubmitPlayerReport";
        }

        internal static class FaultCode
        {
            internal const string RequestNull = "REPORT_REQUEST_NULL";
            internal const string TokenInvalid = "REPORT_TOKEN_INVALID";
            internal const string InvalidTarget = "REPORT_INVALID_TARGET";
            internal const string SelfReport = "REPORT_SELF";
            internal const string InvalidReason = "REPORT_INVALID_REASON";
            internal const string CommentTooLong = "REPORT_COMMENT_TOO_LONG";

            internal const string DbError = "REPORT_DB_ERROR";
            internal const string Unexpected = "REPORT_UNEXPECTED";
        }

        internal static class MessageKey
        {
            internal const string RequestNull = "Report.SubmitPlayerReport.Validation.RequestNull";
            internal const string TokenInvalid = "Report.SubmitPlayerReport.Validation.TokenInvalid";
            internal const string InvalidTarget = "Report.SubmitPlayerReport.Validation.InvalidTarget";
            internal const string SelfReport = "Report.SubmitPlayerReport.Validation.SelfReport";
            internal const string InvalidReason = "Report.SubmitPlayerReport.Validation.InvalidReason";
            internal const string CommentTooLong = "Report.SubmitPlayerReport.Validation.CommentTooLong";

            internal const string Unexpected = "Report.SubmitPlayerReport.Unexpected";
        }

        internal static class Sql
        {
            internal const string MainConnectionStringName = "TheWeakestRivalDb";

            internal const string SpSubmitPlayerReport = "dbo.sp_submit_player_report";

            internal const string ParamReporterAccountId = "@reporter_account_id";
            internal const string ParamReportedAccountId = "@reported_account_id";
            internal const string ParamLobbyId = "@lobby_id";
            internal const string ParamReasonCode = "@reason_code";
            internal const string ParamComment = "@comment";

            internal const string OutReportId = "@report_id";
            internal const string OutSanctionApplied = "@sanction_applied";
            internal const string OutSanctionType = "@sanction_type";
            internal const string OutSanctionEndAtUtc = "@sanction_end_at_utc";

            internal const int CommentMaxLength = 500;
        }
    }
}
