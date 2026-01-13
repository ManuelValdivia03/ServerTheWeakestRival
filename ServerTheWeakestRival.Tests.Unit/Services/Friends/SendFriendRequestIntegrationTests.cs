using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Integration.Helpers;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Friends;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using System;
using System.ServiceModel;

namespace ServerTheWeakestRival.Tests.Integration.Services.Friends
{
    [TestClass]
    public sealed class SendFriendRequestIntegrationTests
    {
        private const string DisplayNameMe = "Me";
        private const string DisplayNameTarget = "Target";

        private const int InvalidAccountId = 0;

        private const int TokenLifetimeMinutes = 10;

        private const string TokenPrefix = "test-token-";

        private const string ExpectedAuthRequiredCode = "AUTH_REQUIRED";
        private const string ExpectedInvalidRequestCode = "INVALID_REQUEST";
        private const string ExpectedSelfCode = "FR_SELF";

        private const string ExpectedStatusPending = "Pending";
        private const string ExpectedStatusAccepted = "Accepted";

        private string connectionString;

        [TestInitialize]
        public void TestInitialize()
        {
            connectionString = DbTestConfig.GetMainConnectionString();

            DbTestCleaner.CleanupAll();
            TokenStoreTestCleaner.Clean();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DbTestCleaner.CleanupAll();
            TokenStoreTestCleaner.Clean();
        }

        [TestMethod]
        public void SendFriendRequest_WhenTokenMissing_ThrowsAuthRequiredFault()
        {
            FriendRequestLogic logic = CreateLogic();

            var request = new SendFriendRequestRequest
            {
                Token = string.Empty,
                TargetAccountId = 1,
                Message = string.Empty
            };

            FaultAssert.ThrowsServiceFault(
                () => logic.SendFriendRequest(request),
                ExpectedAuthRequiredCode);
        }

        [TestMethod]
        public void SendFriendRequest_WhenNoExistingRequest_CreatesPendingRequest()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int targetAccountId = CreateAccount(DisplayNameTarget);
            string tokenValue = StoreTokenForUser(meAccountId);

            FriendRequestLogic logic = CreateLogic();

            var request = new SendFriendRequestRequest
            {
                Token = tokenValue,
                TargetAccountId = targetAccountId,
                Message = "hi"
            };

            SendFriendRequestResponse response = logic.SendFriendRequest(request);

            Assert.IsNotNull(response);
            Assert.IsTrue(response.FriendRequestId > 0);
            Assert.AreEqual(FriendRequestStatus.Pending, response.Status);
        }

        [TestMethod]
        public void SendFriendRequest_WhenPendingOutgoingExists_ReusesExistingId()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int targetAccountId = CreateAccount(DisplayNameTarget);
            string tokenValue = StoreTokenForUser(meAccountId);

            FriendRequestLogic logic = CreateLogic();

            int firstId = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = tokenValue,
                TargetAccountId = targetAccountId,
                Message = "first"
            }).FriendRequestId;

            SendFriendRequestResponse second = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = tokenValue,
                TargetAccountId = targetAccountId,
                Message = "second"
            });

            Assert.IsNotNull(second);
            Assert.AreEqual(firstId, second.FriendRequestId);
            Assert.AreEqual(FriendRequestStatus.Pending, second.Status);
        }

        [TestMethod]
        public void SendFriendRequest_WhenPendingIncomingExists_AcceptsIncomingAndReturnsAccepted()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int targetAccountId = CreateAccount(DisplayNameTarget);

            string meToken = StoreTokenForUser(meAccountId);
            string targetToken = StoreTokenForUser(targetAccountId);

            FriendRequestLogic logic = CreateLogic();

            int incomingId = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = targetToken,
                TargetAccountId = meAccountId,
                Message = "incoming"
            }).FriendRequestId;

            SendFriendRequestResponse converted = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = meToken,
                TargetAccountId = targetAccountId,
                Message = "convert"
            });

            Assert.IsNotNull(converted);
            Assert.AreEqual(incomingId, converted.FriendRequestId);
            Assert.AreEqual(FriendRequestStatus.Accepted, converted.Status);
        }

        private int CreateAccount(string displayName)
        {
            return TestAccountFactory.CreateAccountAndUser(connectionString, displayName);
        }

        private static FriendRequestLogic CreateLogic()
        {
            IFriendRequestRepository repository = new FriendRequestRepository();
            return new FriendRequestLogic(repository);
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

        private static class TokenStoreTestCleaner
        {
            internal static void Clean()
            {
                TokenStore.Cache.Clear();
                TokenStore.ActiveTokenByUserId.Clear();
            }
        }
    }

    internal static class FaultAssert
    {
        public static void ThrowsServiceFault(Action action, string expectedCode)
        {
            if (action == null)
            {
                Assert.Fail("action must be provided.");
            }

            try
            {
                action();
                Assert.Fail("Expected FaultException<ServiceFault> but no exception was thrown.");
            }
            catch (FaultException<ServiceFault> ex)
            {
                Assert.IsNotNull(ex.Detail);
                Assert.AreEqual(expectedCode, ex.Detail.Code);
            }
        }
    }
}
