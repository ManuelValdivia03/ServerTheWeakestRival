using System;
using System.Reflection;
using ServicesTheWeakestRival.Server.Services;

namespace ServerTheWeakestRival.Tests.Unit.Infrastructure
{
    internal static class TokenStoreTestCleaner
    {
        private const string TOKEN_STORE_TYPE_NAME = "ServicesTheWeakestRival.Server.Services.TokenStore";
        private const string TOKEN_CACHE_FIELD_NAME = "Cache";
        private const string CLEAR_METHOD_NAME = "Clear";

        internal static void ClearAllTokens()
        {
            Assembly serverAssembly = typeof(AuthService).Assembly;

            Type tokenStoreType = serverAssembly.GetType(TOKEN_STORE_TYPE_NAME, throwOnError: false);
            if (tokenStoreType == null)
            {
                return;
            }

            FieldInfo cacheField = tokenStoreType.GetField(
                TOKEN_CACHE_FIELD_NAME,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            object cacheInstance = cacheField?.GetValue(null);
            if (cacheInstance == null)
            {
                return;
            }

            MethodInfo clearMethod = cacheInstance.GetType().GetMethod(
                CLEAR_METHOD_NAME,
                BindingFlags.Instance | BindingFlags.Public);

            clearMethod?.Invoke(cacheInstance, parameters: null);
        }
    }
}
