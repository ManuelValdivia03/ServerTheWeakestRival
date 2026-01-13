using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    internal static class ReportSql
    {
        internal const string MAIN_CONNECTION_STRING_NAME  = "TheWeakestRivalDb";

        internal const string SP_SUBMIT_PLAYER_REPORT = "dbo.sp_submit_player_report";

        internal const string PARAM_REPORTER_ACCOUNT_ID = "@reporter_account_id";
        internal const string PARAM_REPORTED_ACCOUNT_ID = "@reported_account_id";
        internal const string PARAM_LOBBY_ID = "@lobby_id";
        internal const string PARAM_REASON_CODE = "@reason_code";
        internal const string PARAM_COMMENT = "@comment";

        internal const string OUT_REPORT_ID = "@report_id";
        internal const string OUT_SANCTION_APPLIED = "@sanction_applied";
        internal const string OUT_SANCTION_TYPE = "@sanction_type";
        internal const string OUT_SANCTION_END_AT_UTC = "@sanction_end_at_utc";

        internal const int COMMENT_MAX_LENGTH = 500;

        internal const string DB_TOKEN_DUPLICATE_COOLDOWN = "Reporte duplicado en cooldown";
        internal const string DB_TOKEN_INVALID_REASON = "Motivo inválido";
        internal const string DB_TOKEN_REPORTER_NOT_ACTIVE = "Reportante no activo";
        internal const string DB_TOKEN_REPORTED_NOT_ACTIVE = "Reportado no activo";
        internal const string DB_TOKEN_COMMENT_TOO_LONG = "Comentario demasiado largo";
        internal const string DB_TOKEN_SANCTION_POLICY_MISSING = "Política de sanción no encontrada";
        internal const string DB_TOKEN_SELF_REPORT = "Autoreporte no permitido";
        internal const string DB_TOKEN_INVALID_ACCOUNTS = "Cuentas inválidas";
    }
}

