using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Validation;
using System.Reflection;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class AuthRequestValidatorTests : AuthTestBase
    {
        private const string EMAIL_RAW = "  user@test.local  ";
        private const string EMAIL_TRIMMED = "user@test.local";
        private const string EMAIL_WITH_TABS = "\tuser@test.local\t";

        private const string PASSWORD_VALID = "Password123!";
        private const int USER_ID_VALID = 1;
        private const int USER_ID_INVALID_ZERO = 0;
        private const int USER_ID_INVALID_NEGATIVE = -1;
        private const int TOKEN_TEST_USER_ID = 1001;
        private const int EXPIRED_MINUTES = 1;

        private const string TOKEN_STORE_TYPE_NAME = "ServicesTheWeakestRival.Server.Services.TokenStore";
        private const string TOKEN_STORE_FIELD_CACHE = "Cache";
        private const string TOKEN_STORE_FIELD_ACTIVE_BY_USER = "ActiveTokenByUserId";


        [TestMethod]
        public void NormalizeRequiredEmail_WhenEmailIsNull_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.NormalizeRequiredEmail(null, AuthServiceConstants.MESSAGE_EMAIL_REQUIRED));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void NormalizeRequiredEmail_WhenEmailIsWhitespace_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.NormalizeRequiredEmail(" ", AuthServiceConstants.MESSAGE_EMAIL_REQUIRED));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void NormalizeRequiredEmail_WhenEmailIsEmpty_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.NormalizeRequiredEmail(string.Empty, AuthServiceConstants.MESSAGE_EMAIL_REQUIRED));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void NormalizeRequiredEmail_WhenEmailHasTabs_ReturnsTrimmed()
        {
            string normalized = AuthRequestValidator.NormalizeRequiredEmail(
                EMAIL_WITH_TABS,
                AuthServiceConstants.MESSAGE_EMAIL_REQUIRED);

            Assert.AreEqual(EMAIL_TRIMMED, normalized);
        }

        [TestMethod]
        public void NormalizeRequiredEmail_WhenEmailIsValid_ReturnsTrimmed()
        {
            string normalized = AuthRequestValidator.NormalizeRequiredEmail(
                EMAIL_RAW,
                AuthServiceConstants.MESSAGE_EMAIL_REQUIRED);

            Assert.AreEqual(EMAIL_TRIMMED, normalized);
        }

        [TestMethod]
        public void EnsureCodeNotExpired_WhenUsedTrue_ThrowsCodeExpired()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureCodeNotExpired(
                    DateTime.UtcNow.AddMinutes(5),
                    used: true,
                    expiredMessage: AuthServiceConstants.MESSAGE_VERIFICATION_CODE_EXPIRED));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_EXPIRED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_EXPIRED, fault.Message);
        }

        [TestMethod]
        public void EnsureCodeNotExpired_WhenExpired_ThrowsCodeExpired()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureCodeNotExpired(
                    DateTime.UtcNow.AddMinutes(-1),
                    used: false,
                    expiredMessage: AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_EXPIRED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED, fault.Message);
        }

        [TestMethod]
        public void EnsureCodeNotExpired_WhenExpiresNow_ThrowsCodeExpired()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureCodeNotExpired(
                    DateTime.UtcNow,
                    used: false,
                    expiredMessage: AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_EXPIRED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED, fault.Message);
        }

        [TestMethod]
        public void EnsureCodeNotExpired_WhenNotExpired_DoesNotThrow()
        {
            AuthRequestValidator.EnsureCodeNotExpired(
                DateTime.UtcNow.AddMinutes(5),
                used: false,
                expiredMessage: AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void EnsureValidUserIdOrThrow_WhenUserIdInvalidZero_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureValidUserIdOrThrow(USER_ID_INVALID_ZERO));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_USER_ID_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void EnsureValidUserIdOrThrow_WhenUserIdInvalidNegative_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureValidUserIdOrThrow(USER_ID_INVALID_NEGATIVE));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_USER_ID_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void EnsureValidUserIdOrThrow_WhenUserIdValid_DoesNotThrow()
        {
            AuthRequestValidator.EnsureValidUserIdOrThrow(USER_ID_VALID);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void EnsureValidSessionOrThrow_WhenTokenInvalid_ThrowsInvalidSession()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureValidSessionOrThrow(Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT)));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void EnsureValidSessionOrThrow_WhenTokenIsNull_ThrowsInvalidSession()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureValidSessionOrThrow(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void EnsureValidSessionOrThrow_WhenTokenIsEmpty_ThrowsInvalidSession()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureValidSessionOrThrow(string.Empty));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void EnsureValidSessionOrThrow_WhenTokenIsWhitespace_ThrowsInvalidSession()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureValidSessionOrThrow("   "));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void EnsureValidSessionOrThrow_WhenTokenHasOuterWhitespace_ThrowsInvalidSession()
        {
            string email = "tc.validator." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT) + "@test.local";
            CreateAccountForAuthValidator(email);

            string tokenValue = LoginAndGetTokenForAuthValidator(email);
            string paddedToken = " " + tokenValue + " ";

            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureValidSessionOrThrow(paddedToken));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void EnsureValidSessionOrThrow_WhenTokenValid_DoesNotThrow()
        {
            string email = "tc.validator." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT) + "@test.local";
            CreateAccountForAuthValidator(email);

            string tokenValue = LoginAndGetTokenForAuthValidator(email);

            AuthRequestValidator.EnsureValidSessionOrThrow(tokenValue);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void EnsureValidSessionOrThrow_WhenTokenIsExpired_ThrowsInvalidSession()
        {
            TokenStoreTestCleaner.ClearAllTokens();

            string tokenValue = "expired." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);

            var expiredToken = new AuthToken
            {
                UserId = TOKEN_TEST_USER_ID,
                Token = tokenValue,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-EXPIRED_MINUTES)
            };

            SeedTokenStore(expiredToken, activeTokenValue: tokenValue);

            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureValidSessionOrThrow(tokenValue));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);

            bool okAfter = AuthServiceContext.TryGetUserId(tokenValue, out int userIdAfter);
            Assert.IsFalse(okAfter);
            Assert.AreEqual(0, userIdAfter);

            TokenStoreTestCleaner.ClearAllTokens();
        }

        [TestMethod]
        public void EnsureValidSessionOrThrow_WhenTokenIsNotActiveForUser_ThrowsInvalidSession()
        {
            TokenStoreTestCleaner.ClearAllTokens();

            string staleTokenValue = "stale." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);
            string activeTokenValue = "active." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);

            var staleToken = new AuthToken
            {
                UserId = TOKEN_TEST_USER_ID,
                Token = staleTokenValue,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(AuthServiceConstants.TOKEN_TTL_HOURS)
            };

            SeedTokenStore(staleToken, activeTokenValue: activeTokenValue);

            ServiceFault fault = FaultAssert.Capture(() =>
                AuthRequestValidator.EnsureValidSessionOrThrow(staleTokenValue));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);

            bool okAfter = AuthServiceContext.TryGetUserId(staleTokenValue, out int userIdAfter);
            Assert.IsFalse(okAfter);
            Assert.AreEqual(0, userIdAfter);

            TokenStoreTestCleaner.ClearAllTokens();
        }

        private void CreateAccountForAuthValidator(string email)
        {
            string passwordHash = passwordService.Hash(PASSWORD_VALID);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                "Test",
                new ProfileImagePayload(null, null));

            authRepository.CreateAccountAndUser(data);
        }

        private string LoginAndGetTokenForAuthValidator(string email)
        {
            var login = new ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows.LoginWorkflow(authRepository, passwordPolicy)
                .Execute(new ServicesTheWeakestRival.Contracts.Data.LoginRequest
                {
                    Email = email,
                    Password = PASSWORD_VALID
                });

            return login.Token.Token;
        }

        private static void SeedTokenStore(AuthToken token, string activeTokenValue)
        {
            if (token == null)
            {
                return;
            }

            Type tokenStoreType = typeof(ServicesTheWeakestRival.Server.Services.AuthService).Assembly
                .GetType(TOKEN_STORE_TYPE_NAME, throwOnError: false);

            if (tokenStoreType == null)
            {
                Assert.Fail("TokenStore type was not found: " + TOKEN_STORE_TYPE_NAME);
                return;
            }

            object cache = GetStaticFieldValue(tokenStoreType, TOKEN_STORE_FIELD_CACHE);
            object activeByUser = GetStaticFieldValue(tokenStoreType, TOKEN_STORE_FIELD_ACTIVE_BY_USER);

            InvokeTryAdd(cache, token.Token, token);
            SetIndexerValue(activeByUser, token.UserId, activeTokenValue);
        }

        private static object GetStaticFieldValue(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                Assert.Fail("TokenStore field was not found: " + fieldName);
                return null;
            }

            return field.GetValue(null);
        }

        private static void InvokeTryAdd(object dictionary, object key, object value)
        {
            if (dictionary == null)
            {
                Assert.Fail("TokenStore.Cache instance is null.");
                return;
            }

            MethodInfo tryAdd = dictionary.GetType().GetMethod("TryAdd", BindingFlags.Instance | BindingFlags.Public);
            if (tryAdd == null)
            {
                Assert.Fail("TryAdd method was not found in TokenStore.Cache.");
                return;
            }

            tryAdd.Invoke(dictionary, new[] { key, value });
        }

        private static void SetIndexerValue(object dictionary, object key, object value)
        {
            if (dictionary == null)
            {
                Assert.Fail("TokenStore.ActiveTokenByUserId instance is null.");
                return;
            }

            PropertyInfo indexer = dictionary.GetType().GetProperty("Item", BindingFlags.Instance | BindingFlags.Public);
            if (indexer == null)
            {
                Assert.Fail("Indexer property was not found in TokenStore.ActiveTokenByUserId.");
                return;
            }

            indexer.SetValue(dictionary, value, new[] { key });
        }
    }
}
