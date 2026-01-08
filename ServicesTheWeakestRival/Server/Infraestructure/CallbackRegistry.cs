using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using System;
using System.Collections.Concurrent;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Infrastructure
{
    internal static class LobbyCallbackRegistry
    {
        private sealed class CallbackEntry
        {
            public CallbackEntry(int accountId, string entryId, ILobbyClientCallback callback)
            {
                AccountId = accountId;
                EntryId = entryId ?? string.Empty;
                Callback = callback;
                ChannelObject = callback as ICommunicationObject;
            }

            public int AccountId { get; }
            public string EntryId { get; }
            public ILobbyClientCallback Callback { get; }
            public ICommunicationObject ChannelObject { get; }
        }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyCallbackRegistry));

        private static readonly ConcurrentDictionary<int, CallbackEntry> EntriesByAccountId =
            new ConcurrentDictionary<int, CallbackEntry>();

        private static readonly object SyncRoot = new object();

        private const string ENTRY_ID_FORMAT_NO_HYPHENS = "N";

        public static void Upsert(int accountId, ILobbyClientCallback callback)
        {
            if (accountId <= 0 || callback == null)
            {
                return;
            }

            var channelObject = callback as ICommunicationObject;
            if (channelObject == null)
            {
                Logger.WarnFormat("LobbyCallbackRegistry.Upsert: callback does not implement ICommunicationObject. AccountId={0}", accountId);
                return;
            }

            var entryId = Guid.NewGuid().ToString(ENTRY_ID_FORMAT_NO_HYPHENS);
            var entry = new CallbackEntry(accountId, entryId, callback);

            lock (SyncRoot)
            {
                EntriesByAccountId[accountId] = entry;

                try
                {
                    channelObject.Closed += (_, __) => RemoveIfMatches(accountId, entryId, "Closed");
                    channelObject.Faulted += (_, __) => RemoveIfMatches(accountId, entryId, "Faulted");
                }
                catch (Exception ex)
                {
                    Logger.Warn("LobbyCallbackRegistry.Upsert: error attaching Closed/Faulted handlers.", ex);
                }

                if (!IsChannelAlive(channelObject))
                {
                    RemoveIfMatches(accountId, entryId, "Upsert_NotAlive");
                }
            }
        }

        public static void Remove(int accountId)
        {
            CallbackEntry _;
            EntriesByAccountId.TryRemove(accountId, out _);
        }

        public static bool TryGet(int accountId, out ILobbyClientCallback callback)
        {
            callback = null;

            if (accountId <= 0)
            {
                return false;
            }

            if (!EntriesByAccountId.TryGetValue(accountId, out var entry) || entry == null)
            {
                return false;
            }

            if (entry.ChannelObject == null || !IsChannelAlive(entry.ChannelObject))
            {
                RemoveIfMatches(accountId, entry.EntryId, "TryGet_NotAlive");
                return false;
            }

            callback = entry.Callback;
            return callback != null;
        }

        public static bool TrySendForcedLogout(int accountId, ForcedLogoutNotification notification)
        {
            if (!TryGet(accountId, out var callback))
            {
                return false;
            }

            try
            {
                callback.ForcedLogout(notification);
                return true;
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("LobbyCallbackRegistry.TrySendForcedLogout: callback failed. AccountId={0}", accountId);
                Logger.Warn("LobbyCallbackRegistry.TrySendForcedLogout: exception.", ex);

                Remove(accountId);
                return false;
            }
        }

        private static void RemoveIfMatches(int accountId, string entryId, string reason)
        {
            if (accountId <= 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (!EntriesByAccountId.TryGetValue(accountId, out var current) || current == null)
                {
                    return;
                }

                if (!string.Equals(current.EntryId, entryId, StringComparison.Ordinal))
                {
                    return;
                }

                CallbackEntry _;
                EntriesByAccountId.TryRemove(accountId, out _);

                Logger.DebugFormat(
                    "LobbyCallbackRegistry.RemoveIfMatches: removed. AccountId={0}, Reason={1}",
                    accountId,
                    reason);
            }
        }

        private static bool IsChannelAlive(ICommunicationObject channelObject)
        {
            if (channelObject == null)
            {
                return false;
            }

            var state = channelObject.State;

            return state == CommunicationState.Opened
                || state == CommunicationState.Opening;
        }
    }
}
