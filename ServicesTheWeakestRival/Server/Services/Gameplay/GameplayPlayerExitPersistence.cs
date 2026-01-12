using log4net;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal static class GameplayPlayerExitPersistence
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayPlayerExitPersistence));

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private const string SQL_MARK_LEFT =
            "UPDATE dbo.MatchPlayers " +
            "SET left_at = @NowUtc, is_eliminated = 1 " +
            "WHERE match_id = @MatchId AND user_id = @UserId AND left_at IS NULL;";

        private const string PARAM_MATCH_ID = "@MatchId";
        private const string PARAM_USER_ID = "@UserId";
        private const string PARAM_NOW_UTC = "@NowUtc";

        internal static void TryMarkPlayerLeft(int matchId, int userId)
        {
            if (matchId <= 0 || userId <= 0)
            {
                return;
            }

            string connectionString = GetConnectionStringOrThrow();

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(SQL_MARK_LEFT, connection))
            {
                command.CommandType = CommandType.Text;

                command.Parameters.Add(PARAM_MATCH_ID, SqlDbType.Int).Value = matchId;
                command.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;
                command.Parameters.Add(PARAM_NOW_UTC, SqlDbType.DateTime2).Value = DateTime.UtcNow;

                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static string GetConnectionStringOrThrow()
        {
            ConnectionStringSettings settings =
                ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new ConfigurationErrorsException(
                    string.Format("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME));
            }

            return settings.ConnectionString;
        }
    }
}
