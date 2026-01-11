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

        private const string FORCED_LOGOUT_CODE = "FORCED_LOGOUT";

        private const string REASON_CHANNEL_CLOSED = "Closed";
        private const string REASON_CHANNEL_FAULTED = "Faulted";
        private const string REASON_UPSERT_NOT_ALIVE = "Upsert_NotAlive";
        private const string REASON_TRYGET_NOT_ALIVE = "TryGet_NotAlive";
        private const string REASON_REPLACED_BY_NEW_SESSION = "ReplacedByNewSession";
        private const string REASON_FORCED_LOGOUT = "ForcedLogout";

        public static void Upsert(int accountId, ILobbyClientCallback callback)
        {
            if (accountId <= 0 || callback == null)
            {
                return;
            }

            var channelObject = callback as ICommunicationObject;
            if (channelObject == null)
            {
                Logger.WarnFormat(
                    "LobbyCallbackRegistry.Upsert: callback does not implement ICommunicationObject. AccountId={0}",
                    accountId);
                return;
            }

            var entryId = Guid.NewGuid().ToString(ENTRY_ID_FORMAT_NO_HYPHENS);
            var newEntry = new CallbackEntry(accountId, entryId, callback);

            CallbackEntry oldEntry = null;

            lock (SyncRoot)
            {
                if (EntriesByAccountId.TryGetValue(accountId, out var current) && current != null)
                {
                    oldEntry = current;
                }

                EntriesByAccountId[accountId] = newEntry;

                try
                {
                    channelObject.Closed += (_, __) => RemoveIfMatches(accountId, entryId, REASON_CHANNEL_CLOSED);
                    channelObject.Faulted += (_, __) => RemoveIfMatches(accountId, entryId, REASON_CHANNEL_FAULTED);
                }
                catch (Exception ex)
                {
                    Logger.Warn("LobbyCallbackRegistry.Upsert: error attaching Closed/Faulted handlers.", ex);
                }

                if (!IsChannelAlive(channelObject))
                {
                    RemoveIfMatches(accountId, entryId, REASON_UPSERT_NOT_ALIVE);
                }
            }

            if (oldEntry != null && oldEntry.Callback != null && !ReferenceEquals(oldEntry.Callback, callback))
            {
                TrySendForcedLogoutToEntry(oldEntry, REASON_REPLACED_BY_NEW_SESSION);
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
                RemoveIfMatches(accountId, entry.EntryId, REASON_TRYGET_NOT_ALIVE);
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
                Logger.WarnFormat(
                    "LobbyCallbackRegistry.TrySendForcedLogout: callback failed. AccountId={0}",
                    accountId);
                Logger.Warn("LobbyCallbackRegistry.TrySendForcedLogout: exception.", ex);

                Remove(accountId);
                return false;
            }
        }

        public static bool TryForceLogoutAndRemove(int accountId, string reason)
        {
            if (accountId <= 0)
            {
                return false;
            }

            CallbackEntry entry = null;

            lock (SyncRoot)
            {
                if (!EntriesByAccountId.TryRemove(accountId, out entry))
                {
                    return false;
                }
            }

            TrySendForcedLogoutToEntry(entry, string.IsNullOrWhiteSpace(reason) ? REASON_FORCED_LOGOUT : reason);
            return true;
        }

        private static void TrySendForcedLogoutToEntry(CallbackEntry entry, string reason)
        {
            if (entry == null || entry.Callback == null)
            {
                return;
            }

            try
            {
                var notification = new ForcedLogoutNotification
                {
                    Code = FORCED_LOGOUT_CODE,
                    SanctionEndAtUtc = null,
                    SanctionType = 0
                };

                entry.Callback.ForcedLogout(notification);
            }
            catch (Exception ex)
            {
                Logger.WarnFormat(
                    "LobbyCallbackRegistry.TrySendForcedLogoutToEntry: callback failed. AccountId={0}, Reason={1}",
                    entry.AccountId,
                    reason ?? string.Empty);
                Logger.Warn("LobbyCallbackRegistry.TrySendForcedLogoutToEntry: exception.", ex);
            }
            finally
            {
                CloseChannelSafe(entry.ChannelObject);
            }
        }

        private static void CloseChannelSafe(ICommunicationObject channelObject)
        {
            if (channelObject == null)
            {
                return;
            }

            try
            {
                if (channelObject.State == CommunicationState.Faulted)
                {
                    channelObject.Abort();
                    return;
                }

                channelObject.Close();
            }
            catch (Exception ex)
            {
                try
                {
                    channelObject.Abort();
                }
                catch (Exception abortEx)
                {
                    Logger.Warn(
                        "LobbyCallbackRegistry.CloseChannelSafe: Close failed and Abort failed.",
                        new AggregateException(ex, abortEx));
                }
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
