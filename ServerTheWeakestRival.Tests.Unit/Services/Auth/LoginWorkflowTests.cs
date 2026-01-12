using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using System;
using System.Data;
using System.Data.SqlClient;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    [DoNotParallelize]
    public sealed class LoginWorkflowTests : AuthTestBase
    {
        private const string EMAIL_DOMAIN = "@test.local";

        private const string DISPLAY_NAME = "Test User";

        private const string PASSWORD_CORRECT = "Password123!";
        private const string PASSWORD_WRONG = "WrongPassword123!";

        private const string WHITESPACE = " ";
        private const string PASSWORD_WITH_SPACES = "  " + PASSWORD_CORRECT + "  ";

        [TestInitialize]
        public void SetUp()
        {
            ResetAuthState();
        }

        [TestCleanup]
        public void TearDown()
        {
            ResetAuthState();
        }

        [TestMethod]
        public void Execute_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailIsNull_ThrowsInvalidCredentials()
        {
            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = null,
                Password = PASSWORD_CORRECT
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailIsEmpty_ThrowsInvalidCredentials()
        {
            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = WHITESPACE,
                Password = PASSWORD_CORRECT
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenPasswordIsNull_ThrowsInvalidCredentials()
        {
            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = BuildEmail("nullpwd"),
                Password = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenPasswordIsEmpty_ThrowsInvalidCredentials()
        {
            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = BuildEmail("emptypwd"),
                Password = WHITESPACE
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailHasSpacesButAccountNotFound_ThrowsInvalidCredentials()
        {
            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            string email = BuildEmail("notfoundspaces");

            var request = new LoginRequest
            {
                Email = "  " + email + "  ",
                Password = PASSWORD_CORRECT
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenAccountNotFound_ThrowsInvalidCredentials()
        {
            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = BuildEmail("notfound"),
                Password = PASSWORD_CORRECT
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenPasswordIncorrect_ThrowsInvalidCredentials()
        {
            string email = BuildEmail("wrongpwd");
            CreateAccount(email, PASSWORD_CORRECT);

            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = email,
                Password = PASSWORD_WRONG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenPasswordHasLeadingTrailingSpaces_ThrowsInvalidCredentials()
        {
            string email = BuildEmail("pwdspaces");
            CreateAccount(email, PASSWORD_CORRECT);

            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = email,
                Password = PASSWORD_WITH_SPACES
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenAccountIsInactive_ThrowsAccountInactive()
        {
            string email = BuildEmail("inactive");
            CreateAccount(email, PASSWORD_CORRECT);

            UpdateAccountStatus(email, AuthServiceConstants.ACCOUNT_STATUS_INACTIVE);

            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = email,
                Password = PASSWORD_CORRECT
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_ACCOUNT_INACTIVE, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ACCOUNT_NOT_ACTIVE, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenAccountIsSuspended_ThrowsAccountSuspended()
        {
            string email = BuildEmail("suspended");
            CreateAccount(email, PASSWORD_CORRECT);

            UpdateAccountStatus(email, AuthServiceConstants.ACCOUNT_STATUS_SUSPENDED);

            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = email,
                Password = PASSWORD_CORRECT
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_ACCOUNT_SUSPENDED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ACCOUNT_SUSPENDED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenAccountIsBanned_ThrowsAccountBanned()
        {
            string email = BuildEmail("banned");
            CreateAccount(email, PASSWORD_CORRECT);

            UpdateAccountStatus(email, AuthServiceConstants.ACCOUNT_STATUS_BANNED);

            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = email,
                Password = PASSWORD_CORRECT
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_ACCOUNT_BANNED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ACCOUNT_BANNED, fault.Message);
        }

        private static string BuildEmail(string prefix)
        {
            return string.Concat(
                "tc.login.",
                prefix,
                ".",
                Guid.NewGuid().ToString("N"),
                EMAIL_DOMAIN);
        }

        private void ResetAuthState()
        {
            TokenStoreTestCleaner.ClearAllTokens();
            OnlineUserRegistryTestCleaner.ClearAll();
        }

        private int CreateAccount(string email, string password)
        {
            string passwordHash = PasswordService.Hash(password);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(null, null));

            return authRepository.CreateAccountAndUser(data);
        }

        private void UpdateAccountStatus(string email, byte status)
        {
            const string SQL_UPDATE_STATUS = @"
                UPDATE dbo.Accounts
                SET status = @Status
                WHERE email = @Email;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(SQL_UPDATE_STATUS, connection))
            {
                cmd.CommandType = CommandType.Text;

                cmd.Parameters.Add(
                    AuthServiceConstants.PARAMETER_EMAIL,
                    SqlDbType.NVarChar,
                    AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                cmd.Parameters.Add("@Status", SqlDbType.TinyInt).Value = status;

                connection.Open();

                int rows = cmd.ExecuteNonQuery();
                Assert.AreEqual(1, rows);
            }
        }
    }
}
