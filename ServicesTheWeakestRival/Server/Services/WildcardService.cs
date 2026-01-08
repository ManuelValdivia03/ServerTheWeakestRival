using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(
        InstanceContextMode = InstanceContextMode.Single,
        ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class WildcardService : IWildcardService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(WildcardService));
        private static readonly Random RandomGenerator = new Random();

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private const int MIN_VALID_ID = 1;
        private const int ROUND_ID_NOT_FOUND = 0;

        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_INVALID_REQUEST_MESSAGE = "Request is null.";

        private const string ERROR_DB = "DB_ERROR";
        private const string ERROR_UNEXPECTED = "UNEXPECTED_ERROR";

        private const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";

        private const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        private const string ERROR_WILDCARD_NOT_FOUND = "WILDCARD_NOT_FOUND";
        private const string ERROR_WILDCARD_NOT_FOUND_MESSAGE =
            "El comodín no existe o ya fue consumido.";

        private const string ERROR_INVALID_MATCH = "INVALID_MATCH";
        private const string ERROR_INVALID_MATCH_MESSAGE = "MatchId inválido.";

        private const string ERROR_INVALID_PARAMS = "INVALID_PARAMS";
        private const string ERROR_INVALID_PARAMS_MESSAGE = "Parámetros inválidos.";

        private const string AUTH_REQUIRED = "AUTH_REQUIRED";
        private const string AUTH_INVALID = "AUTH_INVALID";
        private const string AUTH_EXPIRED = "AUTH_EXPIRED";

        private const string PARAM_MATCH_ID = "@MatchId";
        private const string PARAM_USER_ID = "@UserId";
        private const string PARAM_PLAYER_WILDCARD_ID = "@PlayerWildcardId";
        private const string PARAM_ROUND_NUMBER = "@RoundNumber";
        private const string PARAM_ROUND_ID = "@RoundId";
        private const string PARAM_WILDCARD_TYPE_ID = "@WildcardTypeId";

        private static string GetConnectionString()
        {
            ConnectionStringSettings configurationString =
                ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (configurationString == null || string.IsNullOrWhiteSpace(configurationString.ConnectionString))
            {
                Logger.ErrorFormat("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME);

                throw ThrowTechnicalFault(
                    "CONFIG_ERROR",
                    "Configuration error. Please contact support.",
                    "WildcardService.GetConnectionString",
                    new ConfigurationErrorsException(
                        string.Format("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME)));
            }

            return configurationString.ConnectionString;
        }

        private static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            string safeCode = code ?? string.Empty;
            string safeMessage = message ?? string.Empty;

            Logger.WarnFormat("Service fault. Code='{0}', Message='{1}'", safeCode, safeMessage);

            ServiceFault fault = new ServiceFault
            {
                Code = safeCode,
                Message = safeMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(safeMessage));
        }

        private static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string userMessage,
            string context,
            Exception ex)
        {
            string safeCode = code ?? string.Empty;
            string safeUserMessage = userMessage ?? string.Empty;
            string safeContext = context ?? string.Empty;

            Logger.Error(safeContext, ex);

            ServiceFault fault = new ServiceFault
            {
                Code = safeCode,
                Message = safeUserMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(safeUserMessage));
        }

        private static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ThrowFault(AUTH_REQUIRED, "Missing token.");
            }

            if (!TokenCache.TryGetValue(token, out AuthToken authToken))
            {
                throw ThrowFault(AUTH_INVALID, "Invalid token.");
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault(AUTH_EXPIRED, "Token expired.");
            }

            return authToken.UserId;
        }

        public ListWildcardTypesResponse ListWildcardTypes(ListWildcardTypesRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            Authenticate(request.Token);

            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                using (SqlCommand command = new SqlCommand(WildcardSql.Text.GET_WILDCARD_TYPES, connection))
                {
                    command.CommandType = CommandType.Text;
                    connection.Open();

                    List<WildcardTypeDto> list = new List<WildcardTypeDto>();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(MapWildcardType(reader));
                        }
                    }

                    Logger.InfoFormat("ListWildcardTypes: Count={0}", list.Count);

                    return new ListWildcardTypesResponse
                    {
                        Types = list.ToArray()
                    };
                }
            }
            catch (SqlException ex)
            {
                LogSqlException("WildcardService.ListWildcardTypes", ex);

                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "WildcardService.ListWildcardTypes",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "WildcardService.ListWildcardTypes",
                    ex);
            }
        }

        public GetPlayerWildcardsResponse GetPlayerWildcards(GetPlayerWildcardsRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId < MIN_VALID_ID)
            {
                throw ThrowFault(ERROR_INVALID_MATCH, ERROR_INVALID_MATCH_MESSAGE);
            }

            int userId = Authenticate(request.Token);

            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();

                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        List<PlayerWildcardDto> list =
                            LoadAvailableWildcards(connection, transaction, request.MatchId, userId);

                        if (list.Count == 0)
                        {
                            GrantRandomWildcardInternal(connection, transaction, request.MatchId, userId);
                            list = LoadAvailableWildcards(connection, transaction, request.MatchId, userId);
                        }

                        transaction.Commit();

                        Logger.InfoFormat(
                            "GetPlayerWildcards: MatchId={0}, UserId={1}, Count={2}",
                            request.MatchId,
                            userId,
                            list.Count);

                        return new GetPlayerWildcardsResponse
                        {
                            Wildcards = list.ToArray()
                        };
                    }
                }
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                LogSqlException("WildcardService.GetPlayerWildcards", ex);

                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "WildcardService.GetPlayerWildcards",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "WildcardService.GetPlayerWildcards",
                    ex);
            }
        }

        public UseWildcardResponse UseWildcard(UseWildcardRequest request)
        {
            ValidateUseWildcardRequest(request);

            int userId = Authenticate(request.Token);

            PlayerWildcardDto consumedWildcard = null;
            int roundId = ROUND_ID_NOT_FOUND;

            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();

                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        roundId = TryGetRoundId(connection, transaction, request.MatchId, request.RoundNumber);

                        if (roundId == ROUND_ID_NOT_FOUND)
                        {
                            Logger.WarnFormat(
                                "UseWildcard: Round not found in DB. MatchId={0}, RoundNumber={1}. Will consume without round_id.",
                                request.MatchId,
                                request.RoundNumber);

                            consumedWildcard = ConsumeAndLoadWildcardForUseWithoutRound(
                                connection,
                                transaction,
                                request.PlayerWildcardId,
                                request.MatchId,
                                userId);
                        }
                        else
                        {
                            consumedWildcard = ConsumeAndLoadWildcardForUse(
                                connection,
                                transaction,
                                request.PlayerWildcardId,
                                request.MatchId,
                                userId,
                                roundId);
                        }

                        transaction.Commit();
                    }
                }

                GameplayEngine.Shared.ApplyWildcardFromDbOrThrow(
                    request.MatchId,
                    userId,
                    consumedWildcard.Code,
                    request.RoundNumber);

                Logger.InfoFormat(
                    "UseWildcard: MatchId={0}, UserId={1}, PlayerWildcardId={2}, RoundNumber={3}, RoundId={4}, Code={5}",
                    request.MatchId,
                    userId,
                    request.PlayerWildcardId,
                    request.RoundNumber,
                    roundId,
                    consumedWildcard.Code);

                return new UseWildcardResponse
                {
                    IsConsumed = true,
                    Wildcard = consumedWildcard
                };
            }
            catch (FaultException<ServiceFault>)
            {
                TryRollbackConsumedWildcard(request.MatchId, userId, request.PlayerWildcardId, roundId);
                throw;
            }
            catch (SqlException ex)
            {
                LogSqlException("WildcardService.UseWildcard", ex);

                TryRollbackConsumedWildcard(request.MatchId, userId, request.PlayerWildcardId, roundId);

                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "WildcardService.UseWildcard",
                    ex);
            }
            catch (Exception ex)
            {
                TryRollbackConsumedWildcard(request.MatchId, userId, request.PlayerWildcardId, roundId);

                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "WildcardService.UseWildcard",
                    ex);
            }
        }

        private static void ValidateUseWildcardRequest(UseWildcardRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId < MIN_VALID_ID ||
                request.PlayerWildcardId < MIN_VALID_ID ||
                request.RoundNumber < MIN_VALID_ID)
            {
                throw ThrowFault(ERROR_INVALID_PARAMS, ERROR_INVALID_PARAMS_MESSAGE);
            }
        }

        private static int TryGetRoundId(
            SqlConnection connection,
            SqlTransaction transaction,
            int matchId,
            int roundNumber)
        {
            using (SqlCommand command = new SqlCommand(
                       WildcardSql.Text.GET_ROUND_ID_BY_MATCH_AND_NUMBER,
                       connection,
                       transaction))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.Add(PARAM_MATCH_ID, SqlDbType.Int).Value = matchId;
                command.Parameters.Add(PARAM_ROUND_NUMBER, SqlDbType.Int).Value = roundNumber;

                object scalar = command.ExecuteScalar();
                if (scalar == null || scalar == DBNull.Value)
                {
                    return ROUND_ID_NOT_FOUND;
                }

                return Convert.ToInt32(scalar);
            }
        }

        private static PlayerWildcardDto ConsumeAndLoadWildcardForUse(
            SqlConnection connection,
            SqlTransaction transaction,
            int playerWildcardId,
            int matchId,
            int userId,
            int roundId)
        {
            using (SqlCommand command = new SqlCommand(
                       WildcardSql.Text.CONSUME_AND_GET_PLAYER_WILDCARD_FOR_USE,
                       connection,
                       transaction))
            {
                command.CommandType = CommandType.Text;

                command.Parameters.Add(PARAM_PLAYER_WILDCARD_ID, SqlDbType.Int).Value = playerWildcardId;
                command.Parameters.Add(PARAM_MATCH_ID, SqlDbType.Int).Value = matchId;
                command.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;
                command.Parameters.Add(PARAM_ROUND_ID, SqlDbType.Int).Value = roundId;

                return ReadConsumedWildcardOrThrow(command);
            }
        }

        private static PlayerWildcardDto ConsumeAndLoadWildcardForUseWithoutRound(
            SqlConnection connection,
            SqlTransaction transaction,
            int playerWildcardId,
            int matchId,
            int userId)
        {
            using (SqlCommand command = new SqlCommand(
                       WildcardSql.Text.CONSUME_AND_GET_PLAYER_WILDCARD_FOR_USE_WITHOUT_ROUND,
                       connection,
                       transaction))
            {
                command.CommandType = CommandType.Text;

                command.Parameters.Add(PARAM_PLAYER_WILDCARD_ID, SqlDbType.Int).Value = playerWildcardId;
                command.Parameters.Add(PARAM_MATCH_ID, SqlDbType.Int).Value = matchId;
                command.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;

                return ReadConsumedWildcardOrThrow(command);
            }
        }

        private static PlayerWildcardDto ReadConsumedWildcardOrThrow(SqlCommand command)
        {
            using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (!reader.Read())
                {
                    throw ThrowFault(ERROR_WILDCARD_NOT_FOUND, ERROR_WILDCARD_NOT_FOUND_MESSAGE);
                }

                return new PlayerWildcardDto
                {
                    PlayerWildcardId = reader.GetInt32(0),
                    MatchId = reader.GetInt32(1),
                    UserId = reader.GetInt32(2),
                    WildcardTypeId = reader.GetInt32(3),
                    Code = reader.GetString(4),
                    Name = reader.GetString(5),
                    Description = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    MaxUsesPerMatch = reader.GetByte(7),
                    GrantedAt = reader.GetDateTime(8),
                    ConsumedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                    ConsumedInRound = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10)
                };
            }
        }

        private static void TryRollbackConsumedWildcard(int matchId, int userId, int playerWildcardId, int roundId)
        {
            try
            {
                string sql = roundId == ROUND_ID_NOT_FOUND
                    ? WildcardSql.Text.UNCONSUME_PLAYER_WILDCARD_WITHOUT_ROUND
                    : WildcardSql.Text.UNCONSUME_PLAYER_WILDCARD;

                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.CommandType = CommandType.Text;

                    command.Parameters.Add(PARAM_PLAYER_WILDCARD_ID, SqlDbType.Int).Value = playerWildcardId;
                    command.Parameters.Add(PARAM_MATCH_ID, SqlDbType.Int).Value = matchId;
                    command.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;

                    if (roundId != ROUND_ID_NOT_FOUND)
                    {
                        command.Parameters.Add(PARAM_ROUND_ID, SqlDbType.Int).Value = roundId;
                    }

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("TryRollbackConsumedWildcard failed.", ex);
            }
        }

        private static List<PlayerWildcardDto> LoadAvailableWildcards(
            SqlConnection connection,
            SqlTransaction transaction,
            int matchId,
            int userId)
        {
            List<PlayerWildcardDto> list = new List<PlayerWildcardDto>();

            using (SqlCommand command = new SqlCommand(
                       WildcardSql.Text.GET_AVAILABLE_PLAYER_WILDCARDS,
                       connection,
                       transaction))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.Add(PARAM_MATCH_ID, SqlDbType.Int).Value = matchId;
                command.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapPlayerWildcard(reader));
                    }
                }
            }

            return list;
        }

        private static void LogSqlException(string context, SqlException ex)
        {
            string safeContext = context ?? string.Empty;

            Logger.ErrorFormat(
                "{0} SqlException. Number={1}, State={2}, Procedure={3}, Line={4}, Message={5}",
                safeContext,
                ex.Number,
                ex.State,
                ex.Procedure ?? string.Empty,
                ex.LineNumber,
                ex.Message ?? string.Empty);

            foreach (SqlError err in ex.Errors)
            {
                Logger.ErrorFormat(
                    "{0} SqlError: Number={1}, State={2}, Class={3}, Procedure={4}, Line={5}, Message={6}",
                    safeContext,
                    err.Number,
                    err.State,
                    err.Class,
                    err.Procedure ?? string.Empty,
                    err.LineNumber,
                    err.Message ?? string.Empty);
            }
        }

        private static WildcardTypeDto MapWildcardType(SqlDataReader reader)
        {
            return new WildcardTypeDto
            {
                WildcardTypeId = reader.GetInt32(0),
                Code = reader.GetString(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                MaxUsesPerMatch = reader.GetByte(4)
            };
        }

        private static PlayerWildcardDto MapPlayerWildcard(SqlDataReader reader)
        {
            return new PlayerWildcardDto
            {
                PlayerWildcardId = reader.GetInt32(0),
                MatchId = reader.GetInt32(1),
                UserId = reader.GetInt32(2),
                WildcardTypeId = reader.GetInt32(3),
                Code = reader.GetString(4),
                Name = reader.GetString(5),
                Description = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                MaxUsesPerMatch = reader.GetByte(7),
                GrantedAt = reader.GetDateTime(8),
                ConsumedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                ConsumedInRound = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10)
            };
        }

        public static void GrantLightningWildcard(int matchId, int userId)
        {
            using (SqlConnection connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    GrantRandomWildcardInternal(connection, transaction, matchId, userId);
                    transaction.Commit();
                }

                Logger.InfoFormat(
                    "GrantLightningWildcard: MatchId={0}, UserId={1} — wildcard granted successfully.",
                    matchId,
                    userId);
            }
        }

        private static void GrantRandomWildcardInternal(
            SqlConnection connection,
            SqlTransaction transaction,
            int matchId,
            int userId)
        {
            List<int> wildcardTypeIds = new List<int>();

            using (SqlCommand cmd = new SqlCommand(WildcardSql.Text.GET_WILDCARD_TYPES, connection, transaction))
            {
                cmd.CommandType = CommandType.Text;

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        wildcardTypeIds.Add(reader.GetInt32(0));
                    }
                }
            }

            if (wildcardTypeIds.Count == 0)
            {
                Logger.Warn("GrantRandomWildcardInternal: no wildcard types found; none granted.");
                return;
            }

            int selectedTypeId;
            lock (RandomGenerator)
            {
                int index = RandomGenerator.Next(wildcardTypeIds.Count);
                selectedTypeId = wildcardTypeIds[index];
            }

            using (SqlCommand cmd = new SqlCommand(WildcardSql.Text.INSERT_PLAYER_WILDCARD, connection, transaction))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(PARAM_MATCH_ID, SqlDbType.Int).Value = matchId;
                cmd.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;
                cmd.Parameters.Add(PARAM_WILDCARD_TYPE_ID, SqlDbType.Int).Value = selectedTypeId;

                cmd.ExecuteNonQuery();
            }

            Logger.InfoFormat(
                "GrantRandomWildcardInternal: MatchId={0}, UserId={1}, WildcardTypeId={2}",
                matchId,
                userId,
                selectedTypeId);
        }

        internal static void GrantRandomWildcardForMatch(int matchId, int userId)
        {
            using (SqlConnection connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    GrantRandomWildcardInternal(connection, transaction, matchId, userId);
                    transaction.Commit();
                }
            }
        }
    }
}
