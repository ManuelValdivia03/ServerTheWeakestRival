using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.Stats
{
    internal sealed class StatsLogic
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(StatsLogic));

        private const string CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private const int DEFAULT_TOP = 10;
        private const int MIN_TOP = 1;
        private const int MAX_TOP = 100;

        private const string CTX_GET_LEADERBOARD = "StatsLogic.GetLeaderboard";
        private const string CTX_GET_PLAYER_STATS = "StatsLogic.GetPlayerStats";

        private const int PLAYER_NAME_MAX_LEN = 80;

        private const string SQL_GET_LEADERBOARD =
            @"SELECT TOP (@Top)
                    U.display_name AS PlayerName,
                    CASE WHEN MP.final_rank = 1 THEN 1 ELSE 0 END AS Wins,
                    MP.final_score AS BestBank
              FROM dbo.MatchPlayers MP
              INNER JOIN dbo.Users U
                      ON U.user_id = MP.user_id
             WHERE MP.final_score IS NOT NULL
             ORDER BY MP.final_score DESC,
                      MP.match_id DESC,
                      U.display_name ASC;";

        private const string SQL_GET_PLAYER_STATS =
            @"SELECT
                    U.display_name AS PlayerName,
                    SUM(CASE WHEN MP.final_rank = 1 THEN 1 ELSE 0 END) AS Wins,
                    MAX(MP.final_score) AS BestBank
              FROM dbo.Users U
              LEFT JOIN dbo.MatchPlayers MP
                     ON MP.user_id = U.user_id
                    AND MP.final_score IS NOT NULL
             WHERE U.display_name = @PlayerName
             GROUP BY U.display_name;";

        internal GetLeaderboardResponse GetLeaderboard(GetLeaderboardRequest request)
        {
            int top = NormalizeTop(request != null ? request.Top : DEFAULT_TOP);

            try
            {
                List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                using (SqlCommand command = new SqlCommand(SQL_GET_LEADERBOARD, connection))
                {
                    command.Parameters.Add("@Top", SqlDbType.Int).Value = top;

                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            entries.Add(new LeaderboardEntry
                            {
                                PlayerId = Guid.Empty,
                                PlayerName = reader["PlayerName"] as string ?? string.Empty,
                                Wins = reader["Wins"] != DBNull.Value ? Convert.ToInt32(reader["Wins"]) : 0,
                                BestBank = reader["BestBank"] != DBNull.Value ? (decimal)reader["BestBank"] : 0m
                            });
                        }
                    }
                }

                return new GetLeaderboardResponse
                {
                    Entries = entries
                };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_DB,
                    GameplayEngineConstants.MESSAGE_DB_ERROR,
                    CTX_GET_LEADERBOARD,
                    ex);
            }
            catch (SqlException ex)
            {
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_DB,
                    GameplayEngineConstants.MESSAGE_DB_ERROR,
                    CTX_GET_LEADERBOARD,
                    ex);
            }
            catch (Exception ex)
            {
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_UNEXPECTED,
                    GameplayEngineConstants.MESSAGE_UNEXPECTED_ERROR,
                    CTX_GET_LEADERBOARD,
                    ex);
            }
        }


        internal GetPlayerStatsResponse GetPlayerStats(GetPlayerStatsRequest request)
        {
            string playerName = request != null ? request.PlayerName : null;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                throw GameplayFaults.ThrowFault(
                    GameplayEngineConstants.ERROR_INVALID_REQUEST,
                    "PlayerName is required.");
            }

            string safeName = playerName.Trim();

            try
            {
                LeaderboardEntry stats = new LeaderboardEntry
                {
                    PlayerId = Guid.Empty,
                    PlayerName = safeName,
                    Wins = 0,
                    BestBank = 0m
                };

                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                using (SqlCommand command = new SqlCommand(SQL_GET_PLAYER_STATS, connection))
                {
                    command.Parameters.Add("@PlayerName", SqlDbType.NVarChar, PLAYER_NAME_MAX_LEN).Value = safeName;

                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stats.PlayerName = reader["PlayerName"] as string ?? safeName;
                            stats.Wins = reader["Wins"] != DBNull.Value ? Convert.ToInt32(reader["Wins"]) : 0;
                            stats.BestBank = reader["BestBank"] != DBNull.Value ? (decimal)reader["BestBank"] : 0m;
                        }
                    }
                }

                return new GetPlayerStatsResponse
                {
                    Stats = stats
                };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_DB,
                    GameplayEngineConstants.MESSAGE_DB_ERROR,
                    CTX_GET_PLAYER_STATS,
                    ex);
            }
            catch (SqlException ex)
            {
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_DB,
                    GameplayEngineConstants.MESSAGE_DB_ERROR,
                    CTX_GET_PLAYER_STATS,
                    ex);
            }
            catch (Exception ex)
            {
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_UNEXPECTED,
                    GameplayEngineConstants.MESSAGE_UNEXPECTED_ERROR,
                    CTX_GET_PLAYER_STATS,
                    ex);
            }
        }

        private static int NormalizeTop(int top)
        {
            if (top <= 0)
            {
                return DEFAULT_TOP;
            }

            if (top < MIN_TOP)
            {
                return MIN_TOP;
            }

            if (top > MAX_TOP)
            {
                return MAX_TOP;
            }

            return top;
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
