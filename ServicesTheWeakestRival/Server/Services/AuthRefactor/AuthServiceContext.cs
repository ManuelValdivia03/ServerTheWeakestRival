using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public static class AuthServiceContext
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(AuthServiceContext));

        public static int CodeTtlMinutes =>
            ParseIntAppSetting(AuthServiceConstants.APPSETTING_EMAIL_CODE_TTL_MINUTES, AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

        public static int ResendCooldownSeconds =>
            ParseIntAppSetting(AuthServiceConstants.APPSETTING_EMAIL_RESEND_COOLDOWN_SECONDS, AuthServiceConstants.DEFAULT_RESEND_COOLDOWN_SECONDS);

        public static string ResolveConnectionString(string name)
        {
            var connection = ConfigurationManager.ConnectionStrings[name];

            if (connection == null || string.IsNullOrWhiteSpace(connection.ConnectionString))
            {
                var configurationException = new ConfigurationErrorsException(
                    string.Format("Missing connection string '{0}'.", name));

                throw ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_CONFIG,
                    AuthServiceConstants.MESSAGE_CONFIG_ERROR,
                    AuthServiceConstants.CTX_GET_CONNECTION,
                    configurationException);
            }

            return connection.ConnectionString;
        }

        public static AuthToken IssueToken(int userId)
        {
            if (userId <= 0)
            {
                throw ThrowFault(AuthServiceConstants.ERROR_INVALID_REQUEST, AuthServiceConstants.MESSAGE_INVALID_USER_ID);
            }

            if (TokenStore.TryGetActiveTokenForUser(userId, out _))
            {
                throw ThrowFault(
                    AuthServiceConstants.ERROR_ALREADY_LOGGED_IN,
                    AuthServiceConstants.MESSAGE_ALREADY_LOGGED_IN);
            }

            string tokenValue = Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);
            DateTime expiresAt = DateTime.UtcNow.AddHours(AuthServiceConstants.TOKEN_TTL_HOURS);

            var token = new AuthToken
            {
                UserId = userId,
                Token = tokenValue,
                ExpiresAtUtc = expiresAt
            };

            TokenStore.StoreToken(token);
            return token;
        }

        public static bool TryRemoveToken(string tokenValue, out AuthToken token)
        {
            return TokenStore.TryRemoveToken(tokenValue, out token);
        }

        public static bool TryGetUserId(string tokenValue, out int userId)
        {
            return TokenStore.TryGetUserId(tokenValue, out userId);
        }

        public static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat(
                "Business fault. Code={0}. Message={1}.",
                code,
                message);

            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        public static FaultException<ServiceFault> ThrowTechnicalFault(
            string technicalCode,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            var fault = new ServiceFault
            {
                Code = technicalCode,
                Message = userMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }

        private static int ParseIntAppSetting(string key, int @default)
        {
            return int.TryParse(ConfigurationManager.AppSettings[key], out int value) ? value : @default;
        }

        internal static Exception CreateSqlTechnicalFault(
            string technicalErrorCode,
            string messageKey,
            string context,
            SqlException ex)
        {
            return ThrowTechnicalFault(technicalErrorCode, messageKey, context, ex);
        }
    }
}
