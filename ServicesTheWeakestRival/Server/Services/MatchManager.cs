using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class MatchManager
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchManager));

        private readonly string connectionString;

        private const byte MATCH_STATE_WAITING = 0;

        private static readonly Random RandomGenerator = new Random();
        private static readonly object RandomSyncRoot = new object();

        private const int ACCESS_CODE_MAX_LEN = 12;
        private const int MATCH_CODE_LEN = 6;
        private static readonly int INITIAL_WILDCARDS_PER_PLAYER = 1;


        public MatchManager(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            }

            this.connectionString = connectionString.Trim();
        }

        public CreateMatchResponse CreateMatch(int hostUserId, CreateMatchRequest request)
        {
            if (hostUserId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hostUserId));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.MaxPlayers <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    request.MaxPlayers,
                    "MaxPlayers must be greater than zero.");
            }

            MatchConfigDto cfg = request.Config ?? new MatchConfigDto();

            bool isPrivate = request.IsPrivate;
            string accessCode = isPrivate ? GenerateAccessCode() : null;

            int matchId;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var cmd = new SqlCommand(MatchSql.Text.INSERT_MATCH, connection, transaction))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add("@HostUserId", SqlDbType.Int).Value = hostUserId;
                        cmd.Parameters.Add("@State", SqlDbType.TinyInt).Value = MATCH_STATE_WAITING;
                        cmd.Parameters.Add("@IsPrivate", SqlDbType.Bit).Value = isPrivate;

                        var pAccessCode = cmd.Parameters.Add("@AccessCode", SqlDbType.NVarChar, ACCESS_CODE_MAX_LEN);
                        pAccessCode.Value = accessCode == null ? (object)DBNull.Value : accessCode;

                        cmd.Parameters.Add("@MaxPlayers", SqlDbType.TinyInt).Value = request.MaxPlayers;

                        matchId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    using (var cmd = new SqlCommand(MatchSql.Text.INSERT_MATCH_RULES, connection, transaction))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;

                        AddDecimal(cmd, "@StartingScore", 5, 2, cfg.StartingScore);
                        AddDecimal(cmd, "@MaxScore", 5, 2, cfg.MaxScore);
                        AddDecimal(cmd, "@PointsPerCorrect", 5, 2, cfg.PointsPerCorrect);
                        AddDecimal(cmd, "@PointsPerWrong", 5, 2, cfg.PointsPerWrong);
                        AddDecimal(cmd, "@PointsPerEliminationGain", 5, 2, cfg.PointsPerEliminationGain);

                        cmd.Parameters.Add("@AllowTiebreakCoinflip", SqlDbType.Bit).Value =
                            cfg.AllowTiebreakCoinflip;

                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = new SqlCommand(MatchSql.Text.INSERT_MATCH_PLAYER, connection, transaction))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = hostUserId;
                        cmd.ExecuteNonQuery();
                    }

                    GrantInitialWildcardsIfNeeded(connection, transaction, matchId, hostUserId);

                    transaction.Commit();

                    Logger.InfoFormat(
                        "CreateMatch: MatchId={0}, HostUserId={1}, MaxPlayers={2}, IsPrivate={3}, AccessCode={4}",
                        matchId,
                        hostUserId,
                        request.MaxPlayers,
                        isPrivate,
                        accessCode ?? "(none)");
                }
            }

            var matchInfo = new MatchInfo
            {
                MatchId = Guid.NewGuid(),
                MatchDbId = matchId,
                MatchCode = accessCode ?? string.Empty,
                State = "Waiting",
                Config = cfg,
                Players = new System.Collections.Generic.List<PlayerSummary>()
            };

            return new CreateMatchResponse
            {
                Match = matchInfo
            };
        }

        public MatchInfo JoinMatchByCode(int userId, string matchCode)
        {
            if (userId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(matchCode))
            {
                throw new ArgumentException("MatchCode is required.", nameof(matchCode));
            }

            string trimmedCode = matchCode.Trim();

            int matchId;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    matchId = TryGetMatchIdByAccessCode(connection, transaction, trimmedCode);
                    if (matchId <= 0)
                    {
                        throw new InvalidOperationException("Match not found for provided code.");
                    }

                    int maxPlayers = TryGetMatchMaxPlayers(connection, transaction, matchId);
                    int currentPlayers = CountMatchPlayers(connection, transaction, matchId);

                    if (maxPlayers > 0 && currentPlayers >= maxPlayers)
                    {
                        throw new InvalidOperationException("Match is full.");
                    }

                    using (var cmd = new SqlCommand(MatchSql.Text.INSERT_MATCH_PLAYER_IF_NOT_EXISTS, connection, transaction))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                        cmd.ExecuteNonQuery();
                    }

                    GrantInitialWildcardsIfNeeded(connection, transaction, matchId, userId);

                    transaction.Commit();
                }
            }

            return new MatchInfo
            {
                MatchId = Guid.NewGuid(),
                MatchDbId = matchId,
                MatchCode = trimmedCode,
                State = "Waiting",
                Players = new System.Collections.Generic.List<PlayerSummary>()
            };
        }

        internal void EnsurePlayersAndInitialWildcards(int matchId, System.Collections.Generic.IEnumerable<int> userIds)
        {
            if (matchId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(matchId));
            }

            if (userIds == null)
            {
                throw new ArgumentNullException(nameof(userIds));
            }

            int[] distinctUserIds = userIds
                .Where(u => u > 0)
                .Distinct()
                .ToArray();

            if (distinctUserIds.Length == 0)
            {
                return;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    foreach (int userId in distinctUserIds)
                    {
                        using (var cmd = new SqlCommand(MatchSql.Text.INSERT_MATCH_PLAYER_IF_NOT_EXISTS, connection, transaction))
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                            cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                            cmd.ExecuteNonQuery();
                        }

                        GrantInitialWildcardsIfNeeded(connection, transaction, matchId, userId);
                    }

                    transaction.Commit();
                }
            }

            Logger.InfoFormat(
                "EnsurePlayersAndInitialWildcards: MatchId={0}, PlayersEnsured={1}",
                matchId,
                distinctUserIds.Length);
        }

        private static int TryGetMatchIdByAccessCode(SqlConnection connection, SqlTransaction transaction, string accessCode)
        {
            using (var cmd = new SqlCommand(MatchSql.Text.GET_MATCH_ID_BY_ACCESS_CODE, connection, transaction))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@AccessCode", SqlDbType.NVarChar, ACCESS_CODE_MAX_LEN).Value = accessCode;

                object scalar = cmd.ExecuteScalar();
                if (scalar == null || scalar == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(scalar);
            }
        }

        private static int TryGetMatchMaxPlayers(SqlConnection connection, SqlTransaction transaction, int matchId)
        {
            using (var cmd = new SqlCommand(MatchSql.Text.GET_MATCH_MAX_PLAYERS, connection, transaction))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;

                object scalar = cmd.ExecuteScalar();
                if (scalar == null || scalar == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(scalar);
            }
        }

        private static int CountMatchPlayers(SqlConnection connection, SqlTransaction transaction, int matchId)
        {
            using (var cmd = new SqlCommand(MatchSql.Text.COUNT_MATCH_PLAYERS, connection, transaction))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;

                object scalar = cmd.ExecuteScalar();
                if (scalar == null || scalar == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(scalar);
            }
        }

        internal static void GrantInitialWildcardsIfNeeded(
            SqlConnection connection,
            SqlTransaction transaction,
            int matchId,
            int userId)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            if (matchId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(matchId));
            }

            if (userId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(userId));
            }

            if (INITIAL_WILDCARDS_PER_PLAYER <= 0)
            {
                return;
            }

            if (HasAnyPlayerWildcards(connection, transaction, matchId, userId))
            {
                return;
            }

            var wildcardTypes = new System.Collections.Generic.List<int>();

            using (var cmd = new SqlCommand(WildcardSql.Text.GET_WILDCARD_TYPES, connection, transaction))
            {
                cmd.CommandType = CommandType.Text;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        wildcardTypes.Add(reader.GetInt32(0));
                    }
                }
            }

            if (wildcardTypes.Count == 0)
            {
                Logger.Warn("GrantInitialWildcardsIfNeeded: no wildcard types found. No wildcards granted.");
                return;
            }

            for (int i = 0; i < INITIAL_WILDCARDS_PER_PLAYER; i++)
            {
                int chosenTypeId;

                lock (RandomSyncRoot)
                {
                    int index = RandomGenerator.Next(0, wildcardTypes.Count);
                    chosenTypeId = wildcardTypes[index];
                }

                using (var cmd = new SqlCommand(WildcardSql.Text.INSERT_PLAYER_WILDCARD, connection, transaction))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    cmd.Parameters.Add("@WildcardTypeId", SqlDbType.Int).Value = chosenTypeId;

                    cmd.ExecuteNonQuery();
                }

                Logger.InfoFormat(
                    "GrantInitialWildcardsIfNeeded: MatchId={0}, UserId={1}, WildcardTypeId={2}",
                    matchId,
                    userId,
                    chosenTypeId);
            }
        }

        private static bool HasAnyPlayerWildcards(SqlConnection connection, SqlTransaction transaction, int matchId, int userId)
        {
            using (var cmd = new SqlCommand(WildcardSql.Text.HAS_ANY_PLAYER_WILDCARDS, connection, transaction))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                object scalar = cmd.ExecuteScalar();
                return scalar != null && scalar != DBNull.Value;
            }
        }

        private static void AddDecimal(SqlCommand cmd, string name, byte precision, byte scale, decimal value)
        {
            var p = cmd.Parameters.Add(name, SqlDbType.Decimal);
            p.Precision = precision;
            p.Scale = scale;
            p.Value = value;
        }

        private static string GenerateAccessCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var buffer = new char[MATCH_CODE_LEN];
            var random = new Random();

            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = chars[random.Next(chars.Length)];
            }

            return new string(buffer);
        }
    }
}
