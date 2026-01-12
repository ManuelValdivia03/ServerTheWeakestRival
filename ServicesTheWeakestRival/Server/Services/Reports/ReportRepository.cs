using ServicesTheWeakestRival.Contracts.Data;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportRepository : IReportRepository
    {
        public SubmitPlayerReportResponse SubmitPlayerReport(int reporterAccountId, SubmitPlayerReportRequest request)
        {
            string connectionString =
                ConfigurationManager.ConnectionStrings[ReportConstants.Sql.MAIN_CONNECTION_STRING_NAME].ConnectionString;

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(ReportConstants.Sql.SP_SUBMIT_PLAYER_REPORT, connection))
            {
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(ReportConstants.Sql.PARAM_REPORTER_ACCOUNT_ID, SqlDbType.Int).Value = reporterAccountId;
                command.Parameters.Add(ReportConstants.Sql.PARAM_REPORTED_ACCOUNT_ID, SqlDbType.Int).Value = request.ReportedAccountId;

                var lobbyIdParam = command.Parameters.Add(ReportConstants.Sql.PARAM_LOBBY_ID, SqlDbType.UniqueIdentifier);
                lobbyIdParam.Value = request.LobbyId.HasValue ? (object)request.LobbyId.Value : DBNull.Value;

                command.Parameters.Add(ReportConstants.Sql.PARAM_REASON_CODE, SqlDbType.TinyInt).Value = (byte)request.ReasonCode;

                var commentParam = command.Parameters.Add(
                    ReportConstants.Sql.PARAM_COMMENT,
                    SqlDbType.NVarChar,
                    ReportConstants.Sql.COMMENT_MAX_LENGTH);

                commentParam.Value = string.IsNullOrWhiteSpace(request.Comment) ? (object)DBNull.Value : request.Comment;

                var outReportId = command.Parameters.Add(ReportConstants.Sql.OUT_REPORT_ID, SqlDbType.BigInt);
                outReportId.Direction = ParameterDirection.Output;

                var outSanctionApplied = command.Parameters.Add(ReportConstants.Sql.OUT_SANCTION_APPLIED, SqlDbType.Bit);
                outSanctionApplied.Direction = ParameterDirection.Output;

                var outSanctionType = command.Parameters.Add(ReportConstants.Sql.OUT_SANCTION_TYPE, SqlDbType.TinyInt);
                outSanctionType.Direction = ParameterDirection.Output;

                var outSanctionEndAtUtc = command.Parameters.Add(ReportConstants.Sql.OUT_SANCTION_END_AT_UTC, SqlDbType.DateTime2);
                outSanctionEndAtUtc.Direction = ParameterDirection.Output;

                connection.Open();
                command.ExecuteNonQuery();

                return new SubmitPlayerReportResponse
                {
                    ReportId = Convert.ToInt64(outReportId.Value),
                    SanctionApplied = Convert.ToBoolean(outSanctionApplied.Value),
                    SanctionType = outSanctionType.Value == DBNull.Value ? (byte)0 : Convert.ToByte(outSanctionType.Value),
                    SanctionEndAtUtc = outSanctionEndAtUtc.Value == DBNull.Value ? (DateTime?)null : (DateTime)outSanctionEndAtUtc.Value
                };
            }
        }
    }
}
