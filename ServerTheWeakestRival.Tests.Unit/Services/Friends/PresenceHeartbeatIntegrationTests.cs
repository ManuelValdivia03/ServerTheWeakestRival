using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Integration.Helpers;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Friends;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using System;
using System.Data;
using System.Data.SqlClient;

namespace ServerTheWeakestRival.Tests.Integration.Services.Friends
{
    [TestClass]
    public sealed class PresenceHeartbeatIntegrationTests
    {
        private const string DisplayNameMe = "Me";

        private const int TokenLifetimeMinutes = 10;
        private const string TokenPrefix = "test-token-";

        private const string DeviceValue = "TEST_DEVICE";
        private const string DeviceNull = null;

        private const int ExpectedInsertRows = 1;

        private const string SqlSelectPresence = @"
        SELECT last_seen_utc, device
        FROM dbo.UserPresence
        WHERE user_id = @Id;";

        private const string ParamId = "@Id";

        private string connectionString;

        [TestInitialize]
        public void TestInitialize()
        {
            connectionString = DbTestConfig.GetMainConnectionString();

            DbTestCleaner.CleanupAll();
            TokenStoreCleaner.Clean();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DbTestCleaner.CleanupAll();
            TokenStoreCleaner.Clean();
        }

        [TestMethod]
        public void PresenceHeartbeat_WhenNoRow_InsertsPresence()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            string meToken = StoreTokenForUser(meAccountId);

            FriendPresenceLogic logic = CreateLogic();

            HeartbeatResponse response = logic.PresenceHeartbeat(new HeartbeatRequest
            {
                Token = meToken,
                Device = DeviceValue
            });

            Assert.IsNotNull(response);
            Assert.AreNotEqual(default(DateTime), response.Utc);

            PresenceRow row = ReadPresenceRow(meAccountId);

            Assert.IsTrue(row.IsFound);
            Assert.IsTrue(row.LastSeenUtc.HasValue);
            Assert.AreNotEqual(default(DateTime), row.LastSeenUtc.Value);
            Assert.AreEqual(DeviceValue, row.Device);
        }

        [TestMethod]
        public void PresenceHeartbeat_WhenRowExists_UpdatesPresence()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            string meToken = StoreTokenForUser(meAccountId);

            InsertPresenceRow(meAccountId, DateTime.UtcNow.AddMinutes(-5), "OLD");

            PresenceRow before = ReadPresenceRow(meAccountId);
            Assert.IsTrue(before.IsFound);
            Assert.IsTrue(before.LastSeenUtc.HasValue);

            FriendPresenceLogic logic = CreateLogic();

            HeartbeatResponse response = logic.PresenceHeartbeat(new HeartbeatRequest
            {
                Token = meToken,
                Device = DeviceValue
            });

            Assert.IsNotNull(response);

            PresenceRow after = ReadPresenceRow(meAccountId);

            Assert.IsTrue(after.IsFound);
            Assert.IsTrue(after.LastSeenUtc.HasValue);
            Assert.IsTrue(after.LastSeenUtc.Value >= before.LastSeenUtc.Value);
            Assert.AreEqual(DeviceValue, after.Device);
        }

        [TestMethod]
        public void PresenceHeartbeat_WhenDeviceIsNull_StoresDbNullDevice()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            string meToken = StoreTokenForUser(meAccountId);

            FriendPresenceLogic logic = CreateLogic();

            HeartbeatResponse response = logic.PresenceHeartbeat(new HeartbeatRequest
            {
                Token = meToken,
                Device = DeviceNull
            });

            Assert.IsNotNull(response);

            PresenceRow row = ReadPresenceRow(meAccountId);

            Assert.IsTrue(row.IsFound);
            Assert.IsTrue(row.LastSeenUtc.HasValue);
            Assert.IsNull(row.Device);
        }

        private int CreateAccount(string displayName)
        {
            return TestAccountFactory.CreateAccountAndUser(connectionString, displayName);
        }

        private static FriendPresenceLogic CreateLogic()
        {
            IFriendPresenceRepository repository = new FriendPresenceRepository();
            return new FriendPresenceLogic(repository);
        }

        private static string StoreTokenForUser(int userId)
        {
            string tokenValue = TokenPrefix + Guid.NewGuid().ToString("N");

            TokenStore.StoreToken(new AuthToken
            {
                Token = tokenValue,
                UserId = userId,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(TokenLifetimeMinutes)
            });

            return tokenValue;
        }

        private PresenceRow ReadPresenceRow(int userId)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(SqlSelectPresence, connection))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.Add(ParamId, SqlDbType.Int).Value = userId;

                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return PresenceRow.NotFound();
                    }

                    DateTime? lastSeenUtc = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);
                    string device = reader.IsDBNull(1) ? null : reader.GetString(1);

                    return PresenceRow.Found(lastSeenUtc, device);
                }
            }
        }

        private void InsertPresenceRow(int userId, DateTime lastSeenUtc, string device)
        {
            const string sqlInsert = @"
INSERT INTO dbo.UserPresence (user_id, last_seen_utc, device)
VALUES (@Id, @LastSeen, @Dev);";

            const string paramLastSeen = "@LastSeen";
            const string paramDev = "@Dev";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sqlInsert, connection))
            {
                command.CommandType = CommandType.Text;

                command.Parameters.Add(ParamId, SqlDbType.Int).Value = userId;
                command.Parameters.Add(paramLastSeen, SqlDbType.DateTime2).Value = lastSeenUtc;

                command.Parameters.Add(paramDev, SqlDbType.NVarChar, FriendServiceContext.DEVICE_MAX_LENGTH).Value =
                    string.IsNullOrWhiteSpace(device) ? (object)DBNull.Value : device;

                connection.Open();

                int affectedRows = command.ExecuteNonQuery();
                if (affectedRows != ExpectedInsertRows)
                {
                    Assert.Fail(string.Format("InsertPresenceRow affectedRows='{0}'.", affectedRows));
                }
            }
        }

        private static class TokenStoreCleaner
        {
            internal static void Clean()
            {
                TokenStore.Cache.Clear();
                TokenStore.ActiveTokenByUserId.Clear();
            }
        }

        private readonly struct PresenceRow
        {
            private PresenceRow(bool isFound, DateTime? lastSeenUtc, string device)
            {
                IsFound = isFound;
                LastSeenUtc = lastSeenUtc;
                Device = device;
            }

            internal bool IsFound { get; }
            internal DateTime? LastSeenUtc { get; }
            internal string Device { get; }

            internal static PresenceRow NotFound()
            {
                return new PresenceRow(false, null, null);
            }

            internal static PresenceRow Found(DateTime? lastSeenUtc, string device)
            {
                return new PresenceRow(true, lastSeenUtc, device);
            }
        }
    }
}
