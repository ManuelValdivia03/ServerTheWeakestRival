namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public static class AuthServiceConstants
    {
        public const int DEFAULT_CODE_TTL_MINUTES = 10;
        public const int DEFAULT_RESEND_COOLDOWN_SECONDS = 60;
        public const int TOKEN_TTL_HOURS = 24;

        public const byte ACCOUNT_STATUS_ACTIVE = 1;
        public const byte ACCOUNT_STATUS_INACTIVE = 2;
        public const byte ACCOUNT_STATUS_SUSPENDED = 3;
        public const byte ACCOUNT_STATUS_BANNED = 4;

        public const int EMAIL_MAX_LENGTH = 320;
        public const int DISPLAY_NAME_MAX_LENGTH = 80;
        public const int PROFILE_URL_MAX_LENGTH = 500;
        public const int EMAIL_CODE_LENGTH = 6;
        public const int PASSWORD_MIN_LENGTH = 8;

        public const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        public const string PARAMETER_EMAIL = "@Email";

        public const string ERROR_CONFIG = "CONFIG_ERROR";
        public const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        public const string ERROR_EMAIL_TAKEN = "EMAIL_TAKEN";
        public const string ERROR_TOO_SOON = "TOO_SOON";
        public const string ERROR_CODE_MISSING = "CODE_MISSING";
        public const string ERROR_CODE_EXPIRED = "CODE_EXPIRED";
        public const string ERROR_CODE_INVALID = "CODE_INVALID";
        public const string ERROR_DB_ERROR = "DB_ERROR";
        public const string ERROR_INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
        public const string ERROR_SMTP = "SMTP_ERROR";
        public const string ERROR_EMAIL_NOT_FOUND = "EMAIL_NOT_FOUND";
        public const string ERROR_WEAK_PASSWORD = "WEAK_PASSWORD";

        public const string ERROR_ACCOUNT_INACTIVE = "ACCOUNT_INACTIVE";
        public const string ERROR_ACCOUNT_SUSPENDED = "ACCOUNT_SUSPENDED";
        public const string ERROR_ACCOUNT_BANNED = "ACCOUNT_BANNED";

        public const string MESSAGE_CONFIG_ERROR = "Configuration error. Please contact support.";
        public const string MESSAGE_PAYLOAD_NULL = "Request payload is null.";

        public const string CTX_GET_CONNECTION = "AuthService.GetConnectionString";
        public const string CTX_BEGIN_REGISTER = "BeginRegister.EmailSender";
        public const string CTX_COMPLETE_REGISTER = "CompleteRegister.Tx";
        public const string CTX_REGISTER = "Register.Tx";
        public const string CTX_LOGIN = "Login.Db";
        public const string CTX_LOGOUT_LEAVE_ALL = "Logout.LeaveAllByUser";
        public const string CTX_BEGIN_RESET = "BeginPasswordReset.EmailSender";
        public const string CTX_COMPLETE_RESET = "CompletePasswordReset.Tx";
    }
}
