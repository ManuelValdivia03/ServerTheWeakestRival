using System;
using System.Data.SqlClient;
using System.Text;

namespace ServicesTheWeakestRival.Server.Infrastructure.Faults
{
    internal static class SqlExceptionFaultMapper
    {
        private const string KEY_SQL_SUFFIX = ".Sql.";
        private const string KEY_UNKNOWN_SQL = "Unknown";

        private const char DETAILS_SEPARATOR = ';';

        private const string DETAILS_PREFIX_SQL_NUMBER = "SqlNumber=";
        private const string DETAILS_PREFIX_SQL_STATE = "State=";
        private const string DETAILS_PREFIX_SQL_CLASS = "Class=";
        private const string DETAILS_PREFIX_SQL_PROCEDURE = "Procedure=";
        private const string DETAILS_PREFIX_SQL_LINE = "Line=";

        public static SqlFaultMapping Map(SqlException ex, string operationKeyPrefix)
        {
            string normalizedPrefix = NormalizePrefixOrThrow(operationKeyPrefix);

            if (ex == null)
            {
                return new SqlFaultMapping(
                    BuildUnknownKey(normalizedPrefix),
                    string.Empty);
            }

            return new SqlFaultMapping(
                BuildKey(normalizedPrefix, ex.Number),
                BuildDetails(ex));
        }

        private static string NormalizePrefixOrThrow(string operationKeyPrefix)
        {
            if (string.IsNullOrWhiteSpace(operationKeyPrefix))
            {
                throw new ArgumentException("Operation key prefix is required.", nameof(operationKeyPrefix));
            }

            return operationKeyPrefix.Trim().TrimEnd('.');
        }

        private static string BuildKey(string operationKeyPrefix, int sqlNumber)
        {
            return operationKeyPrefix + KEY_SQL_SUFFIX + sqlNumber;
        }

        private static string BuildUnknownKey(string operationKeyPrefix)
        {
            return operationKeyPrefix + KEY_SQL_SUFFIX + KEY_UNKNOWN_SQL;
        }

        private static string BuildDetails(SqlException ex)
        {
            var sb = new StringBuilder();

            sb.Append(DETAILS_PREFIX_SQL_NUMBER).Append(ex.Number).Append(DETAILS_SEPARATOR)
              .Append(DETAILS_PREFIX_SQL_STATE).Append(ex.State).Append(DETAILS_SEPARATOR)
              .Append(DETAILS_PREFIX_SQL_CLASS).Append(ex.Class).Append(DETAILS_SEPARATOR)
              .Append(DETAILS_PREFIX_SQL_PROCEDURE).Append(ex.Procedure ?? string.Empty).Append(DETAILS_SEPARATOR)
              .Append(DETAILS_PREFIX_SQL_LINE).Append(ex.LineNumber);

            return sb.ToString();
        }
    }

    internal sealed class SqlFaultMapping
    {
        public string MessageKey { get; }
        public string Details { get; }

        public SqlFaultMapping(string messageKey, string details)
        {
            MessageKey = messageKey ?? string.Empty;
            Details = details ?? string.Empty;
        }
    }
}
