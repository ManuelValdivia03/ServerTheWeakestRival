using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using System;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ForcedLogoutNotifier
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ForcedLogoutNotifier));

        private const string ContextSend = "ForcedLogoutNotifier.TrySendForcedLogoutToAccount";

        internal void TrySendForcedLogoutToAccount(int accountId, ForcedLogoutNotification notification)
        {
            if (accountId <= 0 || notification == null)
            {
                return;
            }

            if (!LobbyCallbackRegistry.TryGet(accountId, out var callback) || callback == null)
            {
                return;
            }

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

                if (channelObject != null)
                {
                    try
                    {
                        channelObject.Abort();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ContextSend, ex);
                    }
                }
            }
        }
    }
}
