using log4net;
using ServicesTheWeakestRival.Contracts.Services;
using System;
using System.Collections.Concurrent;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Infrastructure
{
    internal static class LobbyBroadcastHub
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyBroadcastHub));

        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, ILobbyClientCallback>> Buckets =
            new ConcurrentDictionary<Guid, ConcurrentDictionary<string, ILobbyClientCallback>>();

        private const string SESSION_ID_FORMAT_NO_HYPHENS = "N";

        internal static string CurrentSessionId =>
            OperationContext.Current != null
                ? OperationContext.Current.SessionId
                : Guid.NewGuid().ToString(SESSION_ID_FORMAT_NO_HYPHENS);

        internal static void Register(Guid lobbyUid, ILobbyClientCallback callback)
        {
            if (lobbyUid == Guid.Empty || callback == null)
            {
                return;
            }

            var bucket = Buckets.GetOrAdd(
                lobbyUid,
                _ => new ConcurrentDictionary<string, ILobbyClientCallback>());

            bucket[CurrentSessionId] = callback;
        }

        internal static void Unregister(Guid lobbyUid)
        {
            if (lobbyUid == Guid.Empty)
            {
                return;
            }

            if (!Buckets.TryGetValue(lobbyUid, out var bucket))
            {
                return;
            }

            bucket.TryRemove(CurrentSessionId, out _);

            if (bucket.IsEmpty)
            {
                Buckets.TryRemove(lobbyUid, out _);
            }
        }

        internal static void Broadcast(Guid lobbyUid, Action<ILobbyClientCallback> send)
        {
            if (lobbyUid == Guid.Empty || send == null)
            {
                return;
            }

            if (!Buckets.TryGetValue(lobbyUid, out var bucket))
            {
                return;
            }

            foreach (var kv in bucket)
            {
                try
                {
                    send(kv.Value);
                }
                catch (Exception ex)
                {
                    bucket.TryRemove(kv.Key, out _);

                    Logger.WarnFormat(
                        "LobbyBroadcastHub.Broadcast: callback failed. LobbyUid={0}, SessionId={1}",
                        lobbyUid,
                        kv.Key);

                    Logger.Warn("LobbyBroadcastHub.Broadcast exception.", ex);
                }
            }
        }

        internal static bool TryGetLobbyUidForCurrentSession(out Guid lobbyUid)
        {
            foreach (var kv in Buckets)
            {
                if (kv.Value.ContainsKey(CurrentSessionId))
                {
                    lobbyUid = kv.Key;
                    return true;
                }
            }

            lobbyUid = Guid.Empty;
            return false;
        }
    }
}
