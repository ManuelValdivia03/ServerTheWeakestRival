using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    internal static class ReportSql
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

        internal const string DbTokenDuplicateCooldown = "DUPLICATE_COOLDOWN";
        internal const string DbTokenInvalidReason = "INVALID_REASON";
        internal const string DbTokenReporterNotActive = "REPORTER_NOT_ACTIVE";
        internal const string DbTokenReportedNotActive = "REPORTED_NOT_ACTIVE";
        internal const string DbTokenCommentTooLong = "COMMENT_TOO_LONG";
        internal const string DbTokenSanctionPolicyMissing = "SANCTION_POLICY_MISSING";
        internal const string DbTokenSelfReport = "SELF_REPORT";
        internal const string DbTokenInvalidAccounts = "INVALID_ACCOUNTS";
    }
}

