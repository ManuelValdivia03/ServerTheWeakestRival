using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using System;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplayFaults
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayFaults));

        internal static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat("Service fault. Code='{0}', Message='{1}'", code, message);

            ServiceFault fault = new ServiceFault
            {
                Code = code ?? string.Empty,
                Message = message ?? string.Empty
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message ?? string.Empty));
        }

        internal static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            ServiceFault fault = new ServiceFault
            {
                Code = code ?? string.Empty,
                Message = userMessage ?? string.Empty
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage ?? string.Empty));
        }
    }
}
