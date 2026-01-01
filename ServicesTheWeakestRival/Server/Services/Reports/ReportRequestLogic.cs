using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportRequestLogic
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ReportRequestLogic));

        private const string ContextSubmit = "ReportRequestLogic.SubmitPlayerReport";

        private const string FaultInvalidTarget = "REPORT_INVALID_TARGET";
        private const string FaultSelfReport = "REPORT_SELF";
        private const string FaultCooldown = "REPORT_COOLDOWN";
        private const string FaultInvalidReason = "REPORT_INVALID_REASON";
        private const string FaultReporterNotActive = "REPORT_REPORTER_NOT_ACTIVE";
        private const string FaultReportedNotActive = "REPORT_REPORTED_NOT_ACTIVE";
        private const string FaultCommentTooLong = "REPORT_COMMENT_TOO_LONG";
        private const string FaultSanctionPolicyMissing = "REPORT_SANCTION_POLICY_MISSING";
        private const string FaultDb = "REPORT_DB_ERROR";
        private const string FaultUnexpected = "REPORT_UNEXPECTED";

        internal SubmitPlayerReportResponse SubmitPlayerReport(SubmitPlayerReportRequest request)
        {
            ReportServiceContext.ValidateRequest(request);

            int reporterAccountId = ReportServiceContext.Authenticate(request.Token);

            if (request.ReportedAccountId <= 0)
            {
                throw ReportServiceContext.ThrowFault(FaultInvalidTarget, "Jugador inválido.");
            }

            if (reporterAccountId == request.ReportedAccountId)
            {
                throw ReportServiceContext.ThrowFault(FaultSelfReport, "No puedes reportarte a ti mismo.");
            }

            string connectionString =
                ConfigurationManager.ConnectionStrings[ReportSql.MainConnectionStringName].ConnectionString;

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand(ReportSql.SpSubmitPlayerReport, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.Add(ReportSql.ParamReporterAccountId, SqlDbType.Int).Value = reporterAccountId;
                        command.Parameters.Add(ReportSql.ParamReportedAccountId, SqlDbType.Int).Value = request.ReportedAccountId;

                        var lobbyIdParam = command.Parameters.Add(ReportSql.ParamLobbyId, SqlDbType.UniqueIdentifier);
                        lobbyIdParam.Value = request.LobbyId.HasValue
                            ? (object)request.LobbyId.Value
                            : DBNull.Value;

                        command.Parameters.Add(ReportSql.ParamReasonCode, SqlDbType.TinyInt).Value = (byte)request.ReasonCode;

                        var commentParam = command.Parameters.Add(
                            ReportSql.ParamComment,
                            SqlDbType.NVarChar,
                            ReportSql.CommentMaxLength);

                        commentParam.Value = string.IsNullOrWhiteSpace(request.Comment)
                            ? (object)DBNull.Value
                            : request.Comment;

                        var outReportId = command.Parameters.Add(ReportSql.OutReportId, SqlDbType.BigInt);
                        outReportId.Direction = ParameterDirection.Output;

                        var outSanctionApplied = command.Parameters.Add(ReportSql.OutSanctionApplied, SqlDbType.Bit);
                        outSanctionApplied.Direction = ParameterDirection.Output;

                        var outSanctionType = command.Parameters.Add(ReportSql.OutSanctionType, SqlDbType.TinyInt);
                        outSanctionType.Direction = ParameterDirection.Output;

                        var outSanctionEndAtUtc = command.Parameters.Add(ReportSql.OutSanctionEndAtUtc, SqlDbType.DateTime2);
                        outSanctionEndAtUtc.Direction = ParameterDirection.Output;

                        command.ExecuteNonQuery();

                        var response = new SubmitPlayerReportResponse
                        {
                            ReportId = Convert.ToInt64(outReportId.Value),
                            SanctionApplied = Convert.ToBoolean(outSanctionApplied.Value),
                            SanctionType = outSanctionType.Value == DBNull.Value ? (byte)0 : Convert.ToByte(outSanctionType.Value),
                            SanctionEndAtUtc = outSanctionEndAtUtc.Value == DBNull.Value ? (DateTime?)null : (DateTime)outSanctionEndAtUtc.Value
                        };

                        return response;
                    }
                }
            }
            catch (SqlException ex)
            {
                Logger.Warn(ContextSubmit, ex);

                string message = ex.Message ?? string.Empty;

                if (ContainsToken(message, ReportSql.DbTokenDuplicateCooldown))
                {
                    throw ReportServiceContext.ThrowFault(FaultCooldown, "Debes esperar antes de reportar al mismo jugador otra vez.");
                }

                if (ContainsToken(message, ReportSql.DbTokenInvalidReason))
                {
                    throw ReportServiceContext.ThrowFault(FaultInvalidReason, "Motivo inválido.");
                }

                if (ContainsToken(message, ReportSql.DbTokenReporterNotActive))
                {
                    throw ReportServiceContext.ThrowFault(FaultReporterNotActive, "Tu cuenta no está activa.");
                }

                if (ContainsToken(message, ReportSql.DbTokenReportedNotActive))
                {
                    throw ReportServiceContext.ThrowFault(FaultReportedNotActive, "La cuenta del jugador no está activa.");
                }

                if (ContainsToken(message, ReportSql.DbTokenCommentTooLong))
                {
                    throw ReportServiceContext.ThrowFault(FaultCommentTooLong, "El comentario es demasiado largo.");
                }

                if (ContainsToken(message, ReportSql.DbTokenSanctionPolicyMissing))
                {
                    throw ReportServiceContext.ThrowFault(FaultSanctionPolicyMissing, "Configuración de sanciones incompleta.");
                }

                if (ContainsToken(message, ReportSql.DbTokenSelfReport))
                {
                    throw ReportServiceContext.ThrowFault(FaultSelfReport, "No puedes reportarte a ti mismo.");
                }

                throw ReportServiceContext.ThrowFault(FaultDb, "Ocurrió un error al enviar el reporte.");
            }
            catch (Exception ex)
            {
                Logger.Error(ContextSubmit, ex);
                throw ReportServiceContext.ThrowFault(FaultUnexpected, "Ocurrió un error inesperado al enviar el reporte.");
            }
        }

        private static bool ContainsToken(string text, string token)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
