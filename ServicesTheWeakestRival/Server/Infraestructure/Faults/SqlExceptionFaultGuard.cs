using System;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Infrastructure.Faults
{
    internal static class SqlExceptionFaultGuard
    {
        private const string CONTEXT_DETAILS_SEPARATOR = " | ";

        internal static TResult Execute<TResult>(
            Func<TResult> operation,
            string operationKeyPrefix,
            string technicalErrorCode,
            string context,
            Func<string, string, string, SqlException, Exception> technicalFaultFactory)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (technicalFaultFactory == null) throw new ArgumentNullException(nameof(technicalFaultFactory));

            try
            {
                return operation();
            }
            catch (SqlException ex)
            {
                SqlFaultMapping mapping = SqlExceptionFaultMapper.Map(ex, operationKeyPrefix);

                string safeContext = context ?? string.Empty;
                string contextWithDetails = BuildContextWithDetails(safeContext, mapping.Details);

                throw technicalFaultFactory(
                    technicalErrorCode,
                    mapping.MessageKey,          
                    contextWithDetails,          
                    ex);
            }
        }

        internal static void Execute(
            Action operation,
            string operationKeyPrefix,
            string technicalErrorCode,
            string context,
            Func<string, string, string, SqlException, Exception> technicalFaultFactory)
        {
            Execute(() =>
            {
                operation();
                return 0;
            }, operationKeyPrefix, technicalErrorCode, context, technicalFaultFactory);
        }

        private static string BuildContextWithDetails(string context, string details)
        {
            if (string.IsNullOrWhiteSpace(details))
            {
                return context ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(context))
            {
                return details;
            }

            return context + CONTEXT_DETAILS_SEPARATOR + details;
        }
    }
}
