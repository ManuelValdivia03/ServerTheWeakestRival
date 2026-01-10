using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Stats
{
    internal static class StatsMatchResultsWriter
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(StatsMatchResultsWriter));

        private const string CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private const byte MATCH_STATE_FINISHED = 3;

        private const decimal LOSER_SCORE = 5.00m;

        private const int WINNER_RANK = 1;
        private const int LOSER_RANK = 2;

        private const int USER_ID_MIN_VALUE = 1;

        private const string CTX_PERSIST_FINAL_RESULTS = "StatsMatchResultsWriter.TryPersistFinalResults";

        private const string SQL_UPDATE_MATCH_FINISH =
            @"UPDATE dbo.Matches
                 SET state = @FinishedState,
                     ended_at = SYSUTCDATETIME()
               WHERE match_id = @MatchId
                 AND ended_at IS NULL;";

        private const string SQL_INSERT_PLAYER_IF_MISSING =
            @"IF NOT EXISTS (
                    SELECT 1
                      FROM dbo.MatchPlayers
                     WHERE match_id = @MatchId
                       AND user_id = @UserId
                )
                BEGIN
                    INSERT INTO dbo.MatchPlayers (match_id, user_id, is_eliminated)
                    VALUES (@MatchId, @UserId, 0);
                END";

        private const string SQL_SET_LOSERS_SCORE =
            @"UPDATE dbo.MatchPlayers
                 SET final_score = @LoserScore,
                     final_rank = @LoserRank
               WHERE match_id = @MatchId
                 AND user_id <> @WinnerUserId;";

        private const string SQL_SET_WINNER_SCORE =
            @"UPDATE dbo.MatchPlayers
                 SET final_score = @WinnerScore,
                     final_rank = @WinnerRank
               WHERE match_id = @MatchId
                 AND user_id = @WinnerUserId;";

        internal static bool TryPersistFinalResults(
            int matchId,
            int winnerUserId,
            decimal winnerScore,
            IReadOnlyList<int> participantUserIds)
        {
            if (matchId <= 0 || winnerUserId < USER_ID_MIN_VALUE)
            {
                return false;
            }

            decimal safeWinnerScore = Math.Round(winnerScore, 2, MidpointRounding.AwayFromZero);

            try
            {
                string connectionString = GetConnectionString();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        ExecuteUpdateMatchFinish(connection, transaction, matchId);

                        EnsureParticipantsInserted(connection, transaction, matchId, participantUserIds, winnerUserId);

                        ExecuteSetLosersScore(connection, transaction, matchId, winnerUserId);
                        ExecuteSetWinnerScore(connection, transaction, matchId, winnerUserId, safeWinnerScore);

                        transaction.Commit();
                    }
                }

                return true;
            }
            catch (SqlException ex)
            {
                Logger.Error(CTX_PERSIST_FINAL_RESULTS, ex);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(CTX_PERSIST_FINAL_RESULTS, ex);
                return false;
            }
        }

        private static void EnsureParticipantsInserted(
            SqlConnection connection,
            SqlTransaction transaction,
            int matchId,
            IReadOnlyList<int> participantUserIds,
            int winnerUserId)
        {
            HashSet<int> ids = new HashSet<int>();

            if (participantUserIds != null)
            {
                for (int i = 0; i < participantUserIds.Count; i++)
                {
                    int userId = participantUserIds[i];
                    if (userId >= USER_ID_MIN_VALUE)
                    {
                        ids.Add(userId);
                    }
                }
            }

            ids.Add(winnerUserId);

            foreach (int userId in ids)
            {
                using (SqlCommand command = new SqlCommand(SQL_INSERT_PLAYER_IF_MISSING, connection, transaction))
                {
                    command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                    command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                    command.ExecuteNonQuery();
                }
            }
        }

        private static void ExecuteUpdateMatchFinish(SqlConnection connection, SqlTransaction transaction, int matchId)
        {
            using (SqlCommand command = new SqlCommand(SQL_UPDATE_MATCH_FINISH, connection, transaction))
            {
                command.Parameters.Add("@FinishedState", SqlDbType.TinyInt).Value = MATCH_STATE_FINISHED;
                command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;

                command.ExecuteNonQuery();
            }
        }

        private static void ExecuteSetLosersScore(
            SqlConnection connection,
            SqlTransaction transaction,
            int matchId,
            int winnerUserId)
        {
            using (SqlCommand command = new SqlCommand(SQL_SET_LOSERS_SCORE, connection, transaction))
            {
                command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                command.Parameters.Add("@WinnerUserId", SqlDbType.Int).Value = winnerUserId;

                SqlParameter loserScoreParam = command.Parameters.Add("@LoserScore", SqlDbType.Decimal);
                loserScoreParam.Precision = 6;
                loserScoreParam.Scale = 2;
                loserScoreParam.Value = LOSER_SCORE;

                command.Parameters.Add("@LoserRank", SqlDbType.Int).Value = LOSER_RANK;

                command.ExecuteNonQuery();
            }
        }

        private static void ExecuteSetWinnerScore(
            SqlConnection connection,
            SqlTransaction transaction,
            int matchId,
            int winnerUserId,
            decimal winnerScore)
        {
            using (SqlCommand command = new SqlCommand(SQL_SET_WINNER_SCORE, connection, transaction))
            {
                command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                command.Parameters.Add("@WinnerUserId", SqlDbType.Int).Value = winnerUserId;

                SqlParameter winnerScoreParam = command.Parameters.Add("@WinnerScore", SqlDbType.Decimal);
                winnerScoreParam.Precision = 6;
                winnerScoreParam.Scale = 2;
                winnerScoreParam.Value = winnerScore;

                command.Parameters.Add("@WinnerRank", SqlDbType.Int).Value = WINNER_RANK;

                command.ExecuteNonQuery();
            }
        }

        private static string GetConnectionString()
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[CONNECTION_STRING_NAME];

            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new InvalidOperationException("Missing DB connection string.");
            }

            return settings.ConnectionString;
        }
    }
}
