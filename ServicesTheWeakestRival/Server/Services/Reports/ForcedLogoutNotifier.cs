using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using System;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ForcedLogoutNotifier
    {
        private const int MIN_ACCOUNT_ID = 1;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(ForcedLogoutNotifier));

        private const string ContextSend = "ForcedLogoutNotifier.TrySendForcedLogoutToAccount";
        private const string ContextAbort = "ForcedLogoutNotifier.AbortChannelSafe";

        internal void TrySendForcedLogoutToAccount(int accountId, ForcedLogoutNotification notification)
        {
            if (accountId >= MIN_ACCOUNT_ID && notification != null)
            {
                TrySendForcedLogoutToAccountInternal(accountId, notification);
            }
        }

        private static void TrySendForcedLogoutToAccountInternal(int accountId, ForcedLogoutNotification notification)
        {
            if (LobbyCallbackRegistry.TryGet(accountId, out var callback) && callback != null)
            {
                ICommunicationObject channelObject = callback as ICommunicationObject;

                try
                {
                    callback.ForcedLogout(notification);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ContextSend, ex);
                }
                finally
                {
                    LobbyCallbackRegistry.Remove(accountId);
                    AbortChannelSafe(channelObject);
                }
            }
        }

        private static void AbortChannelSafe(ICommunicationObject channelObject)
        {
            if (channelObject != null)
            {
                try
                {
                    channelObject.Abort();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ContextAbort, ex);
                }
            }
        }
    }
}
