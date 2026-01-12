using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Data;
using System.Data.SqlClient;

namespace ServerTheWeakestRival.Tests.Integration.Helpers
{
    internal static class TestAccountFactory
    {
        private const string ParamEmail = "@email";
        private const string ParamPasswordHash = "@password_hash";
        private const string ParamStatus = "@status";
        private const string ParamCreatedAt = "@created_at";
        private const string ParamUserId = "@user_id";
        private const string ParamDisplayName = "@display_name";

        private const int EmailMaxLength = 320;
        private const int PasswordHashMaxLength = 128;
        private const int DisplayNameMaxLength = 80;

        private const int AccountStatusActive = 1;

        private const int InvalidId = 0;
        private const int ExpectedInsertRows = 1;

        private const int DefaultCommandTimeoutSeconds = 30;

        private const string PasswordHashDummy = "DUMMY_HASH_FOR_TESTS_ONLY";

        private const string SqlInsertAccount = @"
            INSERT INTO dbo.Accounts (email, password_hash, status, created_at, suspended_until_utc)
            VALUES (@email, @password_hash, @status, @created_at, NULL);
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        private const string SqlInsertUser = @"
            INSERT INTO dbo.Users (user_id, display_name, created_at)
            VALUES (@user_id, @display_name, @created_at);";

        internal static int CreateAccountAndUser(string connectionString, string displayName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Assert.Fail("connectionString must be provided.");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                Assert.Fail("displayName must be provided.");
            }

            string email = BuildUniqueEmail();
            DateTime utcNow = DateTime.UtcNow;

            using (var sqlConnection = new SqlConnection(connectionString))
            using (var insertAccount = new SqlCommand(SqlInsertAccount, sqlConnection))
            using (var insertUser = new SqlCommand(SqlInsertUser, sqlConnection))
            {
                insertAccount.CommandType = CommandType.Text;
                insertUser.CommandType = CommandType.Text;

                insertAccount.CommandTimeout = DefaultCommandTimeoutSeconds;
                insertUser.CommandTimeout = DefaultCommandTimeoutSeconds;

                insertAccount.Parameters.Add(ParamEmail, SqlDbType.NVarChar, EmailMaxLength).Value = email;
                insertAccount.Parameters.Add(ParamPasswordHash, SqlDbType.NVarChar, PasswordHashMaxLength).Value = PasswordHashDummy;
                insertAccount.Parameters.Add(ParamStatus, SqlDbType.TinyInt).Value = AccountStatusActive;
                insertAccount.Parameters.Add(ParamCreatedAt, SqlDbType.DateTime2).Value = utcNow;

                sqlConnection.Open();

                object accountIdObj = insertAccount.ExecuteScalar();
                if (accountIdObj == null || accountIdObj == DBNull.Value)
                {
                    Assert.Fail("CreateAccount did not return an id.");
                }

                int accountId = Convert.ToInt32(accountIdObj);
                if (accountId <= InvalidId)
                {
                    Assert.Fail(string.Format("Invalid accountId='{0}'.", accountId));
                }

                insertUser.Parameters.Add(ParamUserId, SqlDbType.Int).Value = accountId;
                insertUser.Parameters.Add(ParamDisplayName, SqlDbType.NVarChar, DisplayNameMaxLength).Value = displayName.Trim();
                insertUser.Parameters.Add(ParamCreatedAt, SqlDbType.DateTime2).Value = utcNow;

                int affectedRows = insertUser.ExecuteNonQuery();
                if (affectedRows != ExpectedInsertRows)
                {
                    Assert.Fail(string.Format("CreateUser affectedRows='{0}'.", affectedRows));
                }

                return accountId;
            }
        }

        private static string BuildUniqueEmail()
        {
            return "test_" + Guid.NewGuid().ToString("N") + "@example.local";
        }
    }
}
