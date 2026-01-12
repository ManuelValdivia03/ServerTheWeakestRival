using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Integration.Helpers;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using System;
using System.Linq;

namespace ServerTheWeakestRival.Tests.Integration.Services.Friends
{
    [TestClass]
    public sealed class GetAccountsByIdsIntegrationTests
    {
        private const string DisplayNameMe = "Me";
        private const string DisplayNameA = "A";
        private const string DisplayNameB = "B";

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
        public void GetAccountsByIds_WhenAccountIdsIsEmpty_ReturnsEmptyArray()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            string meToken = StoreTokenForUser(meAccountId);

            FriendAccountLogic logic = CreateLogic();

            GetAccountsByIdsResponse response = logic.GetAccountsByIds(new GetAccountsByIdsRequest
            {
                Token = meToken,
                AccountIds = Array.Empty<int>()
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Accounts);
            Assert.AreEqual(0, response.Accounts.Length);
        }

        [TestMethod]
        public void GetAccountsByIds_WhenRequestIncludesSelf_SelfIsExcluded()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int aAccountId = CreateAccount(DisplayNameA);

            string meToken = StoreTokenForUser(meAccountId);
            StoreTokenForUser(aAccountId);

            FriendAccountLogic logic = CreateLogic();

            GetAccountsByIdsResponse response = logic.GetAccountsByIds(new GetAccountsByIdsRequest
            {
                Token = meToken,
                AccountIds = new[] { meAccountId, aAccountId }
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Accounts);

            Assert.IsFalse(response.Accounts.Any(a => a.AccountId == meAccountId));
            Assert.IsTrue(response.Accounts.Any(a => a.AccountId == aAccountId));
        }

        [TestMethod]
        public void GetAccountsByIds_WhenIdsContainUnknownOnes_ReturnsOnlyExisting()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int aAccountId = CreateAccount(DisplayNameA);
            int bAccountId = CreateAccount(DisplayNameB);

            string meToken = StoreTokenForUser(meAccountId);
            StoreTokenForUser(aAccountId);
            StoreTokenForUser(bAccountId);

            const int unknownId = int.MaxValue;

            FriendAccountLogic logic = CreateLogic();

            GetAccountsByIdsResponse response = logic.GetAccountsByIds(new GetAccountsByIdsRequest
            {
                Token = meToken,
                AccountIds = new[] { aAccountId, unknownId, bAccountId }
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Accounts);

            int[] ids = response.Accounts.Select(a => a.AccountId).OrderBy(i => i).ToArray();

            Assert.AreEqual(2, ids.Length);
            Assert.AreEqual(aAccountId, ids[0]);
            Assert.AreEqual(bAccountId, ids[1]);
        }

        [TestMethod]
        public void GetAccountsByIds_WhenAccountsExist_ReturnsBasicFieldsAndAvatarNotNull()
        {
            int meAccountId = CreateAccount(DisplayNameMe);
            int aAccountId = CreateAccount(DisplayNameA);

            string meToken = StoreTokenForUser(meAccountId);
            StoreTokenForUser(aAccountId);

            FriendAccountLogic logic = CreateLogic();

            GetAccountsByIdsResponse response = logic.GetAccountsByIds(new GetAccountsByIdsRequest
            {
                Token = meToken,
                AccountIds = new[] { aAccountId }
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Accounts);
            Assert.AreEqual(1, response.Accounts.Length);

            AccountMini account = response.Accounts[0];

            Assert.AreEqual(aAccountId, account.AccountId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(account.Email));
            Assert.IsNotNull(account.DisplayName);
            Assert.IsNotNull(account.ProfileImageCode);
            Assert.IsNotNull(account.Avatar);
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
