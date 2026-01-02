using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal static class ReportServiceContext
    {
        private const string FaultRequestNull = "REPORT_REQUEST_NULL";
        private const string FaultTokenInvalid = "REPORT_TOKEN_INVALID";

        private static ConcurrentDictionary<string, AuthToken> TokenCache
        {
            get { return TokenStore.Cache; }
        }

        internal static void ValidateRequest(object request)
        {
            if (request == null)
            {
                throw ThrowFault(FaultRequestNull, "La solicitud no puede ser null.");
            }
        }

        internal static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ThrowFault(FaultTokenInvalid, "Sesión inválida.");
            }

            AuthToken authToken;
            bool found = TokenCache.TryGetValue(token, out authToken);

            if (!found || authToken == null)
            {
                throw ThrowFault(FaultTokenInvalid, "Sesión inválida.");
            }

            return authToken.UserId;
        }

        internal static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            var fault = new ServiceFault
            {
                Code = code ?? string.Empty,
                Message = message ?? string.Empty
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(fault.Message));
        }
    }
}
