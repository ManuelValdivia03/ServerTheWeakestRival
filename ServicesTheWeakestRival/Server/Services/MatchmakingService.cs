using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.ServiceModel;
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class MatchmakingService : IMatchmakingService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MatchmakingService));

        private static readonly ConcurrentDictionary<Guid, IMatchmakingClientCallback> Cbs =
            new ConcurrentDictionary<Guid, IMatchmakingClientCallback>();

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

        private static string GetConnectionString()
        {
            var configurationString = ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (configurationString == null || string.IsNullOrWhiteSpace(configurationString.ConnectionString))
            {
                Logger.ErrorFormat("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME);

                throw ThrowTechnicalFault(
                    "CONFIG_ERROR",
                    "Configuration error. Please contact support.",
                    "MatchmakingService.GetConnectionString",
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

            AuthToken authToken;
            if (!TokenCache.TryGetValue(token, out authToken))
            {
                throw ThrowFault("AUTH_INVALID", "Invalid token.");
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault("AUTH_EXPIRED", "Token expired.");
            }

            return authToken.UserId;
        }

        public CreateMatchResponse CreateMatch(CreateMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }
            var hostUserId = Authenticate(request.Token);

            try
            {
                var manager = new MatchManager(GetConnectionString());
                var response = manager.CreateMatch(hostUserId, request);
                var cb = OperationContext.Current.GetCallbackChannel<IMatchmakingClientCallback>();

                var match = response.Match;
                if (match == null)
                {
                    throw ThrowTechnicalFault(
                        ERROR_UNEXPECTED,
                        MESSAGE_UNEXPECTED_ERROR,
                        "MatchmakingService.CreateMatch.NullMatch",
                        new InvalidOperationException("MatchManager returned null Match."));
                }
                match.MatchId = Guid.NewGuid();

                Cbs[match.MatchId] = cb;

                try
                {
                    cb.OnMatchCreated(match);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error calling OnMatchCreated callback.", ex);
                    
                }

                return response;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "MatchmakingService.CreateMatch",
                    ex);
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "MatchmakingService.CreateMatch",
                    ex);
            }
        }

        public JoinMatchResponse JoinMatch(JoinMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            Authenticate(request.Token);

            var match = new MatchInfo
            {
                MatchId = Guid.NewGuid(),
                MatchCode = request.MatchCode,
                Players = new List<PlayerSummary>(),
                State = "Waiting"
            };

            return new JoinMatchResponse { Match = match };
        }

        public void LeaveMatch(LeaveMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            Authenticate(request.Token);

            IMatchmakingClientCallback removed;
            Cbs.TryRemove(request.MatchId, out removed);

        }

        public StartMatchResponse StartMatch(StartMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            Authenticate(request.Token);

            var match = new MatchInfo
            {
                MatchId = request.MatchId,
                MatchCode = "NA",
                Players = new List<PlayerSummary>(),
                State = "InProgress"
            };

            IMatchmakingClientCallback cb;
            if (Cbs.TryGetValue(request.MatchId, out cb))
            {
                try
                {
                    cb.OnMatchStarted(match);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error calling OnMatchStarted callback.", ex);
                }
            }

            return new StartMatchResponse { Match = match };
        }

        public ListOpenMatchesResponse ListOpenMatches(ListOpenMatchesRequest request)
        {
            return new ListOpenMatchesResponse
            {
                Matches = new List<MatchInfo>()
            };
        }
    }
}
