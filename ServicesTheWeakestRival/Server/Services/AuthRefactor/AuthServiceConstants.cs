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

        public const string ERROR_CONFIG = "Error de configuración";
        public const string ERROR_INVALID_REQUEST = "Error de solicitud inválida";
        public const string ERROR_EMAIL_TAKEN = "Error: correo ya registrado";
        public const string ERROR_TOO_SOON = "Error: espera requerida";
        public const string ERROR_CODE_MISSING = "Error: código no encontrado";
        public const string ERROR_CODE_EXPIRED = "Error: código expirado";
        public const string ERROR_CODE_INVALID = "Error: código inválido";
        public const string ERROR_DB_ERROR = "Error de base de datos";
        public const string ERROR_INVALID_CREDENTIALS = "Error: credenciales inválidas";
        public const string ERROR_SMTP = "Error del servicio de correo";
        public const string ERROR_EMAIL_NOT_FOUND = "Error: correo no encontrado";
        public const string ERROR_WEAK_PASSWORD = "Error: contraseña débil";

        public const string ERROR_ACCOUNT_INACTIVE = "Error: cuenta inactiva";
        public const string ERROR_ACCOUNT_SUSPENDED = "Error: cuenta suspendida";
        public const string ERROR_ACCOUNT_BANNED = "Error: cuenta baneada";

        public const string ERROR_ALREADY_LOGGED_IN = "Error: ya hay una sesión iniciada";
        public const string MESSAGE_ALREADY_LOGGED_IN = "Ya hay una sesión iniciada.";

        public const string ERROR_FORCED_LOGOUT = "Error al forzar cierre de sesión";
        public const string MESSAGE_FORCED_LOGOUT = "Tu sesión fue reemplazada por otro inicio de sesión.";

        public const string MESSAGE_INVALID_REQUEST = "Solicitud inválida.";
        public const string MESSAGE_CONFIG_ERROR = "Ocurrió un error de configuración.";
        public const string MESSAGE_PAYLOAD_NULL = "La solicitud es nula.";
        public const string MESSAGE_EMAIL_REQUIRED = "El correo es obligatorio.";
        public const string MESSAGE_VERIFICATION_EMAIL_FAILED =
            "No se pudo enviar el correo de verificación. Intenta de nuevo más tarde.";
        public const string MESSAGE_PASSWORD_RESET_EMAIL_FAILED =
            "No se pudo enviar el correo para restablecer la contraseña. Intenta de nuevo más tarde.";
        public const string MESSAGE_INVALID_SESSION = "Sesión inválida.";
        public const string MESSAGE_INVALID_CREDENTIALS = "Correo o contraseña incorrectos.";
        public const string MESSAGE_ACCOUNT_NOT_ACTIVE = "La cuenta no está activa.";
        public const string MESSAGE_ACCOUNT_SUSPENDED = "La cuenta está suspendida.";
        public const string MESSAGE_ACCOUNT_BANNED = "La cuenta está baneada.";

        public const string MESSAGE_UNEXPECTED_DB_LOGIN =
            "Ocurrió un error inesperado de base de datos al iniciar sesión.";
        public const string MESSAGE_UNEXPECTED_DB_LOGOUT =
            "Ocurrió un error inesperado de base de datos al cerrar sesión.";
        public const string MESSAGE_COMPLETE_REGISTER_REQUIRED_FIELDS =
            "El correo, nombre visible, contraseña y código son obligatorios.";

        public const string MESSAGE_VERIFICATION_CODE_EXPIRED =
            "El código de verificación expiró. Solicita uno nuevo.";
        public const string MESSAGE_UNEXPECTED_DB_COMPLETE_REGISTER =
            "Ocurrió un error inesperado de base de datos al completar el registro.";
        public const string MESSAGE_ACCOUNT_NOT_CREATED = "No se pudo crear la cuenta.";

        public const string MESSAGE_COMPLETE_RESET_REQUIRED_FIELDS =
            "El correo, el código y la nueva contraseña son obligatorios.";
        public const string MESSAGE_RESET_CODE_EXPIRED =
            "El código de restablecimiento expiró. Solicita uno nuevo.";
        public const string MESSAGE_EMAIL_NOT_REGISTERED = "Correo no registrado.";
        public const string MESSAGE_UNEXPECTED_DB_COMPLETE_RESET =
            "Ocurrió un error inesperado de base de datos al completar el restablecimiento de contraseña.";

        public const string MESSAGE_REGISTER_REQUIRED_FIELDS =
            "El correo, nombre visible y contraseña son obligatorios.";
        public const string MESSAGE_UNEXPECTED_DB_REGISTER =
            "Ocurrió un error inesperado de base de datos al registrar.";

        public const string MESSAGE_PONG = "pong";
        public const string MESSAGE_USER_ID_REQUIRED = "El UserId es obligatorio.";

        public const string MESSAGE_PROFILE_IMAGE_DB_ERROR =
            "Ocurrió un error inesperado de base de datos al leer la imagen de perfil.";

        public const string MESSAGE_PASSWORD_MIN_LENGTH_NOT_MET =
            "La contraseña no cumple con la longitud mínima requerida ({0} caracteres).";

        public const string ERROR_UNEXPECTED = "UNEXPECTED_ERROR";
        public const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        public const string MESSAGE_VERIFICATION_CODE_MISSING =
            "No hay un código pendiente. Solicita uno nuevo.";
        public const string MESSAGE_RESET_CODE_MISSING =
            "No hay un código de restablecimiento pendiente. Solicita uno nuevo.";

        public const string MESSAGE_EMAIL_TAKEN = "El correo ya está registrado.";
        public const string MESSAGE_TOO_SOON = "Espera antes de solicitar otro código.";

        public const string MESSAGE_UNEXPECTED_DB_BEGIN_REGISTER =
            "Ocurrió un error inesperado de base de datos al iniciar el registro.";
        public const string MESSAGE_UNEXPECTED_DB_BEGIN_RESET =
            "Ocurrió un error inesperado de base de datos al iniciar el restablecimiento de contraseña.";
        public const string MESSAGE_RESET_CODE_INVALID = "El código de restablecimiento es inválido.";
        public const string MESSAGE_VERIFICATION_CODE_INVALID = "El código de verificación es inválido.";

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

        public const string MESSAGE_INVALID_USER_ID = "El UserId es inválido.";

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
