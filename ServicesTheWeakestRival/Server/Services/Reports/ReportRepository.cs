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
                ConfigurationManager.ConnectionStrings[ReportConstants.Sql.MainConnectionStringName].ConnectionString;

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(ReportConstants.Sql.SpSubmitPlayerReport, connection))
            {
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(ReportConstants.Sql.ParamReporterAccountId, SqlDbType.Int).Value = reporterAccountId;
                command.Parameters.Add(ReportConstants.Sql.ParamReportedAccountId, SqlDbType.Int).Value = request.ReportedAccountId;

                var lobbyIdParam = command.Parameters.Add(ReportConstants.Sql.ParamLobbyId, SqlDbType.UniqueIdentifier);
                lobbyIdParam.Value = request.LobbyId.HasValue ? (object)request.LobbyId.Value : DBNull.Value;

                command.Parameters.Add(ReportConstants.Sql.ParamReasonCode, SqlDbType.TinyInt).Value = (byte)request.ReasonCode;

                var commentParam = command.Parameters.Add(
                    ReportConstants.Sql.ParamComment,
                    SqlDbType.NVarChar,
                    ReportConstants.Sql.CommentMaxLength);

                commentParam.Value = string.IsNullOrWhiteSpace(request.Comment) ? (object)DBNull.Value : request.Comment;

                var outReportId = command.Parameters.Add(ReportConstants.Sql.OutReportId, SqlDbType.BigInt);
                outReportId.Direction = ParameterDirection.Output;

                var outSanctionApplied = command.Parameters.Add(ReportConstants.Sql.OutSanctionApplied, SqlDbType.Bit);
                outSanctionApplied.Direction = ParameterDirection.Output;

                var outSanctionType = command.Parameters.Add(ReportConstants.Sql.OutSanctionType, SqlDbType.TinyInt);
                outSanctionType.Direction = ParameterDirection.Output;

                var outSanctionEndAtUtc = command.Parameters.Add(ReportConstants.Sql.OutSanctionEndAtUtc, SqlDbType.DateTime2);
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
