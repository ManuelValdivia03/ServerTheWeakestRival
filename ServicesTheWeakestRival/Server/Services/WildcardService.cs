using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public sealed class WildcardService : IWildcardService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(WildcardService));

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

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

        private const string PARAM_MATCH_ID = "@MatchId";
        private const string PARAM_USER_ID = "@UserId";
        private const string PARAM_PLAYER_WILDCARD_ID = "@PlayerWildcardId";
        private const string PARAM_ROUND_NUMBER = "@RoundNumber";

        private static string GetConnectionString()
        {
            var configurationString = ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

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
            Logger.WarnFormat("Service fault. Code='{0}', Message='{1}'", code, message);

            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        private static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            var fault = new ServiceFault
            {
                Code = code,
                Message = userMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }

        private static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ThrowFault("AUTH_REQUIRED", "Missing token.");
            }

            if (!TokenCache.TryGetValue(token, out var authToken))
            {
                throw ThrowFault("AUTH_INVALID", "Invalid token.");
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault("AUTH_EXPIRED", "Token expired.");
            }

            return authToken.UserId;
        }

        // ============= OPERACIONES =============

        public ListWildcardTypesResponse ListWildcardTypes(ListWildcardTypesRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            Authenticate(request.Token);

            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                using (var command = new SqlCommand(WildcardSql.Text.GET_WILDCARD_TYPES, connection))
                {
                    command.CommandType = CommandType.Text;
                    connection.Open();

                    var list = new System.Collections.Generic.List<WildcardTypeDto>();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var dto = new WildcardTypeDto
                            {
                                WildcardTypeId = reader.GetInt32(0),
                                Code = reader.GetString(1),
                                Name = reader.GetString(2),
                                Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                MaxUsesPerMatch = reader.GetByte(4)
                            };

                            list.Add(dto);
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
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at ListWildcardTypes.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at ListWildcardTypes.",
                    ex);
            }
        }

        public GetPlayerWildcardsResponse GetPlayerWildcards(GetPlayerWildcardsRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId <= 0)
            {
                throw ThrowFault("INVALID_MATCH", "MatchId inválido.");
            }

            var userId = Authenticate(request.Token);

            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                using (var command = new SqlCommand(WildcardSql.Text.GET_AVAILABLE_PLAYER_WILDCARDS, connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.Add(PARAM_MATCH_ID, SqlDbType.Int).Value = request.MatchId;
                    command.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;

                    connection.Open();

                    var list = new System.Collections.Generic.List<PlayerWildcardDto>();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var dto = new PlayerWildcardDto
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

                            list.Add(dto);
                        }
                    }

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
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at GetPlayerWildcards.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at GetPlayerWildcards.",
                    ex);
            }
        }

        public UseWildcardResponse UseWildcard(UseWildcardRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId <= 0 || request.PlayerWildcardId <= 0 || request.RoundNumber <= 0)
            {
                throw ThrowFault("INVALID_PARAMS", "Parámetros inválidos.");
            }

            var userId = Authenticate(request.Token);

            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        PlayerWildcardDto wildcard;

                        // 1) Verificar que el comodín pertenece al jugador/partida y no está consumido
                        using (var checkCommand = new SqlCommand(
                                   WildcardSql.Text.GET_PLAYER_WILDCARD_FOR_USE,
                                   connection,
                                   transaction))
                        {
                            checkCommand.CommandType = CommandType.Text;
                            checkCommand.Parameters.Add(PARAM_PLAYER_WILDCARD_ID, SqlDbType.Int).Value =
                                request.PlayerWildcardId;
                            checkCommand.Parameters.Add(PARAM_MATCH_ID, SqlDbType.Int).Value = request.MatchId;
                            checkCommand.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;

                            using (var reader = checkCommand.ExecuteReader(CommandBehavior.SingleRow))
                            {
                                if (!reader.Read())
                                {
                                    throw ThrowFault(
                                        ERROR_WILDCARD_NOT_FOUND,
                                        ERROR_WILDCARD_NOT_FOUND_MESSAGE);
                                }

                                wildcard = new PlayerWildcardDto
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
                                    ConsumedAt = null,
                                    ConsumedInRound = null
                                };
                            }
                        }

                        // 2) Marcar como consumido
                        using (var updateCommand =
                               new SqlCommand(WildcardSql.Text.CONSUME_PLAYER_WILDCARD, connection, transaction))
                        {
                            updateCommand.CommandType = CommandType.Text;
                            updateCommand.Parameters.Add(PARAM_PLAYER_WILDCARD_ID, SqlDbType.Int).Value =
                                request.PlayerWildcardId;
                            updateCommand.Parameters.Add(PARAM_ROUND_NUMBER, SqlDbType.Int).Value =
                                request.RoundNumber;

                            var rows = updateCommand.ExecuteNonQuery();
                            if (rows == 0)
                            {
                                throw ThrowFault(
                                    ERROR_WILDCARD_NOT_FOUND,
                                    ERROR_WILDCARD_NOT_FOUND_MESSAGE);
                            }
                        }

                        transaction.Commit();

                        Logger.InfoFormat(
                            "UseWildcard: MatchId={0}, UserId={1}, PlayerWildcardId={2}, Round={3}",
                            request.MatchId,
                            userId,
                            request.PlayerWildcardId,
                            request.RoundNumber);

                        wildcard.ConsumedAt = DateTime.UtcNow;
                        wildcard.ConsumedInRound = request.RoundNumber;

                        return new UseWildcardResponse
                        {
                            IsConsumed = true,
                            Wildcard = wildcard
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
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "Database error at UseWildcard.",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "Unexpected error at UseWildcard.",
                    ex);
            }
        }
    }
}
