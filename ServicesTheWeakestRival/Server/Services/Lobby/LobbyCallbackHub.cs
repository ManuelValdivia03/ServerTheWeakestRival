using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public sealed class LobbyCallbackHub
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyCallbackHub));

        private static readonly LobbyCallbackHub shared = new LobbyCallbackHub();

        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, ILobbyClientCallback>> callbackBuckets =
            new ConcurrentDictionary<Guid, ConcurrentDictionary<string, ILobbyClientCallback>>();

        private readonly ConcurrentDictionary<int, Guid> accountLobbyUids =
            new ConcurrentDictionary<int, Guid>();

        private readonly ConcurrentDictionary<int, string> accountSessionIds =
            new ConcurrentDictionary<int, string>();

        private LobbyCallbackHub()
        {
        }

        public static LobbyCallbackHub Shared => shared;

        public string GetCurrentSessionId()
        {
            return OperationContext.Current != null
                ? OperationContext.Current.SessionId
                : Guid.NewGuid().ToString(LobbyServiceConstants.GUID_NO_DASHES_FORMAT);
        }

        public void AddCallback(Guid lobbyUid, int accountId, ILobbyClientCallback callback)
        {
            ConcurrentDictionary<string, ILobbyClientCallback> bucket =
                callbackBuckets.GetOrAdd(lobbyUid, _ => new ConcurrentDictionary<string, ILobbyClientCallback>());

            string sessionId = GetCurrentSessionId();

            bucket[sessionId] = callback;

            if (accountId > 0)
            {
                accountLobbyUids[accountId] = lobbyUid;
                accountSessionIds[accountId] = sessionId;
            }

            Logger.DebugFormat(
                "AddCallback: LobbyUid={0}, AccountId={1}, SessionId={2}, BucketCount={3}",
                lobbyUid,
                accountId,
                sessionId,
                bucket.Count);
        }

        public void RemoveCallback(Guid lobbyUid, int accountId)
        {
            if (!callbackBuckets.TryGetValue(lobbyUid, out ConcurrentDictionary<string, ILobbyClientCallback> bucket))
            {
                CleanupAccount(accountId);
                return;
            }

            string sessionId = null;

            if (accountId > 0)
            {
                accountSessionIds.TryGetValue(accountId, out sessionId);
            }

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = GetCurrentSessionId();
            }

            bucket.TryRemove(sessionId, out _);

            if (bucket.Count == 0)
            {
                callbackBuckets.TryRemove(lobbyUid, out _);
            }

            CleanupAccount(accountId);

            Logger.DebugFormat(
                "RemoveCallback: LobbyUid={0}, AccountId={1}, SessionId={2}, RemainingInBucket={3}",
                lobbyUid,
                accountId,
                sessionId,
                bucket.Count);
        }

        public void CleanupAccount(int accountId)
        {
            if (accountId <= 0)
            {
                return;
            }

            accountLobbyUids.TryRemove(accountId, out _);
            accountSessionIds.TryRemove(accountId, out _);
        }

        public void Broadcast(Guid lobbyUid, Action<ILobbyClientCallback> send)
        {
            if (!callbackBuckets.TryGetValue(lobbyUid, out ConcurrentDictionary<string, ILobbyClientCallback> bucket))
            {
                Logger.DebugFormat("Broadcast: no callbacks for LobbyUid={0}", lobbyUid);
                return;
            }

            foreach (KeyValuePair<string, ILobbyClientCallback> kv in bucket)
            {
                try
                {
                    send(kv.Value);
                }
                catch (Exception ex)
                {
                    Logger.Warn(
                        string.Format(
                            "Broadcast: callback failed. LobbyUid={0}, SessionId={1}",
                            lobbyUid,
                            kv.Key),
                        ex);
                }
            }
        }

        public bool TryGetLobbyUidForCurrentSession(out Guid lobbyUid)
        {
            string sessionId = GetCurrentSessionId();

            foreach (KeyValuePair<Guid, ConcurrentDictionary<string, ILobbyClientCallback>> kv in callbackBuckets)
            {
                if (kv.Value.ContainsKey(sessionId))
                {
                    lobbyUid = kv.Key;
                    return true;
                }
            }

            lobbyUid = Guid.Empty;
            return false;
        }

        public bool TryGetLobbyUidForAccount(int accountId, out Guid lobbyUid)
        {
            if (accountLobbyUids.TryGetValue(accountId, out lobbyUid))
            {
                return lobbyUid != Guid.Empty;
            }

            lobbyUid = Guid.Empty;
            return false;
        }

        public void TryRefreshLobbyCallbackRegistry(int userId)
        {
            try
            {
                if (OperationContext.Current == null)
                {
                    return;
                }

                ILobbyClientCallback callback =
                    OperationContext.Current.GetCallbackChannel<ILobbyClientCallback>();

                if (callback != null)
                {
                    LobbyCallbackRegistry.Upsert(userId, callback);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("TryRefreshLobbyCallbackRegistry: could not refresh.", ex);
            }
        }

        public void TrySendForcedLogout(int accountId, byte sanctionType, DateTime? sanctionEndAtUtc)
        {
            try
            {
                if (!LobbyCallbackRegistry.TryGet(accountId, out ILobbyClientCallback callback) || callback == null)
                {
                    return;
                }

                var notification = new ForcedLogoutNotification
                {
                    Code = LobbyServiceConstants.FORCED_LOGOUT_CODE_SANCTION,
                    SanctionType = sanctionType,
                    SanctionEndAtUtc = sanctionEndAtUtc
                };

                callback.ForcedLogout(notification);
            }
            catch (Exception ex)
            {
                Logger.Warn("TrySendForcedLogout: callback failed.", ex);
            }
        }
    }
}
