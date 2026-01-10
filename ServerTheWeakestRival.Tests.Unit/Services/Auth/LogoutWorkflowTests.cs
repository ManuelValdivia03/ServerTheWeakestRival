using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using System;

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

        [TestMethod]
        public void Execute_WhenRequestIsNull_DoesNothing()
        {
            string email = BuildEmail("nullrequest");
            int userId = CreateAccount(email);

            string token = LoginAndGetToken(email);

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

            string token = LoginAndGetToken(email);

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

            string token = LoginAndGetToken(email);

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

            string token = LoginAndGetToken(email);

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

            var loginWorkflow = new LoginWorkflow(authRepository, passwordPolicy);
            LoginResponse login = loginWorkflow.Execute(new LoginRequest { Email = email, Password = PASSWORD });

            Assert.IsNotNull(login);
            Assert.IsNotNull(login.Token);
            Assert.AreEqual(userId, login.Token.UserId);

            bool before = AuthServiceContext.TryGetUserId(login.Token.Token, out int resolvedBefore);
            Assert.IsTrue(before);
            Assert.AreEqual(userId, resolvedBefore);

            var workflow = new LogoutWorkflow(authRepository);

            workflow.Execute(new LogoutRequest { Token = login.Token.Token });

            bool after = AuthServiceContext.TryGetUserId(login.Token.Token, out int resolvedAfter);
            Assert.IsFalse(after);
            Assert.AreEqual(0, resolvedAfter);
        }

        [TestMethod]
        public void Execute_WhenCalledTwice_IsIdempotent()
        {
            string email = BuildEmail("idempotent");
            CreateAccount(email);

            string token = LoginAndGetToken(email);

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

            string token = LoginAndGetToken(email);

            var workflow = new LogoutWorkflow(authRepository);
            workflow.Execute(new LogoutRequest { Token = token });

            var loginWorkflow = new LoginWorkflow(authRepository, passwordPolicy);
            LoginResponse second = loginWorkflow.Execute(new LoginRequest { Email = email, Password = PASSWORD });

            Assert.IsNotNull(second);
            Assert.IsNotNull(second.Token);
            Assert.AreEqual(userId, second.Token.UserId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(second.Token.Token));
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
            string passwordHash = passwordService.Hash(PASSWORD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(null, null));

            return authRepository.CreateAccountAndUser(data);
        }

        private string LoginAndGetToken(string email)
        {
            var loginWorkflow = new LoginWorkflow(authRepository, passwordPolicy);

            LoginResponse login = loginWorkflow.Execute(new LoginRequest
            {
                Email = email,
                Password = PASSWORD
            });

            Assert.IsNotNull(login);
            Assert.IsNotNull(login.Token);
            Assert.IsFalse(string.IsNullOrWhiteSpace(login.Token.Token));

            return login.Token.Token;
        }
    }
}
