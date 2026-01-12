using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services;
using System;
using System.Configuration;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public static class AuthServiceContext
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(AuthServiceContext));

        private const int USER_ID_MIN_VALUE = 1;
        private const int TOKEN_GENERATION_MAX_ATTEMPTS = 8;

        private const string CTX_GET_CONNECTION = AuthServiceConstants.CTX_GET_CONNECTION;
        private const string CTX_ISSUE_TOKEN = "AuthServiceContext.IssueToken";

        public static int CodeTtlMinutes =>
            ParseIntAppSetting(
                AuthServiceConstants.APPSETTING_EMAIL_CODE_TTL_MINUTES,
                AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

        public static int ResendCooldownSeconds =>
            ParseIntAppSetting(
                AuthServiceConstants.APPSETTING_EMAIL_RESEND_COOLDOWN_SECONDS,
                AuthServiceConstants.DEFAULT_RESEND_COOLDOWN_SECONDS);

        public static string ResolveConnectionString(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                var ex = new ConfigurationErrorsException("Missing connection string name.");
                throw ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_CONFIG,
                    AuthServiceConstants.MESSAGE_CONFIG_ERROR,
                    CTX_GET_CONNECTION,
                    ex);
            }

            ConnectionStringSettings connection = ConfigurationManager.ConnectionStrings[name];

            if (connection == null || string.IsNullOrWhiteSpace(connection.ConnectionString))
            {
                var ex = new ConfigurationErrorsException(
                    string.Format("Missing connection string '{0}'.", name));

                throw ThrowTechnicalFault(
                    AuthServiceConstants.ERROR_CONFIG,
                    AuthServiceConstants.MESSAGE_CONFIG_ERROR,
                    CTX_GET_CONNECTION,
                    ex);
            }

            return connection.ConnectionString;
        }

        public static AuthToken IssueToken(int userId)
        {
            if (userId < USER_ID_MIN_VALUE)
            {
                throw ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    AuthServiceConstants.MESSAGE_INVALID_USER_ID);
            }

            if (TokenStore.ActiveTokenByUserId.TryGetValue(userId, out string activeTokenValue) &&
                !string.IsNullOrWhiteSpace(activeTokenValue))
            {
                if (!TokenStore.Cache.TryGetValue(activeTokenValue, out AuthToken activeToken) || activeToken == null)
                {
                    TokenStore.ActiveTokenByUserId.TryRemove(userId, out _);
                }
                else if (activeToken.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    TokenStore.Cache.TryRemove(activeTokenValue, out _);
                    TokenStore.ActiveTokenByUserId.TryRemove(userId, out _);
                }
                else
                {
                    throw ThrowFault(
                        AuthServiceConstants.ERROR_ALREADY_LOGGED_IN,
                        AuthServiceConstants.MESSAGE_ALREADY_LOGGED_IN);
                }
            }

            for (int attempt = 0; attempt < TOKEN_GENERATION_MAX_ATTEMPTS; attempt++)
            {
                AuthToken token = CreateNewToken(userId);

                if (!TokenStore.Cache.TryAdd(token.Token, token))
                {
                    continue;
                }

                if (!TokenStore.ActiveTokenByUserId.TryAdd(userId, token.Token))
                {
                    TokenStore.Cache.TryRemove(token.Token, out _);

                    throw ThrowFault(
                        AuthServiceConstants.ERROR_ALREADY_LOGGED_IN,
                        AuthServiceConstants.MESSAGE_ALREADY_LOGGED_IN);
                }

                return token;
            }

            throw ThrowTechnicalFault(
                AuthServiceConstants.ERROR_UNEXPECTED,
                AuthServiceConstants.MESSAGE_UNEXPECTED_ERROR,
                CTX_ISSUE_TOKEN,
                new InvalidOperationException("Failed to generate a unique token after max attempts."));
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
            string safeContext = string.IsNullOrWhiteSpace(context)
                ? "AuthServiceContext.ThrowTechnicalFault"
                : context;

            Logger.Error(safeContext, ex);

            var fault = new ServiceFault
            {
                Code = technicalCode,
                Message = userMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }

        internal static Exception CreateSqlTechnicalFault(
            string technicalErrorCode,
            string messageKey,
            string context,
            System.Data.SqlClient.SqlException ex)
        {
            return ThrowTechnicalFault(technicalErrorCode, messageKey, context, ex);
        }

        private static AuthToken CreateNewToken(int userId)
        {
            string tokenValue = Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);
            DateTime expiresAtUtc = DateTime.UtcNow.AddHours(AuthServiceConstants.TOKEN_TTL_HOURS);

            return new AuthToken
            {
                UserId = userId,
                Token = tokenValue,
                ExpiresAtUtc = expiresAtUtc
            };
        }

        private static int ParseIntAppSetting(string key, int @default)
        {
            string rawValue = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return @default;
            }

            string trimmed = rawValue.Trim();

            return int.TryParse(trimmed, out int value)
                ? value
                : @default;
        }
    }
}
