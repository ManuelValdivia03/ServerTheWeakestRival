using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Fakes;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Email;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    public abstract class AuthTestBase
    {
        protected FakeEmailService fakeEmailService;

        protected AuthRepository authRepository;

        protected PasswordService passwordService;
        protected PasswordPolicy passwordPolicy;

        protected AuthEmailDispatcher emailDispatcher;

        [TestInitialize]
        public void TestInitialize()
        {
            DbTestCleaner.CleanupAll();
            TokenStoreTestCleaner.ClearAllTokens();

            fakeEmailService = new FakeEmailService();

            passwordService = new PasswordService(AuthServiceConstants.PASSWORD_MIN_LENGTH);
            passwordPolicy = new PasswordPolicy(passwordService);

            authRepository = new AuthRepository(DbTestConfig.GetMainConnectionString);

            emailDispatcher = new AuthEmailDispatcher(fakeEmailService);

            OnAfterInitialize();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            try
            {
                OnBeforeCleanup();
                TokenStoreTestCleaner.ClearAllTokens();
            }
            finally
            {
                DbTestCleaner.CleanupAll();
            }
        }

        protected virtual void OnAfterInitialize()
        {
        }

        protected virtual void OnBeforeCleanup()
        {
        }
    }
}
