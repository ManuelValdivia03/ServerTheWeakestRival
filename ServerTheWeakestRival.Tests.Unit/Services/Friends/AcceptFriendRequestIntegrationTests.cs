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
    public sealed class AcceptFriendRequestIntegrationTests
    {
        private const string DisplayNameMe = "Me";
        private const string DisplayNameTarget = "Target";

        private const int TokenLifetimeMinutes = 10;
        private const string TokenPrefix = "test-token-";

        private const int MissingRequestId = -1;

        private const string ExpectedNotFoundCode = "FR_NOT_FOUND";
        private const string ExpectedForbiddenCode = "FORBIDDEN";
        private const string ExpectedNotPendingCode = "FR_NOT_PENDING";

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
        public void AcceptFriendRequest_WhenRequestDoesNotExist_ThrowsNotFoundFault()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            string meToken = StoreTokenForUser(meAccountId);

            FriendRequestLogic logic = CreateLogic();

            var request = new AcceptFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = MissingRequestId
            };

            FaultAssert.ThrowsServiceFault(
                () => logic.AcceptFriendRequest(request),
                ExpectedNotFoundCode);
        }

        [TestMethod]
        public void AcceptFriendRequest_WhenCallerIsNotReceiver_ThrowsForbiddenFault()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int targetAccountId = CreateAccount(DisplayNameTarget);

            string meToken = StoreTokenForUser(meAccountId);
            string targetToken = StoreTokenForUser(targetAccountId);

            FriendRequestLogic logic = CreateLogic();

            int requestId = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = targetToken,
                TargetAccountId = meAccountId,
                Message = "incoming"
            }).FriendRequestId;

            var acceptRequest = new AcceptFriendRequestRequest
            {
                Token = targetToken,
                FriendRequestId = requestId
            };

            FaultAssert.ThrowsServiceFault(
                () => logic.AcceptFriendRequest(acceptRequest),
                ExpectedForbiddenCode);
        }

        [TestMethod]
        public void AcceptFriendRequest_WhenRequestIsNotPending_ThrowsNotPendingFault()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int targetAccountId = CreateAccount(DisplayNameTarget);

            string meToken = StoreTokenForUser(meAccountId);
            string targetToken = StoreTokenForUser(targetAccountId);

            FriendRequestLogic logic = CreateLogic();

            int requestId = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = targetToken,
                TargetAccountId = meAccountId,
                Message = "incoming"
            }).FriendRequestId;

            var acceptRequest = new AcceptFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = requestId
            };

            AcceptFriendRequestResponse first = logic.AcceptFriendRequest(acceptRequest);

            Assert.IsNotNull(first);
            Assert.IsNotNull(first.NewFriend);
            Assert.AreEqual(targetAccountId, first.NewFriend.AccountId);

            FaultAssert.ThrowsServiceFault(
                () => logic.AcceptFriendRequest(acceptRequest),
                ExpectedNotPendingCode);
        }

        [TestMethod]
        public void AcceptFriendRequest_WhenPendingIncoming_HappyPath_ReturnsNewFriendWithFromAccountId()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int targetAccountId = CreateAccount(DisplayNameTarget);

            string meToken = StoreTokenForUser(meAccountId);
            string targetToken = StoreTokenForUser(targetAccountId);

            FriendRequestLogic logic = CreateLogic();

            int requestId = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = targetToken,
                TargetAccountId = meAccountId,
                Message = "incoming"
            }).FriendRequestId;

            AcceptFriendRequestResponse response = logic.AcceptFriendRequest(new AcceptFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = requestId
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.NewFriend);

            Assert.AreEqual(targetAccountId, response.NewFriend.AccountId);
            Assert.AreNotEqual(default(DateTime), response.NewFriend.SinceUtc);
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

        private static class TokenStoreCleaner
        {
            internal static void Clean()
            {
                TokenStore.Cache.Clear();
                TokenStore.ActiveTokenByUserId.Clear();
            }
        }

        internal static class FaultAssert
        {
            internal static void ThrowsServiceFault(Action action, string expectedCode)
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
}
