using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using System;

using ServerTokenStore = ServicesTheWeakestRival.Server.Services.TokenStore;
using TokenCleaner = ServerTheWeakestRival.Tests.Unit.Infrastructure.TokenStoreTestCleaner;

namespace ServerTheWeakestRival.Tests.Unit.Services
{
    [TestClass]
    public sealed class TokenStoreTests
    {
        private const int USER_ID = 123;

        [TestInitialize]
        public void SetUp()
        {
            TokenCleaner.ClearAllTokens();
        }

        [TestCleanup]
        public void TearDown()
        {
            TokenCleaner.ClearAllTokens();
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenIsNull_ReturnsFalse()
        {
            bool ok = ServerTokenStore.TryGetUserId(null, out int userId);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, userId);
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenNotFound_ReturnsFalse()
        {
            bool ok = ServerTokenStore.TryGetUserId(Guid.NewGuid().ToString("N"), out int userId);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, userId);
        }

        [TestMethod]
        public void StoreToken_WhenTokenIsNull_DoesNothing()
        {
            ServerTokenStore.StoreToken(null);

            bool ok = ServerTokenStore.TryGetActiveTokenForUser(USER_ID, out AuthToken active);
            Assert.IsFalse(ok);
            Assert.IsNull(active);
        }

        [TestMethod]
        public void StoreToken_WhenValidToken_AllowsTryGetUserId()
        {
            var token = new AuthToken
            {
                UserId = USER_ID,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };

            ServerTokenStore.StoreToken(token);

            bool ok = ServerTokenStore.TryGetUserId(token.Token, out int userId);

            Assert.IsTrue(ok);
            Assert.AreEqual(USER_ID, userId);
        }

        [TestMethod]
        public void StoreToken_WhenNewTokenForSameUser_RemovesPreviousToken()
        {
            var first = new AuthToken
            {
                UserId = USER_ID,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };

            var second = new AuthToken
            {
                UserId = USER_ID,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };

            ServerTokenStore.StoreToken(first);
            ServerTokenStore.StoreToken(second);

            bool okFirst = ServerTokenStore.TryGetUserId(first.Token, out int userIdFirst);
            Assert.IsFalse(okFirst);
            Assert.AreEqual(0, userIdFirst);

            bool okSecond = ServerTokenStore.TryGetUserId(second.Token, out int userIdSecond);
            Assert.IsTrue(okSecond);
            Assert.AreEqual(USER_ID, userIdSecond);
        }

        [TestMethod]
        public void TryGetActiveTokenForUser_WhenUserIdInvalid_ReturnsFalse()
        {
            bool ok = ServerTokenStore.TryGetActiveTokenForUser(0, out AuthToken token);

            Assert.IsFalse(ok);
            Assert.IsNull(token);
        }

        [TestMethod]
        public void TryGetActiveTokenForUser_WhenExists_ReturnsToken()
        {
            var stored = new AuthToken
            {
                UserId = USER_ID,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };

            ServerTokenStore.StoreToken(stored);

            bool ok = ServerTokenStore.TryGetActiveTokenForUser(USER_ID, out AuthToken active);

            Assert.IsTrue(ok);
            Assert.IsNotNull(active);
            Assert.AreEqual(USER_ID, active.UserId);
            Assert.AreEqual(stored.Token, active.Token);
        }

        [TestMethod]
        public void TryRemoveToken_WhenTokenIsEmpty_ReturnsFalse()
        {
            bool ok = ServerTokenStore.TryRemoveToken(string.Empty, out AuthToken removed);

            Assert.IsFalse(ok);
            Assert.IsNull(removed);
        }

        [TestMethod]
        public void TryRemoveToken_WhenTokenExists_RemovesIt()
        {
            var stored = new AuthToken
            {
                UserId = USER_ID,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };

            ServerTokenStore.StoreToken(stored);

            bool removedOk = ServerTokenStore.TryRemoveToken(stored.Token, out AuthToken removed);

            Assert.IsTrue(removedOk);
            Assert.IsNotNull(removed);
            Assert.AreEqual(stored.Token, removed.Token);

            bool okAfter = ServerTokenStore.TryGetUserId(stored.Token, out int userIdAfter);
            Assert.IsFalse(okAfter);
            Assert.AreEqual(0, userIdAfter);
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenExpired_RemovesAndReturnsFalse()
        {
            var stored = new AuthToken
            {
                UserId = USER_ID,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
            };

            ServerTokenStore.StoreToken(stored);

            bool ok = ServerTokenStore.TryGetUserId(stored.Token, out int userId);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, userId);

            bool ok2 = ServerTokenStore.TryGetActiveTokenForUser(USER_ID, out AuthToken active);
            Assert.IsFalse(ok2);
            Assert.IsNull(active);
        }
    }
}
