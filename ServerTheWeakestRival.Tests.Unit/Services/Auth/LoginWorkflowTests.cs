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
    public sealed class LoginWorkflowTests : AuthTestBase
    {
        private const string EMAIL_DOMAIN = "@test.local";

        private const string DISPLAY_NAME = "Test User";

        private const string PASSWORD_CORRECT = "Password123!";
        private const string PASSWORD_WRONG = "WrongPassword123!";

        private const string WHITESPACE = " ";
        private const string PASSWORD_WITH_SPACES = "  " + PASSWORD_CORRECT + "  ";

        private const int EXPIRED_MINUTES_OFFSET = -1;

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

        [TestMethod]
        public void Execute_WhenSuccess_IssuesTokenAndTokenIsValid()
        {
            string email = BuildEmail("success");
            int userId = CreateAccount(email, PASSWORD_CORRECT);

            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            var request = new LoginRequest
            {
                Email = "  " + email + "  ",
                Password = PASSWORD_CORRECT
            };

            LoginResponse response = workflow.Execute(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Token);

            Assert.AreEqual(userId, response.Token.UserId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Token.Token));
            Assert.IsTrue(response.Token.ExpiresAtUtc > DateTime.UtcNow);

            bool ok = AuthServiceContext.TryGetUserId(response.Token.Token, out int resolvedUserId);
            Assert.IsTrue(ok);
            Assert.AreEqual(userId, resolvedUserId);
        }

        [TestMethod]
        public void Execute_WhenAlreadyLoggedIn_ThrowsAlreadyLoggedIn()
        {
            string email = BuildEmail("already");
            CreateAccount(email, PASSWORD_CORRECT);

            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            workflow.Execute(new LoginRequest
            {
                Email = email,
                Password = PASSWORD_CORRECT
            });

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(new LoginRequest
            {
                Email = email,
                Password = PASSWORD_CORRECT
            }));

            Assert.AreEqual(AuthServiceConstants.ERROR_ALREADY_LOGGED_IN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ALREADY_LOGGED_IN, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenTokenRemoved_AllowsLoginAgain()
        {
            string email = BuildEmail("relogin_after_remove");
            int userId = CreateAccount(email, PASSWORD_CORRECT);

            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            LoginResponse first = workflow.Execute(new LoginRequest
            {
                Email = email,
                Password = PASSWORD_CORRECT
            });

            bool removedOk = AuthServiceContext.TryRemoveToken(first.Token.Token, out AuthToken removed);
            Assert.IsTrue(removedOk);
            Assert.IsNotNull(removed);
            Assert.AreEqual(first.Token.Token, removed.Token);

            LoginResponse second = workflow.Execute(new LoginRequest
            {
                Email = email,
                Password = PASSWORD_CORRECT
            });

            Assert.IsNotNull(second);
            Assert.IsNotNull(second.Token);
            Assert.AreEqual(userId, second.Token.UserId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(second.Token.Token));
        }

        [TestMethod]
        public void Execute_WhenExistingTokenIsExpired_AllowsLoginAgain()
        {
            string email = BuildEmail("relogin_after_expire");
            CreateAccount(email, PASSWORD_CORRECT);

            var workflow = new LoginWorkflow(authRepository, passwordPolicy);

            LoginResponse first = workflow.Execute(new LoginRequest
            {
                Email = email,
                Password = PASSWORD_CORRECT
            });

            first.Token.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(EXPIRED_MINUTES_OFFSET);

            LoginResponse second = workflow.Execute(new LoginRequest
            {
                Email = email,
                Password = PASSWORD_CORRECT
            });

            Assert.IsNotNull(second);
            Assert.IsNotNull(second.Token);
            Assert.IsFalse(string.IsNullOrWhiteSpace(second.Token.Token));
            Assert.IsTrue(second.Token.ExpiresAtUtc > DateTime.UtcNow);
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
