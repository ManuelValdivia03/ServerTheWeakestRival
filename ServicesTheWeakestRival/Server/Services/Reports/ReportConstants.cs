namespace ServicesTheWeakestRival.Server.Services.Reports
{
    public static class ReportConstants
    {
        internal static class Context
        {
            public const string SUBMIT_PLAYER_REPORT = "ReportCoordinator.SubmitPlayerReport";
            public const string SQL_SUBMIT_PLAYER_REPORT = "ReportRepository.SubmitPlayerReport";
            public const string TOKEN_AUTH = "ReportTokenAuthenticator.AuthenticateOrThrow";
            public const string UNEXPECTED_SUBMIT = "ReportCoordinator.SubmitPlayerReport.Unexpected";
            public const string TIMEOUT_SUBMIT = "ReportCoordinator.SubmitPlayerReport.Timeout";
            public const string COMMUNICATION_SUBMIT = "ReportCoordinator.SubmitPlayerReport.Communication";
            public const string CONFIGURATION_SUBMIT = "ReportCoordinator.SubmitPlayerReport.Configuration";
        }

        public static class OperationKeyPrefix
        {
            public const string SUBMIT_PLAYER_REPORT = "Report.SubmitPlayerReport";
        }

        public static class FaultCode
        {
            public const string REQUEST_NULL = "Solicitud nula";
            public const string TOKEN_INVALID = "Token inválido";
            public const string INVALID_TARGET = "Objetivo inválido";
            public const string SELFREPORT = "Auto-reporte no permitido";
            public const string INVALID_REASON = "Motivo inválido";
            public const string COMMENT_TOO_LONG = "Comentario demasiado largo";

            public const string DB_ERROR = "Error de base de datos";
            public const string TIMEOUT = "Tiempo de espera agotado";
            public const string COMMUNICATION = "Error de comunicación";
            public const string CONFIGURATION = "Error de configuración";
            public const string UNEXPECTED = "Error inesperado";
        }

        public static class MessageKey
        {
            public const string REQUEST_NULL = "Report.SubmitPlayerReport.Validation.RequestNull";
            public const string TOKEN_INVALID = "Report.SubmitPlayerReport.Validation.TokenInvalid";
            public const string INVALID_TARGET = "Report.SubmitPlayerReport.Validation.InvalidTarget";
            public const string SELF_REPORT = "Report.SubmitPlayerReport.Validation.SelfReport";
            public const string INVALID_REASON = "Report.SubmitPlayerReport.Validation.InvalidReason";
            public const string COMMENT_TOO_LONG = "Report.SubmitPlayerReport.Validation.CommentTooLong";

            public const string TIMEOUT = "Report.SubmitPlayerReport.Timeout";
            public const string COMMUNICATION = "Report.SubmitPlayerReport.Communication";
            public const string CONFIGURATION = "Report.SubmitPlayerReport.Configuration";
            public const string UNEXPECTED = "Report.SubmitPlayerReport.Unexpected";
        }

        public static class Sql
        {
            public const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

            public const string SP_SUBMIT_PLAYER_REPORT = "dbo.sp_submit_player_report";

            public const string PARAM_REPORTER_ACCOUNT_ID = "@reporter_account_id";
            public const string PARAM_REPORTED_ACCOUNT_ID = "@reported_account_id";
            public const string PARAM_LOBBY_ID = "@lobby_id";
            public const string PARAM_REASON_CODE = "@reason_code";
            public const string PARAM_COMMENT = "@comment";

            public const string OUT_REPORT_ID = "@report_id";
            public const string OUT_SANCTION_APPLIED = "@sanction_applied";
            public const string OUT_SANCTION_TYPE = "@sanction_type";
            public const string OUT_SANCTION_END_AT_UTC = "@sanction_end_at_utc";

            public const int COMMENT_MAX_LENGTH = 500;
        }
    }
}
