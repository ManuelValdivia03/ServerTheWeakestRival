using System;
using System.Data;
using System.Data.SqlClient;
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;

namespace ServicesTheWeakestRival.Server.Services
{
    /// <summary>
    /// Encapsula la lógica de creación de partidas en base de datos.
    /// No es WCF, no maneja tokens ni Faults.
    /// </summary>
    internal sealed class MatchManager
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchManager));

        private readonly string _connectionString;

        private const byte MATCH_STATE_WAITING = 0;

        public MatchManager(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            }

            _connectionString = connectionString.Trim();
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
                throw new ArgumentOutOfRangeException(nameof(request.MaxPlayers));
            }

            var cfg = request.Config ?? new MatchConfigDto();

            // Usamos el flag que viene del cliente
            var isPrivate = request.IsPrivate;
            var accessCode = isPrivate ? GenerateAccessCode() : null;

            int matchId;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    // 1) Matches
                    using (var cmd = new SqlCommand(MatchSql.Text.INSERT_MATCH, connection, transaction))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add("@HostUserId", SqlDbType.Int).Value = hostUserId;
                        cmd.Parameters.Add("@State", SqlDbType.TinyInt).Value = MATCH_STATE_WAITING;
                        cmd.Parameters.Add("@IsPrivate", SqlDbType.Bit).Value = isPrivate;

                        // si no es privada, guardamos NULL en access_code
                        var pAccessCode = cmd.Parameters.Add("@AccessCode", SqlDbType.NVarChar, 12);
                        pAccessCode.Value = accessCode == null ? (object)DBNull.Value : (object)accessCode;

                        cmd.Parameters.Add("@MaxPlayers", SqlDbType.TinyInt).Value = request.MaxPlayers;

                        matchId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 2) MatchRules
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

                    // 3) MatchPlayers (host)
                    using (var cmd = new SqlCommand(MatchSql.Text.INSERT_MATCH_PLAYER, connection, transaction))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = hostUserId;
                        cmd.ExecuteNonQuery();
                    }

                    // 4) Comodines iniciales para el host
                    GrantInitialWildcards(connection, transaction, matchId, hostUserId);

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
                MatchId = Guid.Empty, // pendiente mapear con la BD si quieres Guid real
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

        /// <summary>
        /// Asigna comodines iniciales al jugador host de la partida.
        /// Por ahora: 1 comodín de cada tipo definido en dbo.WildcardTypes.
        /// </summary>
        private static void GrantInitialWildcards(
            SqlConnection connection,
            SqlTransaction transaction,
            int matchId,
            int userId)
        {
            // 1) Leer tipos de comodín disponibles
            var wildcardTypeIds = new System.Collections.Generic.List<int>();

            using (var cmd = new SqlCommand(WildcardSql.Text.GET_WILDCARD_TYPES, connection, transaction))
            {
                cmd.CommandType = CommandType.Text;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // primera columna: wildcard_type_id
                        wildcardTypeIds.Add(reader.GetInt32(0));
                    }
                }
            }

            // 2) Insertar PlayerWildcards (uno por tipo)
            foreach (var wildcardTypeId in wildcardTypeIds)
            {
                using (var cmd = new SqlCommand(WildcardSql.Text.INSERT_PLAYER_WILDCARD, connection, transaction))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    cmd.Parameters.Add("@WildcardTypeId", SqlDbType.Int).Value = wildcardTypeId;

                    cmd.ExecuteNonQuery();
                }
            }

            // Si no hay tipos definidos, simplemente no se insertan comodines (y no pasa nada).
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
            var buffer = new char[6];
            var random = new Random();

            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = chars[random.Next(chars.Length)];
            }

            return new string(buffer);
        }
    }
}
