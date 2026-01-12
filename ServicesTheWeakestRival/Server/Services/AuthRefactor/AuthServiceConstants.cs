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

        public const string CONFIG = "Error de configuración";
        public const string INVALID_REQUEST = "Error de solicitud inválida";
        public const string EMAIL_TAKEN = "Error: correo ya registrado";
        public const string TOO_SOON = "Error: espera requerida";
        public const string CODE_MISSING = "Error: código no encontrado";
        public const string CODE_EXPIRED = "Error: código expirado";
        public const string CODE_INVALID = "Error: código inválido";
        public const string DB_ERROR = "Error de base de datos";
        public const string INVALID_CREDENTIALS = "Error: credenciales inválidas";
        public const string SMTP = "Error del servicio de correo";
        public const string EMAIL_NOT_FOUND = "Error: correo no encontrado";
        public const string WEAK_PASSWORD = "Error: contraseña débil";

        public const string ACCOUNT_INACTIVE = "Error: cuenta inactiva";
        public const string ACCOUNT_SUSPENDED = "Error: cuenta suspendida";
        public const string ACCOUNT_BANNED = "Error: cuenta baneada";

        public const string ALREADY_LOGGED_IN = "Error: ya hay una sesión iniciada";
        public const string ALREADY_LOGGED_IN_MESSAGE_KEY = "Auth.IssueToken.AlreadyLoggedIn";

        public const string FORCED_LOGOUT = "Error al forzar cierre de sesión";
        public const string FORCED_LOGOUT_MESSAGE = "Tu sesión fue reemplazada por otro inicio de sesión.";

        public const string INVALID_REQUEST_MESSAGE = "Solicitud inválida.";
        public const string CONFIG_ERROR_MESSAGE = "Ocurrió un error de configuración.";
        public const string PAYLOAD_NULL_MESSAGE = "La solicitud es nula.";
        public const string EMAIL_REQUIRED_MESSAGE = "El correo es obligatorio.";
        public const string VERIFICATION_EMAIL_FAILED_MESSAGE =
            "No se pudo enviar el correo de verificación. Intenta de nuevo más tarde.";
        public const string PASSWORD_RESET_EMAIL_FAILED_MESSAGE =
            "No se pudo enviar el correo para restablecer la contraseña. Intenta de nuevo más tarde.";
        public const string INVALID_SESSION_MESSAGE = "Sesión inválida.";
        public const string INVALID_CREDENTIALS_MESSAGE = "Correo o contraseña incorrectos.";
        public const string ACCOUNT_NOT_ACTIVE_MESSAGE = "La cuenta no está activa.";
        public const string ACCOUNT_SUSPENDED_MESSAGE = "La cuenta está suspendida.";
        public const string ACCOUNT_BANNED_MESSAGE = "La cuenta está baneada.";

        public const string UNEXPECTED_DB_LOGIN_MESSAGE =
            "Ocurrió un error inesperado de base de datos al iniciar sesión.";
        public const string UNEXPECTED_DB_LOGOUT_MESSAGE =
            "Ocurrió un error inesperado de base de datos al cerrar sesión.";
        public const string COMPLETE_REGISTER_REQUIRED_FIELDS_MESSAGE =
            "El correo, nombre visible, contraseña y código son obligatorios.";

        public const string VERIFICATION_CODE_EXPIRED_MESSAGE =
            "El código de verificación expiró. Solicita uno nuevo.";
        public const string UNEXPECTED_DB_COMPLETE_REGISTER_MESSAGE =
            "Ocurrió un error inesperado de base de datos al completar el registro.";
        public const string ACCOUNT_NOT_CREATED_MESSAGE = "No se pudo crear la cuenta.";

        public const string COMPLETE_RESET_REQUIRED_FIELDS_MESSAGE =
            "El correo, el código y la nueva contraseña son obligatorios.";
        public const string RESET_CODE_EXPIRED_MESSAGE =
            "El código de restablecimiento expiró. Solicita uno nuevo.";
        public const string EMAIL_NOT_REGISTERED_MESSAGE_KEY = "Auth.Email.NotRegistered";
        public const string UNEXPECTED_DB_COMPLETE_RESET_MESSAGE =
            "Ocurrió un error inesperado de base de datos al completar el restablecimiento de contraseña.";

        public const string REGISTER_REQUIRED_FIELDS_MESSAGE =
            "El correo, nombre visible y contraseña son obligatorios.";
        public const string UNEXPECTED_DB_REGISTER_MESSAGE =
            "Ocurrió un error inesperado de base de datos al registrar.";

        public const string PONG_MESSAGE = "pong";
        public const string USER_ID_REQUIRED_MESSAGE = "El UserId es obligatorio.";

        public const string PROFILE_IMAGE_DB_ERROR_MESSAGE =
            "Ocurrió un error inesperado de base de datos al leer la imagen de perfil.";

        public const string PASSWORD_MIN_LENGTH_NOT_MET_MESSAGE =
            "La contraseña no cumple con la longitud mínima requerida ({0} caracteres).";

        public const string UNEXPECTED = "UNEXPECTED_ERROR";
        public const string UNEXPECTED_ERROR_MESSAGE_KEY = "Auth.Unexpected";

        public const string VERIFICATION_CODE_MISSING_MESSAGE =
            "No hay un código pendiente. Solicita uno nuevo.";
        public const string RESET_CODE_MISSING_MESSAGE =
            "No hay un código de restablecimiento pendiente. Solicita uno nuevo.";

        public const string EMAIL_TAKEN_MESSAGE_KEY = "Auth.Email.Taken";
        public const string TOO_SOON_MESSAGE = "Espera antes de solicitar otro código.";

        public const string UNEXPECTED_DB_BEGIN_REGISTER_MESSAGE =
            "Ocurrió un error inesperado de base de datos al iniciar el registro.";
        public const string UNEXPECTED_DB_BEGIN_RESET_MESSAGE =
            "Ocurrió un error inesperado de base de datos al iniciar el restablecimiento de contraseña.";
        public const string RESET_CODE_INVALID_MESSAGE = "El código de restablecimiento es inválido.";
        public const string VERIFICATION_CODE_INVALID_MESSAGE = "El código de verificación es inválido.";

        public const string GET_CONNECTION_CONTEXT = "AuthService.GetConnectionString";
        public const string BEGIN_REGISTER_CONTEXT = "BeginRegister.EmailSender";
        public const string COMPLETE_REGISTER_CONTEXT = "CompleteRegister.Tx";
        public const string REGISTER_CONTEXT = "Register.Tx";
        public const string LOGIN_CONTEXT = "Login.Db";
        public const string LOGOUT_LEAVE_ALL_CONTEXT = "Logout.LeaveAllByUser";
        public const string BEGIN_RESET_CONTEXT = "BeginPasswordReset.EmailSender";
        public const string COMPLETE_RESET_CONTEXT = "CompletePasswordReset.Tx";
        public const string GET_PROFILE_IMAGE_CONTEXT = "GetProfileImage.Db";

        public const string EMAIL_CODE_TTL_MINUTES_APPSETTING = "EmailCodeTtlMinutes";
        public const string EMAIL_RESEND_COOLDOWN_SECONDS_APPSETTING = "EmailResendCooldownSeconds";

        public const string INVALID_USER_ID_MESSAGE_KEY = "Auth.IssueToken.InvalidUserId";

        public const string TOKEN_GUID_FORMAT = "N";
        public const int SQL_TRUE = 1;

        public const string BEGIN_REGISTER_KEY_PREFIX = "Auth.BeginRegister";
        public const string BEGIN_RESET_KEY_PREFIX = "Auth.BeginPasswordReset";
        public const string COMPLETE_REGISTER_KEY_PREFIX = "Auth.CompleteRegister";
        public const string COMPLETE_RESET_KEY_PREFIX = "Auth.CompletePasswordReset";
        public const string REGISTER_KEY_PREFIX = "Error al registrarse en base de datos";
        public const string LOGIN_KEY_PREFIX = "Auth.Login";
        public const string LOGOUT_KEY_PREFIX = "Auth.Logout";
        public const string GET_PROFILE_IMAGE_KEY_PREFIX = "Auth.GetProfileImage";
    }
}
