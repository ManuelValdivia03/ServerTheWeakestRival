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
    public sealed class RejectFriendRequestIntegrationTests
    {
        private const string DisplayNameMe = "Me";
        private const string DisplayNameOther = "Other";

        private const int TokenLifetimeMinutes = 10;
        private const string TokenPrefix = "test-token-";

        private const int MissingRequestId = -1;

        private const string ExpectedNotFoundCode = "FR_NOT_FOUND";
        private const string ExpectedForbiddenCode = "FR_FORBIDDEN";
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
        public void RejectFriendRequest_WhenRequestDoesNotExist_ThrowsNotFoundFault()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            string meToken = StoreTokenForUser(meAccountId);

            FriendRequestLogic logic = CreateLogic();

            var request = new RejectFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = MissingRequestId
            };

            FaultAssert.ThrowsServiceFault(
                () => logic.RejectFriendRequest(request),
                ExpectedNotFoundCode);
        }

        [TestMethod]
        public void RejectFriendRequest_WhenCallerIsNotInvolved_ThrowsForbiddenFault()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int otherAccountId = CreateAccount(DisplayNameOther);
            int thirdAccountId = CreateAccount("Third");

            string otherToken = StoreTokenForUser(otherAccountId);
            string meToken = StoreTokenForUser(meAccountId);

            FriendRequestLogic logic = CreateLogic();

            int requestId = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = otherToken,
                TargetAccountId = thirdAccountId,
                Message = "req"
            }).FriendRequestId;

            var rejectRequest = new RejectFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = requestId
            };

            FaultAssert.ThrowsServiceFault(
                () => logic.RejectFriendRequest(rejectRequest),
                ExpectedForbiddenCode);
        }

        [TestMethod]
        public void RejectFriendRequest_WhenReceiverRejects_SetsRejected()
        {
            int receiverAccountId = CreateAccount(DisplayNameMe);
            int senderAccountId = CreateAccount(DisplayNameOther);

            string receiverToken = StoreTokenForUser(receiverAccountId);
            string senderToken = StoreTokenForUser(senderAccountId);

            FriendRequestLogic logic = CreateLogic();

            int requestId = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = senderToken,
                TargetAccountId = receiverAccountId,
                Message = "incoming"
            }).FriendRequestId;

            RejectFriendRequestResponse response = logic.RejectFriendRequest(new RejectFriendRequestRequest
            {
                Token = receiverToken,
                FriendRequestId = requestId
            });

            Assert.IsNotNull(response);
            Assert.AreEqual(FriendRequestStatus.Rejected, response.Status);
        }

        [TestMethod]
        public void RejectFriendRequest_WhenSenderCancels_SetsCancelled()
        {
            int senderAccountId = CreateAccount(DisplayNameMe);
            int receiverAccountId = CreateAccount(DisplayNameOther);

            string senderToken = StoreTokenForUser(senderAccountId);
            StoreTokenForUser(receiverAccountId);

            FriendRequestLogic logic = CreateLogic();

            int requestId = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = senderToken,
                TargetAccountId = receiverAccountId,
                Message = "outgoing"
            }).FriendRequestId;

            RejectFriendRequestResponse response = logic.RejectFriendRequest(new RejectFriendRequestRequest
            {
                Token = senderToken,
                FriendRequestId = requestId
            });

            Assert.IsNotNull(response);
            Assert.AreEqual(FriendRequestStatus.Cancelled, response.Status);
        }

        [TestMethod]
        public void RejectFriendRequest_WhenAlreadyProcessed_ThrowsNotPendingFault()
        {
            int receiverAccountId = CreateAccount(DisplayNameMe);
            int senderAccountId = CreateAccount(DisplayNameOther);

            string receiverToken = StoreTokenForUser(receiverAccountId);
            string senderToken = StoreTokenForUser(senderAccountId);

            FriendRequestLogic logic = CreateLogic();

            int requestId = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = senderToken,
                TargetAccountId = receiverAccountId,
                Message = "incoming"
            }).FriendRequestId;

            RejectFriendRequestResponse first = logic.RejectFriendRequest(new RejectFriendRequestRequest
            {
                Token = receiverToken,
                FriendRequestId = requestId
            });

            Assert.IsNotNull(first);
            Assert.AreEqual(FriendRequestStatus.Rejected, first.Status);

            FaultAssert.ThrowsServiceFault(
                () => logic.RejectFriendRequest(new RejectFriendRequestRequest
                {
                    Token = receiverToken,
                    FriendRequestId = requestId
                }),
                ExpectedNotPendingCode);
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
