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

        internal const string ERROR_INVALID_REQUEST = "Error";
        internal const string ERROR_DB = "Error";
        internal const string ERROR_UNEXPECTED = "Error";
        internal const string ERROR_MATCH_NOT_FOUND = "Error";
        internal const string ERROR_NOT_PLAYER_TURN = "Error";
        internal const string ERROR_DUEL_NOT_ACTIVE = "Error";
        internal const string ERROR_NOT_WEAKEST_RIVAL = "Error";
        internal const string ERROR_INVALID_DUEL_TARGET = "Error";
        internal const string ERROR_MATCH_ALREADY_STARTED = "Error";
        internal const string ERROR_NO_QUESTIONS = "Error";

        internal const string FALLBACK_LOCALE_EN_US = "en-US";

        internal const string ERROR_INVALID_REQUEST_MESSAGE = "Request is null.";
        internal const string ERROR_MATCH_NOT_FOUND_MESSAGE = "Match not found.";
        internal const string ERROR_NOT_PLAYER_TURN_MESSAGE = "It is not the player turn.";
        internal const string ERROR_DUEL_NOT_ACTIVE_MESSAGE = "Duel is not active.";
        internal const string ERROR_NOT_WEAKEST_RIVAL_MESSAGE = "Only weakest rival can choose duel opponent.";
        internal const string ERROR_INVALID_DUEL_TARGET_MESSAGE = "Invalid duel opponent.";
        internal const string ERROR_MATCH_ALREADY_STARTED_MESSAGE = "Match already started. Joining is not allowed.";
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

        internal const int DEFAULT_MAX_QUESTIONS = 40;

        private static readonly Random RandomGenerator = new Random();
        private static readonly object RandomSyncRoot = new object();

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
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }
        }

        internal static string GetConnectionString()
        {
            ConnectionStringSettings configurationString =
                ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (configurationString == null || string.IsNullOrWhiteSpace(configurationString.ConnectionString))
            {
                Logger.ErrorFormat("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME);

                throw ThrowTechnicalFault(
                    "CONFIG_ERROR",
                    "Configuration error. Please contact support.",
                    "GameplayService.GetConnectionString",
                    new ConfigurationErrorsException(
                        string.Format("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME)));
            }

            return configurationString.ConnectionString;
        }

        internal static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat("Service fault. Code='{0}', Message='{1}'", code, message);

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
                throw ThrowFault("AUTH_REQUIRED", "Missing token.");
            }

            if (!TokenCache.TryGetValue(token, out AuthToken authToken))
            {
                throw ThrowFault("AUTH_INVALID", "Invalid token.");
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault("AUTH_EXPIRED", "Token expired.");
            }

            return authToken.UserId;
        }

        internal static int NextRandom(int minInclusive, int maxExclusive)
        {
            lock (RandomSyncRoot)
            {
                return RandomGenerator.Next(minInclusive, maxExclusive);
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
