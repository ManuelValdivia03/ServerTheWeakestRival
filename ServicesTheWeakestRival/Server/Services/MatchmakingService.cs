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

        private const string ERROR_CONFIG = "Error de configuración";
        private const string ERROR_SOLICITUD_INVALIDA = "Error de solicitud inválida";
        private const string ERROR_AUTENTICACION_REQUERIDA = "Error de autenticación requerida";
        private const string ERROR_AUTENTICACION_INVALIDA = "Error de autenticación inválida";
        private const string ERROR_AUTENTICACION_EXPIRADA = "Error de autenticación expirada";
        private const string ERROR_BASE_DATOS = "Error de base de datos";
        private const string ERROR_INESPERADO = "Error inesperado";

        private const string MESSAGE_CONFIG_ERROR =
            "Error de configuración. Por favor contacta a soporte.";

        private const string MESSAGE_INVALID_REQUEST =
            "La solicitud es inválida.";

        private const string MESSAGE_MISSING_TOKEN =
            "Token requerido.";

        private const string MESSAGE_INVALID_TOKEN =
            "Token inválido.";

        private const string MESSAGE_EXPIRED_TOKEN =
            "Tu sesión expiró. Inicia sesión de nuevo.";

        private const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";

        private const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        private const string CONTEXT_GET_CONNECTION_STRING = "MatchmakingService.GetConnectionString";
        private const string CONTEXT_CREATE_MATCH = "MatchmakingService.CreateMatch";
        private const string CONTEXT_CREATE_MATCH_NULL_MATCH = "MatchmakingService.CreateMatch.NullMatch";
        private const string CONTEXT_JOIN_MATCH = "MatchmakingService.JoinMatch";
        private const string CONTEXT_LEAVE_MATCH = "MatchmakingService.LeaveMatch";
        private const string CONTEXT_START_MATCH = "MatchmakingService.StartMatch";

        private static string GetConnectionString()
        {
            ConnectionStringSettings configurationString =
                ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (configurationString == null || string.IsNullOrWhiteSpace(configurationString.ConnectionString))
            {
                Logger.ErrorFormat("Falta la cadena de conexión '{0}'.", MAIN_CONNECTION_STRING_NAME);

                throw ThrowTechnicalFault(
                    ERROR_CONFIG,
                    MESSAGE_CONFIG_ERROR,
                    CONTEXT_GET_CONNECTION_STRING,
                    new ConfigurationErrorsException(
                        string.Format("Falta la cadena de conexión '{0}'.", MAIN_CONNECTION_STRING_NAME)));
            }

            return configurationString.ConnectionString;
        }

        private static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat("Falla de servicio. Código='{0}', Mensaje='{1}'", code, message);

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
                throw ThrowFault(ERROR_AUTENTICACION_REQUERIDA, MESSAGE_MISSING_TOKEN);
            }

            AuthToken authToken;
            if (!TokenCache.TryGetValue(token, out authToken))
            {
                throw ThrowFault(ERROR_AUTENTICACION_INVALIDA, MESSAGE_INVALID_TOKEN);
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault(ERROR_AUTENTICACION_EXPIRADA, MESSAGE_EXPIRED_TOKEN);
            }

            return authToken.UserId;
        }

        public CreateMatchResponse CreateMatch(CreateMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_SOLICITUD_INVALIDA, MESSAGE_INVALID_REQUEST);
            }

            int hostUserId = Authenticate(request.Token);

            try
            {
                var manager = new MatchManager(GetConnectionString());
                CreateMatchResponse response = manager.CreateMatch(hostUserId, request);

                MatchInfo match = response != null ? response.Match : null;
                if (match == null)
                {
                    throw ThrowTechnicalFault(
                        ERROR_INESPERADO,
                        MESSAGE_UNEXPECTED_ERROR,
                        CONTEXT_CREATE_MATCH_NULL_MATCH,
                        new InvalidOperationException("MatchManager regresó Match null."));
                }

                match.MatchId = Guid.NewGuid();

                IMatchmakingClientCallback cb = null;
                if (OperationContext.Current != null)
                {
                    cb = OperationContext.Current.GetCallbackChannel<IMatchmakingClientCallback>();
                }

                if (cb != null)
                {
                    Cbs[match.MatchId] = cb;

                    try
                    {
                        cb.OnMatchCreated(match);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error al invocar callback OnMatchCreated.", ex);
                    }
                }

                return response;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_BASE_DATOS,
                    MESSAGE_DB_ERROR,
                    CONTEXT_CREATE_MATCH,
                    ex);
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_INESPERADO,
                    MESSAGE_UNEXPECTED_ERROR,
                    CONTEXT_CREATE_MATCH,
                    ex);
            }
        }

        public JoinMatchResponse JoinMatch(JoinMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_SOLICITUD_INVALIDA, MESSAGE_INVALID_REQUEST);
            }

            Authenticate(request.Token);

            try
            {
                var match = new MatchInfo
                {
                    MatchId = Guid.NewGuid(),
                    MatchCode = request.MatchCode,
                    Players = new List<PlayerSummary>(),
                    State = "Waiting"
                };

                return new JoinMatchResponse { Match = match };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_INESPERADO,
                    MESSAGE_UNEXPECTED_ERROR,
                    CONTEXT_JOIN_MATCH,
                    ex);
            }
        }

        public void LeaveMatch(LeaveMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_SOLICITUD_INVALIDA, MESSAGE_INVALID_REQUEST);
            }

            Authenticate(request.Token);

            try
            {
                IMatchmakingClientCallback removed;
                Cbs.TryRemove(request.MatchId, out removed);
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_INESPERADO,
                    MESSAGE_UNEXPECTED_ERROR,
                    CONTEXT_LEAVE_MATCH,
                    ex);
            }
        }

        public StartMatchResponse StartMatch(StartMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_SOLICITUD_INVALIDA, MESSAGE_INVALID_REQUEST);
            }

            Authenticate(request.Token);

            try
            {
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
                        Logger.Warn("Error al invocar callback OnMatchStarted.", ex);
                    }
                }

                return new StartMatchResponse { Match = match };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_INESPERADO,
                    MESSAGE_UNEXPECTED_ERROR,
                    CONTEXT_START_MATCH,
                    ex);
            }
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
