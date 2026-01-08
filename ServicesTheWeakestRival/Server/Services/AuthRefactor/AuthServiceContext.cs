using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.ServiceModel;
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public static class AuthServiceContext
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(AuthServiceContext));

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        public static int CodeTtlMinutes =>
            ParseIntAppSetting("EmailCodeTtlMinutes", AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

        public static int ResendCooldownSeconds =>
            ParseIntAppSetting("EmailResendCooldownSeconds", AuthServiceConstants.DEFAULT_RESEND_COOLDOWN_SECONDS);

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
            string tokenValue = Guid.NewGuid().ToString("N");
            DateTime expiresAt = DateTime.UtcNow.AddHours(AuthServiceConstants.TOKEN_TTL_HOURS);

            var token = new AuthToken
            {
                UserId = userId,
                Token = tokenValue,
                ExpiresAtUtc = expiresAt
            };

            TokenCache[tokenValue] = token;
            return token;
        }

        public static bool TryRemoveToken(string tokenValue, out AuthToken token)
        {
            return TokenCache.TryRemove(tokenValue, out token);
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

        public static bool TryGetUserId(string tokenValue, out int userId)
        {
            userId = 0;

            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                return false;
            }

            if (!TokenCache.TryGetValue(tokenValue, out AuthToken token))
            {
                return false;
            }

            if (token.ExpiresAtUtc <= DateTime.UtcNow)
            {
                TokenCache.TryRemove(tokenValue, out _);
                return false;
            }

            userId = token.UserId;
            return true;
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
    }
}
