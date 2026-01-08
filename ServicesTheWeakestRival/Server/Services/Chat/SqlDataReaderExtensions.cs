using System;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Chat
{
    public static class SqlDataReaderExtensions
    {
        public static int GetInt32OrDefault(this SqlDataReader reader, int ordinal, int defaultValue)
        {
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal);
        }

        public static string GetStringOrDefault(this SqlDataReader reader, int ordinal, string defaultValue)
        {
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetString(ordinal);
        }

        public static DateTime GetDateTimeOrDefault(this SqlDataReader reader, int ordinal, DateTime defaultValue)
        {
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetDateTime(ordinal);
        }
    }
}
