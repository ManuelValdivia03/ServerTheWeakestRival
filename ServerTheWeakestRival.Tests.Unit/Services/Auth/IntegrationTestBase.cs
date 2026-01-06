using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Fakes;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [DoNotParallelize]
    public abstract class IntegrationTestBase
    {
        private const string EMAIL_DOMAIN = "example.com";

        protected FakeEmailService emailService;
        protected ServicesTheWeakestRival.Server.Services.AuthService service;
        protected string testEmail;

        [TestInitialize]
        public void TestInitialize()
        {
            TestConfigBootstrapper.EnsureLoaded();

            DbTestCleaner.CleanupAll();
            TokenStoreTestCleaner.ClearAllTokens();

            emailService = new FakeEmailService();
            service = new ServicesTheWeakestRival.Server.Services.AuthService(
                new PasswordService(AuthServiceConstants.PASSWORD_MIN_LENGTH),
                emailService);

            testEmail = CreateUniqueEmail();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DbTestCleaner.CleanupAll();
            TokenStoreTestCleaner.ClearAllTokens();
        }

        protected static string CreateUniqueEmail()
        {
            return string.Format("test_{0}@{1}", Guid.NewGuid().ToString("N"), EMAIL_DOMAIN);
        }
    }
}
