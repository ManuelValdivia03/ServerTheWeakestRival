using log4net;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportUserIdResolver
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ReportUserIdResolver));

        private const string ContextResolve = "ReportUserIdResolver.ResolveUserIdFromAccountId";

        private const string SqlResolveUserIdFromAccountId =
            "SELECT TOP (1) u.user_id FROM dbo.Users u WHERE u.account_id = @AccountId;";

        private const string ParamAccountId = "@AccountId";

        internal int ResolveUserIdFromAccountId(int accountId)
        {
            if (accountId <= 0)
            {
                return 0;
            }

            string connectionString =
                ConfigurationManager.ConnectionStrings[ReportSql.MainConnectionStringName].ConnectionString;

            try
            {
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(SqlResolveUserIdFromAccountId, connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.Add(ParamAccountId, SqlDbType.Int).Value = accountId;

                    connection.Open();

                    object obj = command.ExecuteScalar();
                    return obj == null || obj == DBNull.Value ? 0 : Convert.ToInt32(obj);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ContextResolve, ex);
                return 0;
            }
        }
    }
}
