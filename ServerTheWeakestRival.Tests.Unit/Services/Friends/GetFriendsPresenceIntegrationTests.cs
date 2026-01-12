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
    public sealed class GetFriendsPresenceIntegrationTests
    {
        private const string DisplayNameMe = "Me";
        private const string DisplayNameFriend = "Friend";

        private const int TokenLifetimeMinutes = 10;
        private const string TokenPrefix = "test-token-";

        private const int ExpectedInsertRows = 1;

        private const int OnlineWindowSeconds = FriendServiceContext.ONLINE_WINDOW_SECONDS;

        private const string SqlInsertPresence = @"
            INSERT INTO dbo.UserPresence (user_id, last_seen_utc, device)
            VALUES (@Id, @LastSeenUtc, @Dev);";

        private const string SqlUpdatePresence = @"
            UPDATE dbo.UserPresence
            SET last_seen_utc = @LastSeenUtc, device = @Dev
            WHERE user_id = @Id;";

        private const string ParamId = "@Id";
        private const string ParamLastSeenUtc = "@LastSeenUtc";
        private const string ParamDev = "@Dev";

        private const string DeviceValue = "TEST_DEVICE";

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
        public void GetFriendsPresence_WhenFriendLastSeenWithinWindow_IsOnlineTrue()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int friendAccountId = CreateAccount(DisplayNameFriend);

            string meToken = StoreTokenForUser(meAccountId);
            string friendToken = StoreTokenForUser(friendAccountId);

            FriendRequestLogic requestLogic = CreateRequestLogic();

            int requestId = requestLogic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = friendToken,
                TargetAccountId = meAccountId,
                Message = "incoming"
            }).FriendRequestId;

            requestLogic.AcceptFriendRequest(new AcceptFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = requestId
            });

            DateTime utcNow = DateTime.UtcNow;
            DateTime withinWindow = utcNow.AddSeconds(-OnlineWindowSeconds + 5);

            UpsertPresence(friendAccountId, withinWindow, DeviceValue);

            FriendPresenceLogic presenceLogic = CreatePresenceLogic();

            GetFriendsPresenceResponse response = presenceLogic.GetFriendsPresence(new GetFriendsPresenceRequest
            {
                Token = meToken
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Friends);
            Assert.AreEqual(1, response.Friends.Length);

            FriendPresence presence = response.Friends[0];

            Assert.AreEqual(friendAccountId, presence.AccountId);
            Assert.IsTrue(presence.IsOnline);
            Assert.IsTrue(presence.LastSeenUtc.HasValue);
        }

        [TestMethod]
        public void GetFriendsPresence_WhenFriendLastSeenOutsideWindow_IsOnlineFalse()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int friendAccountId = CreateAccount(DisplayNameFriend);

            string meToken = StoreTokenForUser(meAccountId);
            string friendToken = StoreTokenForUser(friendAccountId);

            FriendRequestLogic requestLogic = CreateRequestLogic();

            int requestId = requestLogic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = friendToken,
                TargetAccountId = meAccountId,
                Message = "incoming"
            }).FriendRequestId;

            requestLogic.AcceptFriendRequest(new AcceptFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = requestId
            });

            DateTime utcNow = DateTime.UtcNow;
            DateTime outsideWindow = utcNow.AddSeconds(-OnlineWindowSeconds - 5);

            UpsertPresence(friendAccountId, outsideWindow, DeviceValue);

            FriendPresenceLogic presenceLogic = CreatePresenceLogic();

            GetFriendsPresenceResponse response = presenceLogic.GetFriendsPresence(new GetFriendsPresenceRequest
            {
                Token = meToken
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Friends);
            Assert.AreEqual(1, response.Friends.Length);

            FriendPresence presence = response.Friends[0];

            Assert.AreEqual(friendAccountId, presence.AccountId);
            Assert.IsFalse(presence.IsOnline);
            Assert.IsTrue(presence.LastSeenUtc.HasValue);
        }

        [TestMethod]
        public void GetFriendsPresence_WhenFriendHasNoPresenceRow_IsOnlineFalseAndLastSeenNull()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int friendAccountId = CreateAccount(DisplayNameFriend);

            string meToken = StoreTokenForUser(meAccountId);
            string friendToken = StoreTokenForUser(friendAccountId);

            FriendRequestLogic requestLogic = CreateRequestLogic();

            int requestId = requestLogic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = friendToken,
                TargetAccountId = meAccountId,
                Message = "incoming"
            }).FriendRequestId;

            requestLogic.AcceptFriendRequest(new AcceptFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = requestId
            });

            FriendPresenceLogic presenceLogic = CreatePresenceLogic();

            GetFriendsPresenceResponse response = presenceLogic.GetFriendsPresence(new GetFriendsPresenceRequest
            {
                Token = meToken
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Friends);
            Assert.AreEqual(1, response.Friends.Length);

            FriendPresence presence = response.Friends[0];

            Assert.AreEqual(friendAccountId, presence.AccountId);
            Assert.IsFalse(presence.IsOnline);
            Assert.IsFalse(presence.LastSeenUtc.HasValue);
        }

        private int CreateAccount(string displayName)
        {
            return TestAccountFactory.CreateAccountAndUser(connectionString, displayName);
        }

        private static FriendRequestLogic CreateRequestLogic()
        {
            IFriendRequestRepository repository = new FriendRequestRepository();
            return new FriendRequestLogic(repository);
        }

        private static FriendPresenceLogic CreatePresenceLogic()
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

        private void UpsertPresence(int userId, DateTime lastSeenUtc, string device)
        {
            bool exists = PresenceRowExists(userId);
            string sql = exists ? SqlUpdatePresence : SqlInsertPresence;

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.CommandType = CommandType.Text;

                command.Parameters.Add(ParamId, SqlDbType.Int).Value = userId;
                command.Parameters.Add(ParamLastSeenUtc, SqlDbType.DateTime2).Value = lastSeenUtc;

                command.Parameters.Add(ParamDev, SqlDbType.NVarChar, FriendServiceContext.DEVICE_MAX_LENGTH).Value =
                    string.IsNullOrWhiteSpace(device) ? (object)DBNull.Value : device;

                connection.Open();

                int affectedRows = command.ExecuteNonQuery();
                if (affectedRows != ExpectedInsertRows)
                {
                    Assert.Fail(string.Format("UpsertPresence affectedRows='{0}'.", affectedRows));
                }
            }
        }

        private bool PresenceRowExists(int userId)
        {
            const string sqlExists = @"
                SELECT 1
                FROM dbo.UserPresence
                WHERE user_id = @Id;";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sqlExists, connection))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.Add(ParamId, SqlDbType.Int).Value = userId;

                connection.Open();

                object scalar = command.ExecuteScalar();
                return scalar != null && scalar != DBNull.Value;
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
    }
}
