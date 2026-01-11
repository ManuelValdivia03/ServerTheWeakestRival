using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using System;
using System.Data.SqlClient;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal static class ReportFaultFactory
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ReportFaultFactory));

        internal static FaultException<ServiceFault> Create(string faultCode, string messageKey)
        {
            var fault = new ServiceFault
            {
                Code = faultCode ?? string.Empty,
                Message = messageKey ?? string.Empty,
                Details = string.Empty
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(fault.Message));
        }

        internal static Exception CreateTechnicalFault(
            string technicalErrorCode,
            string messageKey,
            string context,
            SqlException ex)
        {
            Logger.Error(context ?? string.Empty, ex);

            var fault = new ServiceFault
            {
                Code = technicalErrorCode ?? string.Empty,
                Message = messageKey ?? string.Empty,  // KEY para Lang
                Details = context ?? string.Empty
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(fault.Message));
        }
    }
}
