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

        public const string DEFAULT_USER_PREFIX = "User";

        public const string ERROR_CONFIG = "CONFIG_ERROR";
        public const string ERROR_UNAUTHORIZED = "UNAUTHORIZED";
        public const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        public const string ERROR_VALIDATION = "VALIDATION_ERROR";
        public const string ERROR_DB = "DB_ERROR";
        public const string ERROR_TIMEOUT = "TIMEOUT";
        public const string ERROR_CANCELLED = "CANCELLED";
        public const string ERROR_INVALID_OPERATION = "INVALID_OPERATION";
        public const string ERROR_UNEXPECTED = "UNEXPECTED_ERROR";

        public const string MESSAGE_CONFIG_MISSING = "Missing connection string 'TheWeakestRivalDb'.";
        public const string MESSAGE_TOKEN_REQUIRED = "Auth token is required.";
        public const string MESSAGE_TOKEN_INVALID = "Auth token is invalid.";
        public const string MESSAGE_TOKEN_EXPIRED = "Auth token has expired.";

        public const string MESSAGE_REQUEST_NULL = "Request cannot be null.";
        public const string MESSAGE_TEXT_EMPTY = "MessageText cannot be empty.";
        public const string MESSAGE_TEXT_TOO_LONG_PREFIX = "MessageText exceeds ";
        public const string MESSAGE_TEXT_TOO_LONG_SUFFIX = " characters.";

        public const string MESSAGE_DB_SEND = "A database error occurred while sending the chat message.";
        public const string MESSAGE_DB_GET = "A database error occurred while retrieving chat messages.";
        public const string MESSAGE_TIMEOUT_SEND = "The operation timed out while sending the chat message.";
        public const string MESSAGE_TIMEOUT_GET = "The operation timed out while retrieving chat messages.";
        public const string MESSAGE_CANCEL_SEND = "The operation was cancelled while sending the chat message.";
        public const string MESSAGE_CANCEL_GET = "The operation was cancelled while retrieving chat messages.";
        public const string MESSAGE_INVALIDOP_SEND = "An invalid operation occurred while sending the chat message.";
        public const string MESSAGE_INVALIDOP_GET = "An invalid operation occurred while retrieving chat messages.";
        public const string MESSAGE_UNEXPECTED_SEND = "An unexpected error occurred while sending the chat message.";
        public const string MESSAGE_UNEXPECTED_GET = "An unexpected error occurred while retrieving chat messages.";

        public const string CTX_SEND = "ChatService.SendChatMessage";
        public const string CTX_GET = "ChatService.GetChatMessages";
        public const string CTX_AUTH = "ChatService.Authenticate";
        public const string CTX_CONFIG = "ChatService.ResolveConnectionString";
    }
}
