using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data.SqlClient;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal static class GameplayServiceContext
    {
        internal static readonly ILog Logger = LogManager.GetLogger(typeof(Services.GameplayService));

        internal static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        internal const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        internal const string ERROR_INVALID_REQUEST = "Solicitud inválida";
        internal const string ERROR_DB = "Error de base de datos";
        internal const string ERROR_UNEXPECTED = "Error inesperado";
        internal const string ERROR_MATCH_NOT_FOUND = "Partida no encontrada";
        internal const string ERROR_NOT_PLAYER_TURN = "No es tu turno";
        internal const string ERROR_DUEL_NOT_ACTIVE = "Duelo no activo";
        internal const string ERROR_NOT_WEAKEST_RIVAL = "Acción no permitida";
        internal const string ERROR_INVALID_DUEL_TARGET = "Objetivo inválido";
        internal const string ERROR_MATCH_ALREADY_STARTED = "Partida ya iniciada";
        internal const string ERROR_NO_QUESTIONS = "Sin preguntas";

        internal const string FALLBACK_LOCALE_EN_US = "en-US";

        internal const string ERROR_INVALID_REQUEST_MESSAGE = "La solicitud es nula.";
        internal const string ERROR_MATCH_NOT_FOUND_MESSAGE = "No se encontró la partida.";
        internal const string ERROR_NOT_PLAYER_TURN_MESSAGE = "No es el turno del jugador.";
        internal const string ERROR_DUEL_NOT_ACTIVE_MESSAGE = "El duelo no está activo.";
        internal const string ERROR_NOT_WEAKEST_RIVAL_MESSAGE = "Solo el rival más débil puede elegir oponente.";
        internal const string ERROR_INVALID_DUEL_TARGET_MESSAGE = "Oponente de duelo inválido.";
        internal const string ERROR_MATCH_ALREADY_STARTED_MESSAGE = "La partida ya inició. No está permitido unirse.";
        internal const string ERROR_NO_QUESTIONS_MESSAGE = "No se encontraron preguntas para la dificultad/idioma solicitados.";

        internal const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";

        internal const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        internal const string CONTEXT_SUBMIT_ANSWER = "GameplayService.SubmitAnswer";
        internal const string CONTEXT_BANK = "GameplayService.Bank";
        internal const string CONTEXT_CAST_VOTE = "GameplayService.CastVote";
        internal const string CONTEXT_JOIN_MATCH = "GameplayService.JoinMatch";
        internal const string CONTEXT_START_MATCH = "GameplayService.StartMatch";
        internal const string CONTEXT_CHOOSE_DUEL = "GameplayService.ChooseDuelOpponent";
        internal const string CONTEXT_GET_QUESTIONS = "GameplayService.GetQuestions";
        internal const string CONTEXT_GET_CONNECTION_STRING = "GameplayService.GetConnectionString";

        internal const int DEFAULT_MAX_QUESTIONS = 40;

        private const string AUTH_REQUIRED_CODE = "AUTH_REQUIRED";
        private const string AUTH_INVALID_CODE = "AUTH_INVALID";
        private const string AUTH_EXPIRED_CODE = "AUTH_EXPIRED";

        private const string AUTH_REQUIRED_MESSAGE = "Falta el token.";
        private const string AUTH_INVALID_MESSAGE = "Token inválido.";
        private const string AUTH_EXPIRED_MESSAGE = "El token expiró.";

        private const string VALIDATION_MATCH_ID_REQUIRED_MESSAGE = "El MatchId es obligatorio.";

        private const string CONFIG_ERROR_CODE = "CONFIG_ERROR";
        private const string CONFIG_ERROR_USER_MESSAGE = "Error de configuración. Por favor contacta a soporte.";

        private const string LOG_MISSING_CONNECTION_STRING_FORMAT = "Missing connection string '{0}'.";
        private const string LOG_SERVICE_FAULT_FORMAT = "Service fault. Code='{0}', Message='{1}'";

        private static readonly Random randomGenerator = new Random();
        private static readonly object randomSyncRoot = new object();

        internal static void ValidateNotNullRequest(object request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }
        }

        internal static void ValidateMatchId(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, VALIDATION_MATCH_ID_REQUIRED_MESSAGE);
            }
        }

        internal static string GetConnectionString()
        {
            ConnectionStringSettings configurationString =
                ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (configurationString == null || string.IsNullOrWhiteSpace(configurationString.ConnectionString))
            {
                Logger.ErrorFormat(LOG_MISSING_CONNECTION_STRING_FORMAT, MAIN_CONNECTION_STRING_NAME);

                throw ThrowTechnicalFault(
                    CONFIG_ERROR_CODE,
                    CONFIG_ERROR_USER_MESSAGE,
                    CONTEXT_GET_CONNECTION_STRING,
                    new ConfigurationErrorsException(
                        string.Format(LOG_MISSING_CONNECTION_STRING_FORMAT, MAIN_CONNECTION_STRING_NAME)));
            }

            return configurationString.ConnectionString;
        }

        internal static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat(LOG_SERVICE_FAULT_FORMAT, code, message);

            ServiceFault fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        internal static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            ServiceFault fault = new ServiceFault
            {
                Code = code,
                Message = userMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }

        internal static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ThrowFault(AUTH_REQUIRED_CODE, AUTH_REQUIRED_MESSAGE);
            }

            if (!TokenCache.TryGetValue(token, out AuthToken authToken))
            {
                throw ThrowFault(AUTH_INVALID_CODE, AUTH_INVALID_MESSAGE);
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault(AUTH_EXPIRED_CODE, AUTH_EXPIRED_MESSAGE);
            }

            return authToken.UserId;
        }

        internal static int NextRandom(int minInclusive, int maxExclusive)
        {
            lock (randomSyncRoot)
            {
                return randomGenerator.Next(minInclusive, maxExclusive);
            }
        }

        internal static int GetMaxQuestionsOrDefault(int? requested)
        {
            if (requested.HasValue && requested.Value > 0)
            {
                return requested.Value;
            }

            return DEFAULT_MAX_QUESTIONS;
        }

        internal static T ExecuteDbOperation<T>(string context, Func<SqlConnection, T> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    return operation(connection);
                }
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(ERROR_DB, MESSAGE_DB_ERROR, context, ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(ERROR_UNEXPECTED, MESSAGE_UNEXPECTED_ERROR, context, ex);
            }
        }

        internal static void ExecuteDbAction(string context, Action<SqlConnection> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            ExecuteDbOperation<Unit>(
                context,
                connection =>
                {
                    action(connection);
                    return Unit.Value;
                });
        }

        internal readonly struct Unit
        {
            public static readonly Unit Value = new Unit();
        }
    }
}
