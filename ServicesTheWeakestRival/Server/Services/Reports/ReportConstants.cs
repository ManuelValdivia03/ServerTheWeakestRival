namespace ServicesTheWeakestRival.Server.Services.Reports
{
    public static class ReportConstants
    {
        internal static class Context
        {
            public const string SubmitPlayerReport = "ReportCoordinator.SubmitPlayerReport";
            public const string SqlSubmitPlayerReport = "ReportRepository.SubmitPlayerReport";
            public const string TokenAuth = "ReportTokenAuthenticator.AuthenticateOrThrow";
            public const string UnexpectedSubmit = "ReportCoordinator.SubmitPlayerReport.Unexpected";
            public const string TimeoutSubmit = "ReportCoordinator.SubmitPlayerReport.Timeout";
            public const string CommunicationSubmit = "ReportCoordinator.SubmitPlayerReport.Communication";
            public const string ConfigurationSubmit = "ReportCoordinator.SubmitPlayerReport.Configuration";
        }

        public static class OperationKeyPrefix
        {
            public const string SubmitPlayerReport = "Report.SubmitPlayerReport";
        }

        public static class FaultCode
        {
            public const string RequestNull = "REPORT_REQUEST_NULL";
            public const string TokenInvalid = "REPORT_TOKEN_INVALID";
            public const string InvalidTarget = "REPORT_INVALID_TARGET";
            public const string SelfReport = "REPORT_SELF";
            public const string InvalidReason = "REPORT_INVALID_REASON";
            public const string CommentTooLong = "REPORT_COMMENT_TOO_LONG";

            public const string DbError = "REPORT_DB_ERROR";
            public const string Timeout = "REPORT_TIMEOUT";
            public const string Communication = "REPORT_COMMUNICATION_ERROR";
            public const string Configuration = "REPORT_CONFIGURATION_ERROR";
            public const string Unexpected = "REPORT_UNEXPECTED";
        }

        public static class MessageKey
        {
            public const string RequestNull = "Report.SubmitPlayerReport.Validation.RequestNull";
            public const string TokenInvalid = "Report.SubmitPlayerReport.Validation.TokenInvalid";
            public const string InvalidTarget = "Report.SubmitPlayerReport.Validation.InvalidTarget";
            public const string SelfReport = "Report.SubmitPlayerReport.Validation.SelfReport";
            public const string InvalidReason = "Report.SubmitPlayerReport.Validation.InvalidReason";
            public const string CommentTooLong = "Report.SubmitPlayerReport.Validation.CommentTooLong";

            public const string Timeout = "Report.SubmitPlayerReport.Timeout";
            public const string Communication = "Report.SubmitPlayerReport.Communication";
            public const string Configuration = "Report.SubmitPlayerReport.Configuration";
            public const string Unexpected = "Report.SubmitPlayerReport.Unexpected";
        }

        public static class Sql
        {
            public const string MainConnectionStringName = "TheWeakestRivalDb";

            public const string SpSubmitPlayerReport = "dbo.sp_submit_player_report";

            public const string ParamReporterAccountId = "@reporter_account_id";
            public const string ParamReportedAccountId = "@reported_account_id";
            public const string ParamLobbyId = "@lobby_id";
            public const string ParamReasonCode = "@reason_code";
            public const string ParamComment = "@comment";

            public const string OutReportId = "@report_id";
            public const string OutSanctionApplied = "@sanction_applied";
            public const string OutSanctionType = "@sanction_type";
            public const string OutSanctionEndAtUtc = "@sanction_end_at_utc";

            public const int CommentMaxLength = 500;
        }
    }
}
