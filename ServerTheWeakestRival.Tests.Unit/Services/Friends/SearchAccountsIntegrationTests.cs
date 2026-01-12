using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Integration.Helpers;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Friends;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using System;
using System.Linq;

namespace ServerTheWeakestRival.Tests.Integration.Services.Friends
{
    [TestClass]
    public sealed class SearchAccountsIntegrationTests
    {
        private const string DisplayNameMe = "Me";
        private const string DisplayNameFriend = "Friend";
        private const string DisplayNameIncoming = "Incoming";
        private const string DisplayNameOutgoing = "Outgoing";

        private const string QueryMatch = "example.local";
        private const int MaxResults = 20;

        private const int TokenLifetimeMinutes = 10;
        private const string TokenPrefix = "test-token-";

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
        public void SearchAccounts_AlwaysExcludesSelf()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            string meToken = StoreTokenForUser(meAccountId);

            FriendAccountLogic logic = CreateLogic();

            SearchAccountsResponse response = logic.SearchAccounts(new SearchAccountsRequest
            {
                Token = meToken,
                Query = QueryMatch,
                MaxResults = MaxResults
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Results);

            Assert.IsFalse(response.Results.Any(a => a.AccountId == meAccountId));
        }

        [TestMethod]
        public void SearchAccounts_WhenAcceptedFriend_SetsIsFriendTrue()
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

            FriendAccountLogic logic = CreateLogic();

            SearchAccountsResponse response = logic.SearchAccounts(new SearchAccountsRequest
            {
                Token = meToken,
                Query = QueryMatch,
                MaxResults = MaxResults
            });

            SearchAccountItem friend = FindRequired(response, friendAccountId);

            Assert.IsTrue(friend.IsFriend);
            Assert.IsFalse(friend.HasPendingIncoming);
            Assert.IsFalse(friend.HasPendingOutgoing);
            Assert.IsFalse(friend.PendingIncomingRequestId.HasValue);
        }

        [TestMethod]
        public void SearchAccounts_WhenPendingIncoming_SetsHasPendingIncomingAndRequestId()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int incomingAccountId = CreateAccount(DisplayNameIncoming);

            string meToken = StoreTokenForUser(meAccountId);
            string incomingToken = StoreTokenForUser(incomingAccountId);

            FriendRequestLogic requestLogic = CreateRequestLogic();

            int requestId = requestLogic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = incomingToken,
                TargetAccountId = meAccountId,
                Message = "incoming"
            }).FriendRequestId;

            FriendAccountLogic logic = CreateLogic();

            SearchAccountsResponse response = logic.SearchAccounts(new SearchAccountsRequest
            {
                Token = meToken,
                Query = QueryMatch,
                MaxResults = MaxResults
            });

            SearchAccountItem item = FindRequired(response, incomingAccountId);

            Assert.IsFalse(item.IsFriend);
            Assert.IsTrue(item.HasPendingIncoming);
            Assert.IsFalse(item.HasPendingOutgoing);

            Assert.IsTrue(item.PendingIncomingRequestId.HasValue);
            Assert.AreEqual(requestId, item.PendingIncomingRequestId.Value);
        }

        [TestMethod]
        public void SearchAccounts_WhenPendingOutgoing_SetsHasPendingOutgoingTrue()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int outgoingAccountId = CreateAccount(DisplayNameOutgoing);

            string meToken = StoreTokenForUser(meAccountId);
            StoreTokenForUser(outgoingAccountId);

            FriendRequestLogic requestLogic = CreateRequestLogic();

            int requestId = requestLogic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = meToken,
                TargetAccountId = outgoingAccountId,
                Message = "outgoing"
            }).FriendRequestId;

            Assert.IsTrue(requestId > 0);

            FriendAccountLogic logic = CreateLogic();

            SearchAccountsResponse response = logic.SearchAccounts(new SearchAccountsRequest
            {
                Token = meToken,
                Query = QueryMatch,
                MaxResults = MaxResults
            });

            SearchAccountItem item = FindRequired(response, outgoingAccountId);

            Assert.IsFalse(item.IsFriend);
            Assert.IsFalse(item.HasPendingIncoming);
            Assert.IsTrue(item.HasPendingOutgoing);
            Assert.IsFalse(item.PendingIncomingRequestId.HasValue);
        }

        private int CreateAccount(string displayName)
        {
            return TestAccountFactory.CreateAccountAndUser(connectionString, displayName);
        }

        private static FriendAccountLogic CreateLogic()
        {
            IFriendAccountRepository repository = new FriendAccountRepository();
            return new FriendAccountLogic(repository);
        }

        private static FriendRequestLogic CreateRequestLogic()
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

        private static SearchAccountItem FindRequired(SearchAccountsResponse response, int accountId)
        {
            if (response == null || response.Results == null)
            {
                Assert.Fail("SearchAccountsResponse must be provided.");
            }

            SearchAccountItem found = response.Results.FirstOrDefault(a => a.AccountId == accountId);
            if (found == null)
            {
                Assert.Fail(string.Format("Expected AccountId='{0}' in results but it was not found.", accountId));
            }

            return found;
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
