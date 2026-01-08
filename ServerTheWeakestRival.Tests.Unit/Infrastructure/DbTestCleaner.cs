using System.Data;
using System.Data.SqlClient;

namespace ServerTheWeakestRival.Tests.Unit.Infrastructure
{
    internal static class DbTestCleaner
    {
        private const int RESEED_IDENTITY_TO = 0;

        private const string SQL_DISABLE_CONSTRAINTS = @"
DECLARE @schemaName sysname;
DECLARE @tableName sysname;
DECLARE @sql nvarchar(max);

DECLARE cur CURSOR FAST_FORWARD FOR
SELECT s.name, t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0;

OPEN cur;
FETCH NEXT FROM cur INTO @schemaName, @tableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' NOCHECK CONSTRAINT ALL;';
    EXEC sp_executesql @sql;

    FETCH NEXT FROM cur INTO @schemaName, @tableName;
END

CLOSE cur;
DEALLOCATE cur;
";

        private const string SQL_DELETE_ALL_TABLES = @"
DECLARE @schemaName sysname;
DECLARE @tableName sysname;
DECLARE @sql nvarchar(max);

DECLARE cur CURSOR FAST_FORWARD FOR
SELECT s.name, t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0;

OPEN cur;
FETCH NEXT FROM cur INTO @schemaName, @tableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DELETE FROM ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N';';
    EXEC sp_executesql @sql;

    FETCH NEXT FROM cur INTO @schemaName, @tableName;
END

CLOSE cur;
DEALLOCATE cur;
";

        private const string SQL_RESEED_IDENTITIES_TEMPLATE = @"
DECLARE @schemaName sysname;
DECLARE @tableName sysname;
DECLARE @sql nvarchar(max);

DECLARE cur CURSOR FAST_FORWARD FOR
SELECT s.name, t.name
FROM sys.identity_columns ic
INNER JOIN sys.tables t ON t.object_id = ic.object_id
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
GROUP BY s.name, t.name;

OPEN cur;
FETCH NEXT FROM cur INTO @schemaName, @tableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DBCC CHECKIDENT (''' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N''', RESEED, {0});';
    EXEC sp_executesql @sql;

    FETCH NEXT FROM cur INTO @schemaName, @tableName;
END

CLOSE cur;
DEALLOCATE cur;
";

        private const string SQL_ENABLE_CONSTRAINTS = @"
DECLARE @schemaName sysname;
DECLARE @tableName sysname;
DECLARE @sql nvarchar(max);

DECLARE cur CURSOR FAST_FORWARD FOR
SELECT s.name, t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0;

OPEN cur;
FETCH NEXT FROM cur INTO @schemaName, @tableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' WITH CHECK CHECK CONSTRAINT ALL;';
    EXEC sp_executesql @sql;

    FETCH NEXT FROM cur INTO @schemaName, @tableName;
END

CLOSE cur;
DEALLOCATE cur;
";

        internal static void CleanupAll()
        {
            string reseedSql = string.Format(SQL_RESEED_IDENTITIES_TEMPLATE, RESEED_IDENTITY_TO);

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand())
            {
                cmd.Connection = connection;
                cmd.CommandType = CommandType.Text;

                connection.Open();

                cmd.CommandText = SQL_DISABLE_CONSTRAINTS;
                cmd.ExecuteNonQuery();

                cmd.CommandText = SQL_DELETE_ALL_TABLES;
                cmd.ExecuteNonQuery();

                cmd.CommandText = reseedSql;
                cmd.ExecuteNonQuery();

                cmd.CommandText = SQL_ENABLE_CONSTRAINTS;
                cmd.ExecuteNonQuery();
            }
        }
    }
}
