namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public static class AuthServiceConstants
    {
        public const int DEFAULT_CODE_TTL_MINUTES = 10;
        public const int DEFAULT_RESEND_COOLDOWN_SECONDS = 60;
        public const int TOKEN_TTL_HOURS = 24;
        public const int SHA256_HASH_BYTES = 32;
        public const int PASSWORD_HASH_MAX_LENGTH = 128;
        public const int SQL_VARBINARY_MAX = -1;

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

        public const string ERROR_ALREADY_LOGGED_IN = "ALREADY_LOGGED_IN";
        public const string MESSAGE_ALREADY_LOGGED_IN = "This account is already online.";

        public const string ERROR_FORCED_LOGOUT = "FORCED_LOGOUT";
        public const string MESSAGE_FORCED_LOGOUT = "Session replaced by a new login.";

        public const string MESSAGE_INVALID_REQUEST = "Invalid request.";
        public const string MESSAGE_CONFIG_ERROR = "Configuration error. Please contact support.";
        public const string MESSAGE_PAYLOAD_NULL = "Request payload is null.";
        public const string MESSAGE_EMAIL_REQUIRED = "Email is required.";
        public const string MESSAGE_VERIFICATION_EMAIL_FAILED = "Failed to send verification email. Please try again later.";
        public const string MESSAGE_PASSWORD_RESET_EMAIL_FAILED = "Failed to send password reset email. Please try again later.";
        public const string MESSAGE_INVALID_SESSION = "Invalid session.";
        public const string MESSAGE_INVALID_CREDENTIALS = "Email or password is incorrect.";
        public const string MESSAGE_ACCOUNT_NOT_ACTIVE = "Account is not active.";
        public const string MESSAGE_ACCOUNT_SUSPENDED = "Account is suspended.";
        public const string MESSAGE_ACCOUNT_BANNED = "Account is banned.";

        public const string MESSAGE_UNEXPECTED_DB_LOGIN = "Unexpected database error while logging in.";
        public const string MESSAGE_UNEXPECTED_DB_LOGOUT = "Unexpected database error while logging out.";
        public const string MESSAGE_COMPLETE_REGISTER_REQUIRED_FIELDS = "Email, display name, password and code are required.";

        public const string MESSAGE_VERIFICATION_CODE_EXPIRED = "Verification code expired. Request a new one.";
        public const string MESSAGE_UNEXPECTED_DB_COMPLETE_REGISTER = "Unexpected database error while completing registration.";
        public const string MESSAGE_ACCOUNT_NOT_CREATED = "Account was not created.";

        public const string MESSAGE_COMPLETE_RESET_REQUIRED_FIELDS = "Email, code and new password are required.";
        public const string MESSAGE_RESET_CODE_EXPIRED = "Reset code expired. Request a new one.";
        public const string MESSAGE_EMAIL_NOT_REGISTERED = "No account is registered with that email.";
        public const string MESSAGE_UNEXPECTED_DB_COMPLETE_RESET = "Unexpected database error while completing password reset.";

        public const string MESSAGE_REGISTER_REQUIRED_FIELDS = "Email, display name and password are required.";
        public const string MESSAGE_UNEXPECTED_DB_REGISTER = "Unexpected database error while registering.";

        public const string MESSAGE_PONG = "pong";
        public const string MESSAGE_USER_ID_REQUIRED = "UserId is required.";

        public const string MESSAGE_PROFILE_IMAGE_DB_ERROR = "Unexpected database error while reading profile image.";

        public const string MESSAGE_PASSWORD_MIN_LENGTH_NOT_MET =
            "Password does not meet the minimum length requirements ({0} characters).";

        public const string ERROR_UNEXPECTED = "UNEXPECTED_ERROR";
        public const string MESSAGE_UNEXPECTED_ERROR = "Unexpected error. Please try again later.";


        public const string MESSAGE_VERIFICATION_CODE_MISSING = "No pending code. Request a new one.";
        public const string MESSAGE_RESET_CODE_MISSING = "No pending reset code. Request a new one.";

        public const string MESSAGE_EMAIL_TAKEN = "Email is already registered.";
        public const string MESSAGE_TOO_SOON = "Please wait before requesting another code.";

        public const string MESSAGE_UNEXPECTED_DB_BEGIN_REGISTER = "Unexpected database error while starting registration.";
        public const string MESSAGE_UNEXPECTED_DB_BEGIN_RESET = "Unexpected database error while starting password reset.";
        public const string MESSAGE_RESET_CODE_INVALID = "Invalid reset code.";
        public const string MESSAGE_VERIFICATION_CODE_INVALID = "Invalid verification code.";

        public const string CTX_GET_CONNECTION = "AuthService.GetConnectionString";
        public const string CTX_BEGIN_REGISTER = "BeginRegister.EmailSender";
        public const string CTX_COMPLETE_REGISTER = "CompleteRegister.Tx";
        public const string CTX_REGISTER = "Register.Tx";
        public const string CTX_LOGIN = "Login.Db";
        public const string CTX_LOGOUT_LEAVE_ALL = "Logout.LeaveAllByUser";
        public const string CTX_BEGIN_RESET = "BeginPasswordReset.EmailSender";
        public const string CTX_COMPLETE_RESET = "CompletePasswordReset.Tx";
        public const string CTX_GET_PROFILE_IMAGE = "GetProfileImage.Db";

        public const string APPSETTING_EMAIL_CODE_TTL_MINUTES = "EmailCodeTtlMinutes";
        public const string APPSETTING_EMAIL_RESEND_COOLDOWN_SECONDS = "EmailResendCooldownSeconds";

        public const string MESSAGE_INVALID_USER_ID = "Invalid user id.";

        public const string TOKEN_GUID_FORMAT = "N";
        public const int SQL_TRUE = 1;

        public const string KEY_PREFIX_BEGIN_REGISTER = "Auth.BeginRegister";
        public const string KEY_PREFIX_BEGIN_RESET = "Auth.BeginPasswordReset";
        public const string KEY_PREFIX_COMPLETE_REGISTER = "Auth.CompleteRegister";
        public const string KEY_PREFIX_COMPLETE_RESET = "Auth.CompletePasswordReset";
        public const string KEY_PREFIX_REGISTER = "Auth.Register";
        public const string KEY_PREFIX_LOGIN = "Auth.Login";
        public const string KEY_PREFIX_LOGOUT = "Auth.Logout";
        public const string KEY_PREFIX_GET_PROFILE_IMAGE = "Auth.GetProfileImage";
    }
}
