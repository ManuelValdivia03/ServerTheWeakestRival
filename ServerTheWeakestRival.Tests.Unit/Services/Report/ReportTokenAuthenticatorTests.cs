using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Reports;
using System;
using System.ServiceModel;

namespace ServerTheWeakestRival.Tests.Unit.Services.Reports
{
    [TestClass]
    public sealed class ReportTokenAuthenticatorTests
    {
        private const int VALID_USER_ID = 11;
        private const int INVALID_USER_ID = 0;

        private const string VALID_TOKEN = "token-valid";
        private const string UNKNOWN_TOKEN = "token-unknown";
        private const string WHITESPACE_TOKEN = "   ";
        private const string EMPTY_TOKEN = "";
        private const string NEW_TOKEN_FOR_SAME_USER = "token-new";
        private const int EXPIRED_TOKEN_OFFSET_MINUTES = -1;


        private const int TOKEN_TTL_MINUTES = 30;

        private ReportTokenAuthenticator authenticator;

        [TestInitialize]
        public void TestInitialize()
        {
            authenticator = new ReportTokenAuthenticator();
            TokenStoreCleaner.ClearAll();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            TokenStoreCleaner.ClearAll();
        }

        [TestMethod]
        public void AuthenticateOrThrow_WhenTokenIsNull_ThrowsFault()
        {
            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => authenticator.AuthenticateOrThrow(null));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.TOKEN_INVALID,
                ReportConstants.MessageKey.TOKEN_INVALID);
        }

        [TestMethod]
        public void AuthenticateOrThrow_WhenTokenIsWhitespace_ThrowsFault()
        {
            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => authenticator.AuthenticateOrThrow(WHITESPACE_TOKEN));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.TOKEN_INVALID,
                ReportConstants.MessageKey.TOKEN_INVALID);
        }

        [TestMethod]
        public void AuthenticateOrThrow_WhenTokenIsUnknown_ThrowsFault()
        {
            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => authenticator.AuthenticateOrThrow(UNKNOWN_TOKEN));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.TOKEN_INVALID,
                ReportConstants.MessageKey.TOKEN_INVALID);
        }

        [TestMethod]
        public void AuthenticateOrThrow_WhenTokenMapsToInvalidUserId_ThrowsFault()
        {
            TokenStoreTestHelper.AddToken(VALID_TOKEN, INVALID_USER_ID);

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => authenticator.AuthenticateOrThrow(VALID_TOKEN));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.TOKEN_INVALID,
                ReportConstants.MessageKey.TOKEN_INVALID);
        }

        [TestMethod]
        public void AuthenticateOrThrow_WhenTokenIsValid_ReturnsUserId()
        {
            TokenStoreTestHelper.AddToken(VALID_TOKEN, VALID_USER_ID);

            int userId = authenticator.AuthenticateOrThrow(VALID_TOKEN);

            Assert.AreEqual(VALID_USER_ID, userId);
        }

        [TestMethod]
        public void AuthenticateOrThrow_WhenTokenIsEmpty_ThrowsFault()
        {
            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => authenticator.AuthenticateOrThrow(EMPTY_TOKEN));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.TOKEN_INVALID,
                ReportConstants.MessageKey.TOKEN_INVALID);
        }

        [TestMethod]
        public void AuthenticateOrThrow_WhenTokenIsExpired_ThrowsFault()
        {
            DateTime expiredAtUtc = DateTime.UtcNow.AddMinutes(EXPIRED_TOKEN_OFFSET_MINUTES);

            TokenStoreTestHelper.AddTokenWithExpiry(VALID_TOKEN, VALID_USER_ID, expiredAtUtc);

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => authenticator.AuthenticateOrThrow(VALID_TOKEN));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.TOKEN_INVALID,
                ReportConstants.MessageKey.TOKEN_INVALID);

            Assert.IsFalse(TokenStore.Cache.ContainsKey(VALID_TOKEN));
        }

        [TestMethod]
        public void AuthenticateOrThrow_WhenUserHasNewerToken_OldTokenThrowsFault()
        {
            TokenStoreTestHelper.AddToken(VALID_TOKEN, VALID_USER_ID);
            TokenStoreTestHelper.AddToken(NEW_TOKEN_FOR_SAME_USER, VALID_USER_ID);

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => authenticator.AuthenticateOrThrow(VALID_TOKEN));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.TOKEN_INVALID,
                ReportConstants.MessageKey.TOKEN_INVALID);
        }

        [TestMethod]
        public void AuthenticateOrThrow_WhenTokenInCacheIsNull_ThrowsFault()
        {
            TokenStoreTestHelper.AddNullTokenToCache(VALID_TOKEN);

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => authenticator.AuthenticateOrThrow(VALID_TOKEN));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.TOKEN_INVALID,
                ReportConstants.MessageKey.TOKEN_INVALID);
        }


        private static class TokenStoreTestHelper
        {
            internal static void AddToken(string tokenValue, int userId)
            {
                AddTokenWithExpiry(tokenValue, userId, DateTime.UtcNow.AddMinutes(TOKEN_TTL_MINUTES));
            }

            internal static void AddTokenWithExpiry(string tokenValue, int userId, DateTime expiresAtUtc)
            {
                if (string.IsNullOrWhiteSpace(tokenValue))
                {
                    Assert.Fail("tokenValue must be non-empty.");
                }

                var token = new AuthToken
                {
                    Token = tokenValue,
                    UserId = userId,
                    ExpiresAtUtc = expiresAtUtc
                };

                TokenStore.StoreToken(token);
            }

            internal static void AddNullTokenToCache(string tokenValue)
            {
                if (string.IsNullOrWhiteSpace(tokenValue))
                {
                    Assert.Fail("tokenValue must be non-empty.");
                }

                TokenStore.Cache[tokenValue] = null;
            }
        }


        private static class TokenStoreCleaner
        {
            internal static void ClearAll()
            {
                TokenStore.Cache.Clear();
                TokenStore.ActiveTokenByUserId.Clear();
            }
        }

        private static class FaultAssert
        {
            internal static FaultException<ServiceFault> CaptureFault(Action action)
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

            internal static void AssertFault(
                FaultException<ServiceFault> fault,
                string expectedCode,
                string expectedMessageKey)
            {
                Assert.IsNotNull(fault);
                Assert.IsNotNull(fault.Detail);

                Assert.AreEqual(expectedCode, fault.Detail.Code);
                Assert.AreEqual(expectedMessageKey, fault.Detail.Message);
            }
        }
    }
}
