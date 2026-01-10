using log4net;
using System;
using System.Collections.Concurrent;
using System.Linq;
using ContractsToken = ServicesTheWeakestRival.Contracts.Data.AuthToken;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class TokenStore
    {
        private const string CONTEXT_REVOKE_ALL_FOR_USER = "TokenStore.RevokeAllForUser";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(TokenStore));

        internal static event Action<int> SessionsRevokedForUser;

        internal static readonly ConcurrentDictionary<string, ContractsToken> Cache =
            new ConcurrentDictionary<string, ContractsToken>(StringComparer.Ordinal);

        internal static bool TryAddToken(ContractsToken token)
        {
            if (token == null || string.IsNullOrWhiteSpace(token.Token))
            {
                return false;
            }

            Cache[token.Token] = token;
            return true;
        }

        internal static bool TryGetUserId(string token, out int userId)
        {
            userId = 0;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (!Cache.TryGetValue(token, out ContractsToken t))
            {
                return false;
            }

            if (t.ExpiresAtUtc <= DateTime.UtcNow)
            {
                Cache.TryRemove(token, out _);
                return false;
            }

            userId = t.UserId;
            return true;
        }

        internal static bool TryRemoveToken(string tokenValue, out ContractsToken token)
        {
            token = null;

            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                return false;
            }

            return Cache.TryRemove(tokenValue, out token);
        }

        internal static int RevokeAllForUser(int userId)
        {
            if (userId <= 0)
            {
                return 0;
            }

            int removed = 0;

            try
            {
                foreach (var kvp in Cache.ToArray())
                {
                    ContractsToken t = kvp.Value;
                    if (t != null && t.UserId == userId)
                    {
                        if (Cache.TryRemove(kvp.Key, out _))
                        {
                            removed++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CONTEXT_REVOKE_ALL_FOR_USER, ex);
            }

            if (removed > 0)
            {
                try
                {
                    SessionsRevokedForUser?.Invoke(userId);
                }
                catch
                {
                }
            }

            return removed;
        }
    }
}
