using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using System;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public static class LobbyServiceContext
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyServiceContext));

        public static void ValidateRequest(object request)
        {
            if (request == null)
            {
                throw ThrowFault(LobbyServiceConstants.ERROR_INVALID_REQUEST, "Request nulo.");
            }
        }

        public static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ThrowFault(LobbyServiceConstants.ERROR_UNAUTHORIZED, "Token inválido o expirado.");
            }

            if (!TokenStore.TryGetUserId(token, out int userId) || userId <= 0)
            {
                throw ThrowFault(LobbyServiceConstants.ERROR_UNAUTHORIZED, "Token inválido o expirado.");
            }

            _ = TryRegisterLobbyCallback(userId);

            return userId;
        }

        public static ILobbyClientCallback GetCallbackChannel()
        {
            OperationContext context = OperationContext.Current;
            if (context == null)
            {
                throw ThrowFault(LobbyServiceConstants.ERROR_UNAUTHORIZED, "No hay contexto de comunicación.");
            }

            ILobbyClientCallback callback = context.GetCallbackChannel<ILobbyClientCallback>();
            if (callback == null)
            {
                throw ThrowFault(LobbyServiceConstants.ERROR_UNAUTHORIZED, "No hay canal de callback.");
            }

            return callback;
        }

        public static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat("Lobby fault. Code='{0}', Message='{1}'", code, message);

            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        public static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            var fault = new ServiceFault
            {
                Code = code,
                Message = userMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }

        private static bool TryRegisterLobbyCallback(int accountId)
        {
            if (accountId <= 0)
            {
                return false;
            }

            try
            {
                OperationContext context = OperationContext.Current;
                if (context == null)
                {
                    Logger.WarnFormat(
                        "{0}: OperationContext.Current is null.",
                        LobbyServiceConstants.CTX_REGISTER_CALLBACK);

                    return false;
                }

                ILobbyClientCallback callback = context.GetCallbackChannel<ILobbyClientCallback>();
                if (callback == null)
                {
                    Logger.WarnFormat(
                        "{0}: callback channel is null. AccountId={1}",
                        LobbyServiceConstants.CTX_REGISTER_CALLBACK,
                        accountId);
                    return false;
                }

                var channelObject = callback as ICommunicationObject;
                if (channelObject == null)
                {
                    Logger.WarnFormat(
                        "{0}: callback does not implement ICommunicationObject. AccountId={1}",
                        LobbyServiceConstants.CTX_REGISTER_CALLBACK,
                        accountId);
                    return false;
                }

                if (channelObject.State == CommunicationState.Faulted || channelObject.State == CommunicationState.Closed)
                {
                    Logger.WarnFormat(
                        "{0}: callback channel not alive. State={1}, AccountId={2}",
                        LobbyServiceConstants.CTX_REGISTER_CALLBACK,
                        channelObject.State,
                        accountId);
                    return false;
                }

                LobbyCallbackRegistry.Upsert(accountId, callback);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(LobbyServiceConstants.CTX_REGISTER_CALLBACK, ex);
                return false;
            }
        }
    }
}
