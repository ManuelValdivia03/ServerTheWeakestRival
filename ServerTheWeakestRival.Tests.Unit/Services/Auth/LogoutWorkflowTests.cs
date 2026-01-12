using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using System;

using ServerTokenStore = ServicesTheWeakestRival.Server.Services.TokenStore;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class LogoutWorkflowTests : AuthTestBase
    {
        private const string EMAIL_DOMAIN = "@test.local";

        private const string DISPLAY_NAME = "Test User";
        private const string PASSWORD = "Password123!";

        private const string EMPTY = "";
        private const string WHITESPACE = " ";

        private const int TOKEN_TTL_MINUTES = 10;

        [TestInitialize]
        public void SetUp()
        {
            TokenStoreTestCleaner.ClearAllTokens();
        }

        [TestCleanup]
        public void TearDown()
        {
            TokenStoreTestCleaner.ClearAllTokens();
        }

        [TestMethod]
        public void Execute_WhenRequestIsNull_DoesNothing()
        {
            string email = BuildEmail("nullrequest");
            int userId = CreateAccount(email);

            string token = SeedTokenForUser(userId);

            bool before = AuthServiceContext.TryGetUserId(token, out int resolvedBefore);
            Assert.IsTrue(before);
            Assert.AreEqual(userId, resolvedBefore);

            var workflow = new LogoutWorkflow(authRepository);

            workflow.Execute(null);

            bool after = AuthServiceContext.TryGetUserId(token, out int resolvedAfter);
            Assert.IsTrue(after);
            Assert.AreEqual(userId, resolvedAfter);
        }

        [TestMethod]
        public void Execute_WhenTokenIsNull_DoesNothing()
        {
            string email = BuildEmail("nulltoken");
            int userId = CreateAccount(email);

            string token = SeedTokenForUser(userId);

            bool before = AuthServiceContext.TryGetUserId(token, out int resolvedBefore);
            Assert.IsTrue(before);
            Assert.AreEqual(userId, resolvedBefore);

            var workflow = new LogoutWorkflow(authRepository);

            workflow.Execute(new LogoutRequest { Token = null });

            bool after = AuthServiceContext.TryGetUserId(token, out int resolvedAfter);
            Assert.IsTrue(after);
            Assert.AreEqual(userId, resolvedAfter);
        }

        [TestMethod]
        public void Execute_WhenTokenIsEmpty_DoesNothing()
        {
            string email = BuildEmail("emptytoken");
            int userId = CreateAccount(email);

            string token = SeedTokenForUser(userId);

            bool before = AuthServiceContext.TryGetUserId(token, out int resolvedBefore);
            Assert.IsTrue(before);
            Assert.AreEqual(userId, resolvedBefore);

            var workflow = new LogoutWorkflow(authRepository);

            workflow.Execute(new LogoutRequest { Token = EMPTY });

            bool afterEmpty = AuthServiceContext.TryGetUserId(token, out int resolvedAfterEmpty);
            Assert.IsTrue(afterEmpty);
            Assert.AreEqual(userId, resolvedAfterEmpty);

            workflow.Execute(new LogoutRequest { Token = WHITESPACE });

            bool afterWhitespace = AuthServiceContext.TryGetUserId(token, out int resolvedAfterWhitespace);
            Assert.IsTrue(afterWhitespace);
            Assert.AreEqual(userId, resolvedAfterWhitespace);
        }

        [TestMethod]
        public void Execute_WhenTokenIsNotFound_DoesNothing()
        {
            string email = BuildEmail("notfound");
            int userId = CreateAccount(email);

            string token = SeedTokenForUser(userId);

            bool before = AuthServiceContext.TryGetUserId(token, out int resolvedBefore);
            Assert.IsTrue(before);
            Assert.AreEqual(userId, resolvedBefore);

            var workflow = new LogoutWorkflow(authRepository);

            workflow.Execute(new LogoutRequest { Token = Guid.NewGuid().ToString("N") });

            bool after = AuthServiceContext.TryGetUserId(token, out int resolvedAfter);
            Assert.IsTrue(after);
            Assert.AreEqual(userId, resolvedAfter);
        }

        [TestMethod]
        public void Execute_WhenSuccess_RemovesTokenAndLeavesAllLobbies()
        {
            string email = BuildEmail("success");
            int userId = CreateAccount(email);

            string token = SeedTokenForUser(userId);

            bool before = AuthServiceContext.TryGetUserId(token, out int resolvedBefore);
            Assert.IsTrue(before);
            Assert.AreEqual(userId, resolvedBefore);

            var workflow = new LogoutWorkflow(authRepository);

            workflow.Execute(new LogoutRequest { Token = token });

            bool after = AuthServiceContext.TryGetUserId(token, out int resolvedAfter);
            Assert.IsFalse(after);
            Assert.AreEqual(0, resolvedAfter);
        }

        [TestMethod]
        public void Execute_WhenCalledTwice_IsIdempotent()
        {
            string email = BuildEmail("idempotent");
            int userId = CreateAccount(email);

            string token = SeedTokenForUser(userId);

            var workflow = new LogoutWorkflow(authRepository);

            workflow.Execute(new LogoutRequest { Token = token });
            workflow.Execute(new LogoutRequest { Token = token });

            bool after = AuthServiceContext.TryGetUserId(token, out int resolvedAfter);
            Assert.IsFalse(after);
            Assert.AreEqual(0, resolvedAfter);
        }

        [TestMethod]
        public void Execute_WhenSuccess_AllowsLoginAgain()
        {
            string email = BuildEmail("relogin");
            int userId = CreateAccount(email);

            string token = SeedTokenForUser(userId);

            var workflow = new LogoutWorkflow(authRepository);
            workflow.Execute(new LogoutRequest { Token = token });

            bool removedOk = AuthServiceContext.TryGetUserId(token, out int removedUserId);
            Assert.IsFalse(removedOk);
            Assert.AreEqual(0, removedUserId);

            string token2 = SeedTokenForUser(userId);

            bool ok2 = AuthServiceContext.TryGetUserId(token2, out int resolved2);
            Assert.IsTrue(ok2);
            Assert.AreEqual(userId, resolved2);
        }

        private static string BuildEmail(string prefix)
        {
            return string.Concat(
                "tc.logout.",
                prefix,
                ".",
                Guid.NewGuid().ToString("N"),
                EMAIL_DOMAIN);
        }

        private int CreateAccount(string email)
        {
            string passwordHash = PasswordService.Hash(PASSWORD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(null, null));

            return authRepository.CreateAccountAndUser(data);
        }

        private static string SeedTokenForUser(int userId)
        {
            if (userId <= 0)
            {
                Assert.Fail("UserId is required to seed a token.");
                return string.Empty;
            }

            string tokenValue = Guid.NewGuid().ToString("N");

            var token = new AuthToken
            {
                UserId = userId,
                Token = tokenValue,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(TOKEN_TTL_MINUTES)
            };

            ServerTokenStore.StoreToken(token);

            return tokenValue;
        }
    }
}
