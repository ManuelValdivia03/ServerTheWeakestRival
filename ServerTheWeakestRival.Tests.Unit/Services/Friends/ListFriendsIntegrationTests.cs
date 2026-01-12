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
    public sealed class ListFriendsIntegrationTests
    {
        private const string DisplayNameMe = "Me";

        private const string DisplayNameAlice = "Alice";
        private const string DisplayNameBob = "Bob";

        private const string DisplayNameIncoming = "IncomingSender";
        private const string DisplayNameOutgoing = "OutgoingTarget";

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
        public void ListFriends_WhenNoFriends_ReturnsEmptyArrays()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            string meToken = StoreTokenForUser(meAccountId);

            FriendPresenceLogic logic = CreatePresenceLogic();

            ListFriendsResponse response = logic.ListFriends(new ListFriendsRequest
            {
                Token = meToken,
                IncludePendingIncoming = true,
                IncludePendingOutgoing = true
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Friends);
            Assert.IsNotNull(response.PendingIncoming);
            Assert.IsNotNull(response.PendingOutgoing);

            Assert.AreEqual(0, response.Friends.Length);
            Assert.AreEqual(0, response.PendingIncoming.Length);
            Assert.AreEqual(0, response.PendingOutgoing.Length);
        }

        [TestMethod]
        public void ListFriends_WhenHasFriends_ReturnsOrderedByDisplayName()
        {
            int meAccountId = CreateAccount(DisplayNameMe);

            int bobAccountId = CreateAccount(DisplayNameBob);
            int aliceAccountId = CreateAccount(DisplayNameAlice);

            string meToken = StoreTokenForUser(meAccountId);
            string bobToken = StoreTokenForUser(bobAccountId);
            string aliceToken = StoreTokenForUser(aliceAccountId);

            FriendRequestLogic requestLogic = CreateRequestLogic();

            int requestIdBob = requestLogic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = bobToken,
                TargetAccountId = meAccountId,
                Message = "bob"
            }).FriendRequestId;

            requestLogic.AcceptFriendRequest(new AcceptFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = requestIdBob
            });

            int requestIdAlice = requestLogic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = aliceToken,
                TargetAccountId = meAccountId,
                Message = "alice"
            }).FriendRequestId;

            requestLogic.AcceptFriendRequest(new AcceptFriendRequestRequest
            {
                Token = meToken,
                FriendRequestId = requestIdAlice
            });

            FriendPresenceLogic presenceLogic = CreatePresenceLogic();

            ListFriendsResponse response = presenceLogic.ListFriends(new ListFriendsRequest
            {
                Token = meToken,
                IncludePendingIncoming = false,
                IncludePendingOutgoing = false
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Friends);

            Assert.AreEqual(2, response.Friends.Length);

            string[] ordered = response.Friends.Select(f => f.DisplayName).ToArray();

            Assert.AreEqual(DisplayNameAlice, ordered[0]);
            Assert.AreEqual(DisplayNameBob, ordered[1]);
        }

        [TestMethod]
        public void ListFriends_WhenHasPendingIncomingAndOutgoing_ReturnsBoth()
        {
            int meAccountId = CreateAccount(DisplayNameMe);

            int incomingSenderId = CreateAccount(DisplayNameIncoming);
            int outgoingTargetId = CreateAccount(DisplayNameOutgoing);

            string meToken = StoreTokenForUser(meAccountId);
            string incomingToken = StoreTokenForUser(incomingSenderId);

            FriendRequestLogic requestLogic = CreateRequestLogic();

            int incomingRequestId = requestLogic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = incomingToken,
                TargetAccountId = meAccountId,
                Message = "incoming"
            }).FriendRequestId;

            Assert.IsTrue(incomingRequestId > 0);

            int outgoingRequestId = requestLogic.SendFriendRequest(new SendFriendRequestRequest
            {
                Token = meToken,
                TargetAccountId = outgoingTargetId,
                Message = "outgoing"
            }).FriendRequestId;

            Assert.IsTrue(outgoingRequestId > 0);

            FriendPresenceLogic presenceLogic = CreatePresenceLogic();

            ListFriendsResponse response = presenceLogic.ListFriends(new ListFriendsRequest
            {
                Token = meToken,
                IncludePendingIncoming = true,
                IncludePendingOutgoing = true
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.PendingIncoming);
            Assert.IsNotNull(response.PendingOutgoing);

            Assert.AreEqual(1, response.PendingIncoming.Length);
            Assert.AreEqual(1, response.PendingOutgoing.Length);

            Assert.AreEqual(incomingSenderId, response.PendingIncoming[0].FromAccountId);
            Assert.AreEqual(meAccountId, response.PendingIncoming[0].ToAccountId);
            Assert.AreEqual(FriendRequestStatus.Pending, response.PendingIncoming[0].Status);

            Assert.AreEqual(meAccountId, response.PendingOutgoing[0].FromAccountId);
            Assert.AreEqual(outgoingTargetId, response.PendingOutgoing[0].ToAccountId);
            Assert.AreEqual(FriendRequestStatus.Pending, response.PendingOutgoing[0].Status);
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
