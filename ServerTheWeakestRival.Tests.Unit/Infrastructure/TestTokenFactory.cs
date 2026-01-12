using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;

namespace ServerTheWeakestRival.Tests.Integration.Helpers
{
    internal static class TestTokenFactory
    {
        private const int TOKEN_TTL_MINUTES = 30;

        private const string PROP_USER_ID = "UserId";
        private const string PROP_EXPIRES_AT_UTC = "ExpiresAtUtc";

        private static readonly string[] TokenPropertyCandidates =
        {
            "Token",
            "Value",
            "TokenValue",
            "AccessToken"
        };

        internal static string StoreValidToken(int userId)
        {
            if (userId <= 0)
            {
                Assert.Fail("userId must be valid.");
            }

            string tokenValue = Guid.NewGuid().ToString("N");
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(TOKEN_TTL_MINUTES);

            object authToken = CreateAuthTokenInstance();
            SetPropertyValue(authToken, PROP_USER_ID, userId);
            SetPropertyValue(authToken, PROP_EXPIRES_AT_UTC, expiresAtUtc);
            SetTokenValue(authToken, tokenValue);

            InvokeTokenStoreStore(authToken);

            return tokenValue;
        }

        private static object CreateAuthTokenInstance()
        {
            Type authTokenType =
                AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("ServicesTheWeakestRival.Contracts.Data.AuthToken", false))
                    .FirstOrDefault(t => t != null);

            if (authTokenType == null)
            {
                Assert.Fail("AuthToken type not found. Expected ServicesTheWeakestRival.Contracts.Data.AuthToken.");
            }

            object instance = Activator.CreateInstance(authTokenType);
            if (instance == null)
            {
                Assert.Fail("Could not create AuthToken instance.");
            }

            return instance;
        }

        private static void InvokeTokenStoreStore(object authToken)
        {
            Type tokenStoreType =
                AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("ServicesTheWeakestRival.Server.Infrastructure.TokenStore", false))
                    .FirstOrDefault(t => t != null);

            if (tokenStoreType == null)
            {
                Assert.Fail("TokenStore type not found. Expected ServicesTheWeakestRival.Server.Infrastructure.TokenStore.");
            }

            MethodInfo storeMethod =
                tokenStoreType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m =>
                        (string.Equals(m.Name, "StoreToken", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(m.Name, "Store", StringComparison.OrdinalIgnoreCase)) &&
                        m.GetParameters().Length == 1);

            if (storeMethod == null)
            {
                Assert.Fail("TokenStore.StoreToken(Store) method not found (expected one-parameter static method).");
            }

            storeMethod.Invoke(null, new[] { authToken });
        }

        private static void SetTokenValue(object authToken, string tokenValue)
        {
            foreach (string propName in TokenPropertyCandidates)
            {
                PropertyInfo p = authToken.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(string) && p.CanWrite)
                {
                    p.SetValue(authToken, tokenValue);
                    return;
                }
            }

            Assert.Fail(
                "AuthToken token string property not found. Tried: " +
                string.Join(", ", TokenPropertyCandidates));
        }

        private static void SetPropertyValue(object target, string propertyName, object value)
        {
            PropertyInfo p = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite)
            {
                Assert.Fail(string.Format("Property '{0}' not found or not writable on {1}.", propertyName, target.GetType().FullName));
            }

            p.SetValue(target, value);
        }
    }
}
