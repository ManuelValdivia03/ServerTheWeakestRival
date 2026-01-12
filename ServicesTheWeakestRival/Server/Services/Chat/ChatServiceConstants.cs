namespace ServicesTheWeakestRival.Server.Services.Chat
{
    public static class ChatServiceConstants
    {
        public const int MAX_MESSAGE_LENGTH = 500;
        public const int MAX_DISPLAYNAME_LENGTH = 80;

        public const int DEFAULT_PAGE_SIZE = 50;
        public const int MAX_PAGE_SIZE = 200;

        public const int DEFAULT_COMMAND_TIMEOUT_SECONDS = 30;

        public const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        public const string COL_CHAT_MESSAGE_ID = "chat_message_id";
        public const string COL_USER_ID = "user_id";
        public const string COL_DISPLAY_NAME = "display_name";
        public const string COL_MESSAGE_TEXT = "message_text";
        public const string COL_SENT_UTC = "sent_utc";

        public const string PARAM_USER_ID = "@user_id";
        public const string PARAM_DISPLAY_NAME = "@display_name";
        public const string PARAM_MESSAGE_TEXT = "@message_text";
        public const string PARAM_MAX_COUNT = "@max_count";
        public const string PARAM_SINCE_ID = "@since_id";

        public const string DEFAULT_USER_PREFIX = "Usuario";

        public const string ERROR_CONFIG = "Error de configuración";
        public const string ERROR_UNAUTHORIZED = "No autorizado";
        public const string ERROR_INVALID_REQUEST = "Solicitud inválida";
        public const string ERROR_VALIDATION = "Error de validación";
        public const string ERROR_DB = "Error de base de datos";
        public const string ERROR_TIMEOUT = "Tiempo de espera agotado";
        public const string ERROR_CANCELLED = "Operación cancelada";
        public const string ERROR_INVALID_OPERATION = "Operación inválida";
        public const string ERROR_UNEXPECTED = "Error inesperado";

        public const string MESSAGE_CONFIG_MISSING =
            "Falta la cadena de conexión 'TheWeakestRivalDb'.";
        public const string MESSAGE_TOKEN_REQUIRED =
            "El token de autenticación es obligatorio.";
        public const string MESSAGE_TOKEN_INVALID =
            "El token de autenticación es inválido.";
        public const string MESSAGE_TOKEN_EXPIRED =
            "El token de autenticación expiró.";

        public const string MESSAGE_REQUEST_NULL =
            "La solicitud no puede ser nula.";
        public const string MESSAGE_TEXT_EMPTY =
            "El texto del mensaje no puede estar vacío.";
        public const string MESSAGE_TEXT_TOO_LONG_PREFIX =
            "El texto del mensaje excede ";
        public const string MESSAGE_TEXT_TOO_LONG_SUFFIX =
            " caracteres.";

        public const string MESSAGE_DB_SEND =
            "Ocurrió un error de base de datos al enviar el mensaje del chat.";
        public const string MESSAGE_DB_GET =
            "Ocurrió un error de base de datos al obtener los mensajes del chat.";
        public const string MESSAGE_TIMEOUT_SEND =
            "Se agotó el tiempo de espera al enviar el mensaje del chat.";
        public const string MESSAGE_TIMEOUT_GET =
            "Se agotó el tiempo de espera al obtener los mensajes del chat.";
        public const string MESSAGE_CANCEL_SEND =
            "La operación fue cancelada al enviar el mensaje del chat.";
        public const string MESSAGE_CANCEL_GET =
            "La operación fue cancelada al obtener los mensajes del chat.";
        public const string MESSAGE_INVALIDOP_SEND =
            "Ocurrió una operación inválida al enviar el mensaje del chat.";
        public const string MESSAGE_INVALIDOP_GET =
            "Ocurrió una operación inválida al obtener los mensajes del chat.";
        public const string MESSAGE_UNEXPECTED_SEND =
            "Ocurrió un error inesperado al enviar el mensaje del chat.";
        public const string MESSAGE_UNEXPECTED_GET =
            "Ocurrió un error inesperado al obtener los mensajes del chat.";

        public const string CTX_SEND = "ChatService.SendChatMessage";
        public const string CTX_GET = "ChatService.GetChatMessages";
        public const string CTX_AUTH = "ChatService.Authenticate";
        public const string CTX_CONFIG = "ChatService.ResolveConnectionString";
    }
}
