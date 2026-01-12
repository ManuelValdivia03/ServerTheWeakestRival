namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public static class LobbyServiceConstants
    {
        public const int MAX_DISPLAY_NAME_LENGTH = 80;
        public const int MAX_PROFILE_IMAGE_URL_LENGTH = 500;
        public const int MAX_EMAIL_LENGTH = 320;

        public const int DEFAULT_MAX_PLAYERS = 8;
        public const int ACCESS_CODE_MAX_LENGTH = 12;

        public const string DEFAULT_LOBBY_NAME = "Lobby";
        public const string DEFAULT_PLAYER_NAME_PREFIX = "Jugador ";

        public const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        public const string ERROR_NOT_FOUND = "Error";
        public const string ERROR_INVALID_REQUEST = "Error";
        public const string ERROR_VALIDATION_ERROR = "Error";
        public const string ERROR_UNAUTHORIZED = "Error";
        public const string ERROR_EMAIL_TAKEN = "Error";

        public const string ERROR_DB = "Error";
        public const string ERROR_UNEXPECTED = "Error";

        public const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";

        public const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        public const string MESSAGE_ACCESS_CODE_REQUIRED = "AccessCode requerido.";

        public const string PARAM_USER_ID = "@UserId";
        public const string PARAM_LOBBY_ID = "@LobbyId";
        public const string PARAM_ID = "@Id";
        public const string PARAM_EMAIL = "@E";
        public const string PARAM_ACCESS_CODE = "@AccessCode";
        public const string PARAM_LOBBY_UID = "@u";
        public const string PARAM_LOBBY_ID_BY_ID = "@id";

        public const string PARAM_OWNER_USER_ID = "@OwnerUserId";
        public const string PARAM_LOBBY_NAME = "@Name";
        public const string PARAM_MAX_PLAYERS = "@MaxPlayers";

        public const string PARAM_OUT_LOBBY_ID = "@LobbyId";
        public const string PARAM_OUT_LOBBY_UID = "@LobbyUid";
        public const string PARAM_OUT_ACCESS_CODE = "@AccessCode";

        public const string PARAM_DISPLAY_NAME = "@DisplayName";
        public const string PARAM_IMAGE_URL = "@ImageUrl";

        public const string FORCED_LOGOUT_CODE_SANCTION = "SANCTION_APPLIED";

        public const string GUID_NO_DASHES_FORMAT = "N";

        public const string CTX_REGISTER_CALLBACK = "LobbyService.TryRegisterLobbyCallback";
        public const string CTX_JOIN_LOBBY = "LobbyService.JoinLobby";
        public const string CTX_GET_MY_PROFILE = "LobbyService.GetMyProfile";
        public const string CTX_UPDATE_ACCOUNT = "LobbyService.UpdateAccount";
        public const string CTX_CREATE_LOBBY = "LobbyService.CreateLobby";
        public const string CTX_JOIN_BY_CODE = "LobbyService.JoinByCode";
        public const string CTX_START_LOBBY_MATCH = "LobbyService.StartLobbyMatch";
        public const string CTX_UPDATE_AVATAR = "LobbyService.UpdateAvatar";
        internal const string CTX_SEND_CHAT_MESSAGE = "LobbyService.SendChatMessage";

    }
}
