using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using System;
using System.ServiceModel;
using System.Threading;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayAuthTests
    {
        private const int USER_ID = 123;

        private const string TOKEN_VALID = "token-valid";
        private const string TOKEN_VALID_2 = "token-valid-2";

        private const string TOKEN_MISSING = "   ";
        private const string TOKEN_UNKNOWN = "token-unknown";

        private const string CODE_AUTH_REQUIRED = "AUTH_REQUIRED";
        private const string CODE_AUTH_INVALID = "AUTH_INVALID";
        private const string CODE_AUTH_EXPIRED = "AUTH_EXPIRED";

        [TestInitialize]
        public void TestInitialize()
        {
            TokenStore.Cache.Clear();
            TokenStore.ActiveTokenByUserId.Clear();
        }

        [TestMethod]
        public void Authenticate_TokenNullOrWhitespace_ThrowsAuthRequiredFault()
        {
            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayAuth.Authenticate(TOKEN_MISSING));

            Assert.AreEqual(CODE_AUTH_REQUIRED, ex.Detail.Code);
        }

        [TestMethod]
        public void Authenticate_TokenNotInCache_ThrowsAuthInvalidFault()
        {
            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayAuth.Authenticate(TOKEN_UNKNOWN));

            Assert.AreEqual(CODE_AUTH_INVALID, ex.Detail.Code);
        }

        [TestMethod]
        public void Authenticate_TokenExpired_ThrowsAuthExpiredFault()
        {
            TokenStore.StoreToken(new AuthToken
            {
                Token = TOKEN_VALID,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayAuth.Authenticate(TOKEN_VALID));

            Assert.AreEqual(CODE_AUTH_EXPIRED, ex.Detail.Code);
        }

        [TestMethod]
        public void Authenticate_TokenValid_ReturnsUserId()
        {
            TokenStore.StoreToken(new AuthToken
            {
                Token = TOKEN_VALID,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });

            int userId = GameplayAuth.Authenticate(TOKEN_VALID);

            Assert.AreEqual(USER_ID, userId);
        }

        [TestMethod]
        public void Authenticate_TokenWithTrailingSpaces_IsInvalidBecauseLookupIsOrdinal()
        {
            TokenStore.StoreToken(new AuthToken
            {
                Token = TOKEN_VALID,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayAuth.Authenticate(TOKEN_VALID + "  "));

            Assert.AreEqual(CODE_AUTH_INVALID, ex.Detail.Code);
        }

        [TestMethod]
        public void Authenticate_TokenValid_ButDifferentCasing_IsInvalidBecauseLookupIsOrdinal()
        {
            const string tokenUpper = "TOKEN-VALID";

            TokenStore.StoreToken(new AuthToken
            {
                Token = tokenUpper,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayAuth.Authenticate(tokenUpper.ToLowerInvariant()));

            Assert.AreEqual(CODE_AUTH_INVALID, ex.Detail.Code);
        }

        [TestMethod]
        public void Authenticate_TokenExactlyAtNowUtc_IsExpired()
        {
            TokenStore.StoreToken(new AuthToken
            {
                Token = TOKEN_VALID,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow
            });

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayAuth.Authenticate(TOKEN_VALID));

            Assert.AreEqual(CODE_AUTH_EXPIRED, ex.Detail.Code);
        }

        [TestMethod]
        public void Authenticate_TokenSlightlyInFuture_IsValid()
        {
            TokenStore.StoreToken(new AuthToken
            {
                Token = TOKEN_VALID,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow.AddMilliseconds(250)
            });

            int userId = GameplayAuth.Authenticate(TOKEN_VALID);

            Assert.AreEqual(USER_ID, userId);
        }

        [TestMethod]
        public void Authenticate_ExpiredTokenRemainsInCache_StillThrowsExpired()
        {
            TokenStore.StoreToken(new AuthToken
            {
                Token = TOKEN_VALID,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-5)
            });

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayAuth.Authenticate(TOKEN_VALID));

            Assert.AreEqual(CODE_AUTH_EXPIRED, ex.Detail.Code);
            Assert.IsTrue(TokenStore.Cache.ContainsKey(TOKEN_VALID));
        }

        [TestMethod]
        public void Authenticate_ValidToken_DoesNotMutateTokenStore()
        {
            var token = new AuthToken
            {
                Token = TOKEN_VALID,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };

            TokenStore.StoreToken(token);

            int beforeCacheCount = TokenStore.Cache.Count;

            int userId = GameplayAuth.Authenticate(TOKEN_VALID);

            Assert.AreEqual(USER_ID, userId);
            Assert.AreEqual(beforeCacheCount, TokenStore.Cache.Count);
            Assert.IsTrue(TokenStore.Cache.ContainsKey(TOKEN_VALID));
        }

        [TestMethod]
        public void Authenticate_WhenSecondTokenStoredForSameUser_FirstTokenBecomesInvalid()
        {
            TokenStore.StoreToken(new AuthToken
            {
                Token = TOKEN_VALID,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });

            TokenStore.StoreToken(new AuthToken
            {
                Token = TOKEN_VALID_2,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });

            int userId2 = GameplayAuth.Authenticate(TOKEN_VALID_2);
            Assert.AreEqual(USER_ID, userId2);

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayAuth.Authenticate(TOKEN_VALID));
            Assert.IsFalse(string.IsNullOrWhiteSpace(ex.Detail.Code));
        }


        [TestMethod]
        public void Authenticate_ConcurrentCalls_AllReturnSameUserId()
        {
            TokenStore.StoreToken(new AuthToken
            {
                Token = TOKEN_VALID,
                UserId = USER_ID,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });

            const int threads = 20;
            int okCount = 0;

            Thread[] workers = new Thread[threads];
            for (int i = 0; i < threads; i++)
            {
                workers[i] = new Thread(() =>
                {
                    int id = GameplayAuth.Authenticate(TOKEN_VALID);
                    if (id == USER_ID)
                    {
                        Interlocked.Increment(ref okCount);
                    }
                });
            }

            for (int i = 0; i < threads; i++)
            {
                workers[i].Start();
            }

            for (int i = 0; i < threads; i++)
            {
                workers[i].Join();
            }

            Assert.AreEqual(threads, okCount);
        }

        [TestMethod]
        public void Authenticate_FaultExceptionBubblesUp()
        {
            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayAuth.Authenticate(TOKEN_UNKNOWN));

            Assert.AreEqual(CODE_AUTH_INVALID, ex.Detail.Code);
            Assert.IsNotNull(ex.Detail);
        }

        private static FaultException<ServiceFault> AssertThrowsFault(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
                return null;
            }
            catch (FaultException<ServiceFault> ex)
            {
                return ex;
            }
        }
    }
}
