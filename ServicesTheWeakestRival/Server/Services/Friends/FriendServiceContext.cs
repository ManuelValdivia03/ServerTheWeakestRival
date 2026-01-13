using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data.SqlClient;
using System.ServiceModel;
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class FriendServiceContext
    {
        internal static readonly ILog Logger = LogManager.GetLogger(typeof(FriendServiceContext));

        internal static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private const string EMPTY_STRING = "";

        internal const int ONLINE_WINDOW_SECONDS = 75;
        internal const int MAX_EMAIL_LENGTH = 320;
        internal const int MAX_DISPLAY_NAME_LENGTH = 100;
        internal const int DEVICE_MAX_LENGTH = 64;
        internal const int DEFAULT_MAX_RESULTS = 20;
        internal const int MAX_RESULTS_LIMIT = 100;

        private const int MIN_ALLOWED_MAX_RESULTS = 1;

        internal const int SQL_ERROR_UNIQUE_CONSTRAINT = 2627;
        internal const int SQL_ERROR_UNIQUE_INDEX = 2601;
        internal const string SQL_LIKE_WILDCARD = "%";

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";
        private const int EXECUTE_DB_ACTION_RESULT = 0;

        private const string LOG_MISSING_CONNECTION_STRING_TEMPLATE = "Missing connection string '{0}'.";
        private const string ERROR_CONFIG = "Error de configuracion";
        private const string MESSAGE_CONFIG_ERROR = "Configuration error. Please contact support.";
        private const string CONTEXT_GET_CONNECTION_STRING = "FriendService.GetConnectionString";

        private const string LOG_SERVICE_FAULT_TEMPLATE = "Service fault. Code='{0}', Message='{1}'";

        private const string ERROR_AUTH_REQUIRED = "AUTH_REQUIRED";
        private const string ERROR_AUTH_INVALID = "AUTH_INVALID";
        private const string ERROR_AUTH_EXPIRED = "AUTH_EXPIRED";

        private const string MESSAGE_AUTH_REQUIRED = "Missing token.";
        private const string MESSAGE_AUTH_INVALID = "Invalid token.";
        private const string MESSAGE_AUTH_EXPIRED = "Token expired.";

        internal const string ERROR_INVALID_REQUEST = "Solicitud inválida";
        internal const string ERROR_INVALID_REQUEST_MESSAGE = "La solicitud es inválida o está incompleta.";
        internal const string ERROR_RACE = "Conflicto de concurrencia";
        internal const string ERROR_DB = "Error de base de datos";
        internal const string ERROR_UNEXPECTED = "Error inesperado";
        internal const string ERROR_FR_SELF = "Solicitud a uno mismo no permitida";
        internal const string ERROR_FR_ALREADY = "Amistad ya existente";
        internal const string ERROR_FR_NOT_FOUND = "Solicitud no encontrada";
        internal const string ERROR_FR_NOT_PENDING = "Solicitud no pendiente";
        internal const string ERROR_FR_FORBIDDEN = "Acceso no autorizado";
        internal const string ERROR_FORBIDDEN = "Acceso denegado";

        internal const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";
        internal const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";
        internal const string MESSAGE_STATE_CHANGED =
            "El estado cambió. Refresca.";
        internal const string MESSAGE_FR_SELF =
            "No puedes enviarte solicitud a ti mismo.";
        internal const string MESSAGE_FR_ALREADY =
            "Ya existe una amistad entre estas cuentas.";
        internal const string MESSAGE_FR_NOT_FOUND =
            "La solicitud no existe.";
        internal const string MESSAGE_FR_NOT_PENDING_ALREADY_PROCESSED =
            "La solicitud ya fue procesada.";
        internal const string MESSAGE_FR_NOT_PENDING_ALREADY_RESOLVED =
            "La solicitud ya fue resuelta.";
        internal const string MESSAGE_FR_FORBIDDEN_NOT_INVOLVED =
            "No estás involucrado en esta solicitud.";
        internal const string MESSAGE_FORBIDDEN_CANNOT_ACCEPT =
            "No puedes aceptar esta solicitud.";
        internal const string MESSAGE_FR_RACE_REOPEN_FAILED =
            "No se pudo crear ni reabrir la solicitud (carrera).";

        internal const string PARAM_ME = "@Me";
        internal const string PARAM_TARGET = "@Target";
        internal const string PARAM_ACCEPTED = "@Accepted";
        internal const string PARAM_PENDING = "@Pending";
        internal const string PARAM_DECLINED = "@Declined";
        internal const string PARAM_CANCELLED = "@Cancelled";
        internal const string PARAM_ID = "@Id";
        internal const string PARAM_OTHER = "@Other";
        internal const string PARAM_DEVICE = "@Dev";
        internal const string PARAM_WINDOW = "@Window";
        internal const string PARAM_MAX = "@Max";
        internal const string PARAM_QUERY_EMAIL = "@Qemail";
        internal const string PARAM_QUERY_NAME = "@Qname";
        internal const string PARAM_REJECTED = "@Rejected";
        internal const string PARAM_REQUEST_ID = "@ReqId";
        internal const string PARAM_ID_LIST_PREFIX = "@p";

        internal const string FALLBACK_USERNAME_PREFIX = "user:";

        internal const string CONTEXT_SEND_FRIEND_REQUEST = "FriendService.SendFriendRequest";
        internal const string CONTEXT_ACCEPT_FRIEND_REQUEST = "FriendService.AcceptFriendRequest";
        internal const string CONTEXT_REJECT_FRIEND_REQUEST = "FriendService.RejectFriendRequest";
        internal const string CONTEXT_REMOVE_FRIEND = "FriendService.RemoveFriend";
        internal const string CONTEXT_LIST_FRIENDS = "FriendService.ListFriends";
        internal const string CONTEXT_PRESENCE_HEARTBEAT = "FriendService.PresenceHeartbeat";
        internal const string CONTEXT_GET_FRIENDS_PRESENCE = "FriendService.GetFriendsPresence";
        internal const string CONTEXT_SEARCH_ACCOUNTS = "FriendService.SearchAccounts";
        internal const string CONTEXT_GET_ACCOUNTS_BY_IDS = "FriendService.GetAccountsByIds";

        internal const int MAX_LOBBY_CODE_LENGTH = 32;

        internal const string CONTEXT_SEND_LOBBY_INVITE_EMAIL = "FriendService.SendLobbyInviteEmail";

        internal const string ERROR_INVITE_INVALID_TARGET = "Invitación: jugador inválido";
        internal const string ERROR_INVITE_INVALID_CODE = "Invitación: código de lobby inválido";
        internal const string ERROR_INVITE_NOT_FRIEND = "Invitación: no es tu amigo";
        internal const string ERROR_INVITE_ACCOUNT_NOT_FOUND = "Invitación: cuenta no encontrada";
        internal const string ERROR_INVITE_EMAIL_FAILED = "Invitación: fallo al enviar correo";

        internal const string MESSAGE_INVITE_INVALID_TARGET = "Jugador inválido.";
        internal const string MESSAGE_INVITE_INVALID_CODE = "No hay un código de lobby válido.";
        internal const string MESSAGE_INVITE_NOT_FRIEND = "Solo puedes invitar a tus amigos.";
        internal const string MESSAGE_INVITE_ACCOUNT_NOT_FOUND = "No se pudo encontrar la cuenta del destinatario.";
        internal const string MESSAGE_INVITE_EMAIL_FAILED = "No se pudo enviar la invitación. Intenta más tarde.";

        public const string OP_KEY_SEND_FRIEND_REQUEST = "Friends.SendFriendRequest";
        public const string OP_KEY_ACCEPT_FRIEND_REQUEST = "Friends.AcceptFriendRequest";
        public const string OP_KEY_REJECT_FRIEND_REQUEST = "Friends.RejectFriendRequest";
        public const string OP_KEY_REMOVE_FRIEND = "Friends.RemoveFriend";
        public const string KEY_FR_REQUEST_ALREADY_PROCESSED = "Friends.FriendRequests.RequestAlreadyProcessed";

        internal static string GetConnectionString()
        {
            ConnectionStringSettings connectionStringSettings =
                ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (connectionStringSettings == null
                || string.IsNullOrWhiteSpace(connectionStringSettings.ConnectionString))
            {
                Logger.ErrorFormat(LOG_MISSING_CONNECTION_STRING_TEMPLATE, MAIN_CONNECTION_STRING_NAME);

                throw ThrowTechnicalFault(
                    ERROR_CONFIG,
                    MESSAGE_CONFIG_ERROR,
                    CONTEXT_GET_CONNECTION_STRING,
                    new ConfigurationErrorsException(
                        string.Format(LOG_MISSING_CONNECTION_STRING_TEMPLATE, MAIN_CONNECTION_STRING_NAME)));
            }

            return connectionStringSettings.ConnectionString;
        }

        internal static void ValidateRequest(object request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }
        }

        internal static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat(LOG_SERVICE_FAULT_TEMPLATE, code, message);

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

        internal static AvatarAppearanceDto MapAvatar(UserAvatarEntity entity)
        {
            if (entity == null)
            {
                return new AvatarAppearanceDto
                {
                    BodyColor = AvatarBodyColor.Blue,
                    PantsColor = AvatarPantsColor.Black,
                    HatType = AvatarHatType.None,
                    HatColor = AvatarHatColor.Default,
                    FaceType = AvatarFaceType.Default,
                    UseProfilePhotoAsFace = false
                };
            }

            return new AvatarAppearanceDto
            {
                BodyColor = (AvatarBodyColor)entity.BodyColor,
                PantsColor = (AvatarPantsColor)entity.PantsColor,
                HatType = (AvatarHatType)entity.HatType,
                HatColor = (AvatarHatColor)entity.HatColor,
                FaceType = (AvatarFaceType)entity.FaceType,
                UseProfilePhotoAsFace = entity.UseProfilePhoto
            };
        }

        internal static bool IsUniqueViolation(SqlException ex)
        {
            if (ex == null)
            {
                return false;
            }

            return ex.Number == SQL_ERROR_UNIQUE_CONSTRAINT
                || ex.Number == SQL_ERROR_UNIQUE_INDEX;
        }

        internal static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ThrowFault(ERROR_AUTH_REQUIRED, MESSAGE_AUTH_REQUIRED);
            }

            if (!TokenCache.TryGetValue(token, out AuthToken authToken))
            {
                throw ThrowFault(ERROR_AUTH_INVALID, MESSAGE_AUTH_INVALID);
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault(ERROR_AUTH_EXPIRED, MESSAGE_AUTH_EXPIRED);
            }

            return authToken.UserId;
        }

        internal static T ExecuteDbOperation<T>(
            string context,
            Func<SqlConnection, T> operation)
        {
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
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    context,
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    context,
                    ex);
            }
        }

        internal static void ExecuteDbAction(
            string context,
            Action<SqlConnection> action)
        {
            ExecuteDbOperation<int>(
                context,
                connection =>
                {
                    action(connection);
                    return EXECUTE_DB_ACTION_RESULT;
                });
        }

        internal static string NormalizeQuery(string query)
        {
            return (query ?? EMPTY_STRING).Trim();
        }

        internal static int NormalizeMaxResults(int requestedMaxResults)
        {
            if (requestedMaxResults < MIN_ALLOWED_MAX_RESULTS
                || requestedMaxResults > MAX_RESULTS_LIMIT)
            {
                return DEFAULT_MAX_RESULTS;
            }

            return requestedMaxResults;
        }

        internal static string BuildLikeQuery(string query)
        {
            string safeQuery = NormalizeQuery(query);
            return SQL_LIKE_WILDCARD + safeQuery + SQL_LIKE_WILDCARD;
        }

        internal static int? ExecuteScalarInt(SqlCommand command)
        {
            if (command == null)
            {
                return null;
            }

            object scalarValue = command.ExecuteScalar();
            if (scalarValue == null || scalarValue == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt32(scalarValue);
        }
    }

    internal enum FriendRequestState : byte
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2,
        Cancelled = 3
    }
}
