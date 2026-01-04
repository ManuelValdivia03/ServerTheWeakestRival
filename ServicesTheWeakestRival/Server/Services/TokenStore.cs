using log4net;
using System;
using System.Collections.Concurrent;

using ContractsToken = ServicesTheWeakestRival.Contracts.Data.AuthToken;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class TokenStore
    {
        private const string CONTEXT_REVOKE_ALL_FOR_USER = "TokenStore.RevokeAllForUser";

        private static readonly ILog _logger = LogManager.GetLogger(typeof(TokenStore));

        internal static readonly ConcurrentDictionary<string, ContractsToken> Cache =
            new ConcurrentDictionary<string, ContractsToken>(StringComparer.Ordinal);

        internal static bool TryGetUserId(string token, out int userId)
        {
            userId = 0;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (!Cache.TryGetValue(token, out var t))
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

        internal static int RevokeAllForUser(int userId)
        {
            var revokedCount = 0;
            var canProcess = userId > 0;

            if (canProcess)
            {
                try
                {
                    foreach (var kv in Cache)
                    {
                        var tokenValue = kv.Value;
                        if (tokenValue != null && tokenValue.UserId == userId)
                        {
                            if (Cache.TryRemove(kv.Key, out _))
                            {
                                revokedCount++;
                            }
                        }
                    }

                    _logger.InfoFormat(
                        "RevokeAllForUser: UserId={0}, RevokedCount={1}",
                        userId,
                        revokedCount);
                }
                catch (Exception ex)
                {
                    _logger.Error(CONTEXT_REVOKE_ALL_FOR_USER, ex);
                }
            }

            return revokedCount;
        }
    }
}
