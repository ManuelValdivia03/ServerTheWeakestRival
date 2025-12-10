using System;
using System.Collections.Concurrent;

using ContractsToken = ServicesTheWeakestRival.Contracts.Data.AuthToken;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class TokenStore
    {
        internal static readonly ConcurrentDictionary<string, ContractsToken> Cache =
            new ConcurrentDictionary<string, ContractsToken>(StringComparer.Ordinal);

        internal static bool TryGetUserId(string token, out int userId)
        {
            userId = 0;
            if (string.IsNullOrWhiteSpace(token)) return false;
            if (!Cache.TryGetValue(token, out var t)) return false;
            if (t.ExpiresAtUtc <= DateTime.UtcNow) { Cache.TryRemove(token, out _); return false; }
            userId = t.UserId;
            return true;
        }
    }
}
