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
    public sealed class RemoveFriendIntegrationTests
    {
        private const string DisplayNameMe = "Me";
        private const string DisplayNameOther = "Other";

        private const int TokenLifetimeMinutes = 10;
        private const string TokenPrefix = "test-token-";

        private const int InvalidAccountId = 0;

        private const string ExpectedInvalidRequestCode = "INVALID_REQUEST";

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
        public void RemoveFriend_WhenNoFriendship_ReturnsRemovedFalse()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int otherAccountId = CreateAccount(DisplayNameOther);

            string meToken = StoreTokenForUser(meAccountId);
            StoreTokenForUser(otherAccountId);

            FriendRequestLogic logic = CreateLogic();

            RemoveFriendResponse response = logic.RemoveFriend(new RemoveFriendRequest
            {
                Token = meToken,
                FriendAccountId = otherAccountId
            });

            Assert.IsNotNull(response);
            Assert.IsFalse(response.Removed);
        }

        [TestMethod]
        public void RemoveFriend_WhenFriendshipExists_MarksCancelledAndReturnsRemovedTrue()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int otherAccountId = CreateAccount(DisplayNameOther);

            string meToken = StoreTokenForUser(meAccountId);
            string otherToken = StoreTokenForUser(otherAccountId);

            FriendRequestLogic logic = CreateLogic();

            int requestId = logic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = otherToken,
                TargetAccountId = meAccountId,
                Message = "incoming"
            }).FriendRequestId;

            AcceptFriendRequestResponse accepted = logic.AcceptFriendRequest(new AcceptFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = requestId
            });

            Assert.IsNotNull(accepted);
            Assert.IsNotNull(accepted.NewFriend);
            Assert.AreEqual(otherAccountId, accepted.NewFriend.AccountId);

            RemoveFriendResponse removed = logic.RemoveFriend(new RemoveFriendRequest
            {
                Token = meToken,
                FriendAccountId = otherAccountId
            });

            Assert.IsNotNull(removed);
            Assert.IsTrue(removed.Removed);

            RemoveFriendResponse removedAgain = logic.RemoveFriend(new RemoveFriendRequest
            {
                Token = meToken,
                FriendAccountId = otherAccountId
            });

            Assert.IsNotNull(removedAgain);
            Assert.IsFalse(removedAgain.Removed);
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
