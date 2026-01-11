using log4net;
using System;
using System.Collections.Concurrent;
using System.Linq;

using ContractsToken = ServicesTheWeakestRival.Contracts.Data.AuthToken;

namespace ServicesTheWeakestRival.Server.Services
{
    public static class TokenStore
    {
        private const string CONTEXT_REVOKE_ALL_FOR_USER = "TokenStore.RevokeAllForUser";
        private const string CONTEXT_SESSIONS_REVOKED_EVENT = "TokenStore.SessionsRevokedForUser";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(TokenStore));
        private static readonly object SyncRoot = new object();

        public static ConcurrentDictionary<string, ContractsToken> Cache { get; } =
            new ConcurrentDictionary<string, ContractsToken>(StringComparer.Ordinal);

        public static ConcurrentDictionary<int, string> ActiveTokenByUserId { get; } =
            new ConcurrentDictionary<int, string>();

        internal static event Action<int> SessionsRevokedForUser;

        internal static bool TryAddToken(ContractsToken token)
        {
            if (token == null || string.IsNullOrWhiteSpace(token.Token) || token.UserId <= 0)
            {
                return false;
            }

            StoreToken(token);
            return true;
        }

        public static void StoreToken(ContractsToken token)
        {
            if (token == null || string.IsNullOrWhiteSpace(token.Token) || token.UserId <= 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (ActiveTokenByUserId.TryGetValue(token.UserId, out string previousTokenValue)
                    && !string.IsNullOrWhiteSpace(previousTokenValue)
                    && !string.Equals(previousTokenValue, token.Token, StringComparison.Ordinal))
                {
                    Cache.TryRemove(previousTokenValue, out _);
                }

                Cache[token.Token] = token;
                ActiveTokenByUserId[token.UserId] = token.Token;
            }
        }

        public static bool TryGetUserId(string tokenValue, out int userId)
        {
            userId = 0;

            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                return false;
            }

            if (!Cache.TryGetValue(tokenValue, out ContractsToken token) || token == null)
            {
                return false;
            }

            if (IsExpired(token))
            {
                TryRemoveToken(tokenValue, out _);
                return false;
            }

            if (ActiveTokenByUserId.TryGetValue(token.UserId, out string currentTokenValue)
                && !string.Equals(currentTokenValue, tokenValue, StringComparison.Ordinal))
            {
                Cache.TryRemove(tokenValue, out _);
                return false;
            }

            userId = token.UserId;
            return true;
        }

        public static bool TryGetActiveTokenForUser(int userId, out ContractsToken activeToken)
        {
            activeToken = null;

            if (userId <= 0)
            {
                return false;
            }

            if (!ActiveTokenByUserId.TryGetValue(userId, out string tokenValue)
                || string.IsNullOrWhiteSpace(tokenValue))
            {
                return false;
            }

            if (!Cache.TryGetValue(tokenValue, out ContractsToken token) || token == null)
            {
                ActiveTokenByUserId.TryRemove(userId, out _);
                return false;
            }

            if (IsExpired(token))
            {
                TryRemoveToken(tokenValue, out _);
                return false;
            }

            activeToken = token;
            return true;
        }

        public static bool TryRemoveToken(string tokenValue, out ContractsToken removedToken)
        {
            removedToken = null;

            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (!Cache.TryRemove(tokenValue, out ContractsToken removed) || removed == null)
                {
                    return false;
                }

                removedToken = removed;

                if (ActiveTokenByUserId.TryGetValue(removed.UserId, out string mappedTokenValue)
                    && string.Equals(mappedTokenValue, tokenValue, StringComparison.Ordinal))
                {
                    ActiveTokenByUserId.TryRemove(removed.UserId, out _);
                }

                return true;
            }
        }

        public static int RevokeAllForUser(int userId)
        {
            int revokedCount = 0;

            if (userId <= 0)
            {
                return revokedCount;
            }

            try
            {
                lock (SyncRoot)
                {
                    foreach (var kvp in Cache.ToArray())
                    {
                        ContractsToken token = kvp.Value;

                        if (token != null
                            && token.UserId == userId
                            && Cache.TryRemove(kvp.Key, out _))
                        {
                            revokedCount++;
                        }
                    }

                    ActiveTokenByUserId.TryRemove(userId, out _);
                }

                Logger.InfoFormat(
                    "RevokeAllForUser: UserId={0}, RevokedCount={1}",
                    userId,
                    revokedCount);
            }
            catch (Exception ex)
            {
                Logger.Error(CONTEXT_REVOKE_ALL_FOR_USER, ex);
                return revokedCount;
            }

            if (revokedCount > 0)
            {
                try
                {
                    SessionsRevokedForUser?.Invoke(userId);
                }
                catch (Exception ex)
                {
                    Logger.Error(CONTEXT_SESSIONS_REVOKED_EVENT, ex);
                }
            }

            return revokedCount;
        }

        public static bool IsExpired(ContractsToken token)
        {
            if (token == null)
            {
                return true;
            }

            return token.ExpiresAtUtc <= DateTime.UtcNow;
        }
    }
}
