using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class CompletePasswordResetWorkflowTests : AuthTestBase
    {
        private const string EMAIL_DOMAIN = "@test.local";

        private const string DISPLAY_NAME = "Test User";

        private const string PASSWORD_OLD = "Password123!";
        private const string PASSWORD_NEW_STRONG = "NewPassword123!";
        private const string PASSWORD_NEW_WEAK = "short";

        private const string CODE_VALID = "111111";
        private const string CODE_INVALID = "222222";

        private const int EXPIRED_MINUTES_OFFSET = -1;
        private const int ACTIVE_MINUTES_OFFSET = 10;

        private const string SQL_SELECT_RESET_ATTEMPTS = @"
            SELECT Attempts
            FROM dbo.PasswordResetRequests
            WHERE Id = @Id;";

        private const int ATTEMPTS_FIRST = 1;
        private const int ATTEMPTS_SECOND = 2;

        private const string WHITESPACE = " ";

        private const int RESET_INSERT_DELAY_MILLISECONDS = 30;

        [TestMethod]
        public void Execute_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenRequiredFieldsMissing_ThrowsInvalidRequest()
        {
            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = " ",
                Code = CODE_VALID,
                NewPassword = PASSWORD_NEW_STRONG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_COMPLETE_RESET_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenNewPasswordIsWeak_ThrowsWeakPassword()
        {
            string email = BuildEmail("weakpwd");
            CreateAccount(email);
            CreateResetRequest(email, CODE_VALID, DateTime.UtcNow.AddMinutes(ACTIVE_MINUTES_OFFSET));

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_VALID,
                NewPassword = PASSWORD_NEW_WEAK
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_WEAK_PASSWORD, fault.Code);

            string expectedMessage = string.Format(
                AuthServiceConstants.MESSAGE_PASSWORD_MIN_LENGTH_NOT_MET,
                AuthServiceConstants.PASSWORD_MIN_LENGTH);

            Assert.AreEqual(expectedMessage, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenNoPendingReset_ThrowsCodeMissing()
        {
            string email = BuildEmail("missing");

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_VALID,
                NewPassword = PASSWORD_NEW_STRONG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_MISSING, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_MISSING, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenResetExpired_ThrowsCodeExpired()
        {
            string email = BuildEmail("expired");
            CreateAccount(email);

            CreateResetRequest(email, CODE_VALID, DateTime.UtcNow.AddMinutes(EXPIRED_MINUTES_OFFSET));

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_VALID,
                NewPassword = PASSWORD_NEW_STRONG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_EXPIRED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenResetAlreadyUsed_ThrowsCodeExpired()
        {
            string email = BuildEmail("used");
            CreateAccount(email);

            int resetId = CreateResetRequest(email, CODE_VALID, DateTime.UtcNow.AddMinutes(ACTIVE_MINUTES_OFFSET));
            authRepository.MarkResetUsed(resetId);

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_VALID,
                NewPassword = PASSWORD_NEW_STRONG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_EXPIRED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenCodeInvalid_ThrowsCodeInvalid()
        {
            string email = BuildEmail("invalidcode");
            CreateAccount(email);

            CreateResetRequest(email, CODE_VALID, DateTime.UtcNow.AddMinutes(ACTIVE_MINUTES_OFFSET));

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_INVALID,
                NewPassword = PASSWORD_NEW_STRONG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_INVALID, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_INVALID, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailNotRegistered_ThrowsEmailNotFound()
        {
            string email = BuildEmail("emailnotfound");

            CreateResetRequest(email, CODE_VALID, DateTime.UtcNow.AddMinutes(ACTIVE_MINUTES_OFFSET));

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_VALID,
                NewPassword = PASSWORD_NEW_STRONG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_NOT_FOUND, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_NOT_REGISTERED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenSuccess_UpdatesPassword_AndMarksResetUsed()
        {
            string email = BuildEmail("success");
            CreateAccount(email);

            int resetId = CreateResetRequest(email, CODE_VALID, DateTime.UtcNow.AddMinutes(ACTIVE_MINUTES_OFFSET));

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = "  " + email + "  ",
                Code = CODE_VALID,
                NewPassword = PASSWORD_NEW_STRONG
            };

            workflow.Execute(request);

            LoginLookupResult loginRow = authRepository.GetAccountForLogin(email);
            Assert.IsTrue(loginRow.Found);

            bool passwordChanged = passwordService.Verify(PASSWORD_NEW_STRONG, loginRow.Account.PasswordHash);
            Assert.IsTrue(passwordChanged);

            ResetLookupResult after = authRepository.ReadLatestReset(email);
            Assert.IsTrue(after.Found);
            Assert.AreEqual(resetId, after.Reset.Id);
            Assert.IsTrue(after.Reset.Used);
        }

        [TestMethod]
        public void Execute_WhenCodeIsWhitespace_ThrowsInvalidRequest()
        {
            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = BuildEmail("missingcode"),
                Code = WHITESPACE,
                NewPassword = PASSWORD_NEW_STRONG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_COMPLETE_RESET_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenNewPasswordIsWhitespace_ThrowsInvalidRequest()
        {
            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = BuildEmail("missingnewpwd"),
                Code = CODE_VALID,
                NewPassword = WHITESPACE
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_COMPLETE_RESET_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenCodeInvalid_IncrementsAttempts_AndDoesNotMarkUsed_AndDoesNotChangePassword()
        {
            string email = BuildEmail("invalid_attempts");
            CreateAccount(email);

            int resetId = CreateResetRequest(email, CODE_VALID, DateTime.UtcNow.AddMinutes(ACTIVE_MINUTES_OFFSET));

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_INVALID,
                NewPassword = PASSWORD_NEW_STRONG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_INVALID, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_INVALID, fault.Message);

            int attempts = ReadResetAttempts(resetId);
            Assert.AreEqual(ATTEMPTS_FIRST, attempts);

            ResetLookupResult afterReset = authRepository.ReadLatestReset(email);
            Assert.IsTrue(afterReset.Found);
            Assert.AreEqual(resetId, afterReset.Reset.Id);
            Assert.IsFalse(afterReset.Reset.Used);

            LoginLookupResult loginRow = authRepository.GetAccountForLogin(email);
            Assert.IsTrue(loginRow.Found);

            bool oldPasswordStillValid = passwordService.Verify(PASSWORD_OLD, loginRow.Account.PasswordHash);
            Assert.IsTrue(oldPasswordStillValid);
        }

        [TestMethod]
        public void Execute_WhenCodeInvalidTwice_IncrementsAttemptsTwice()
        {
            string email = BuildEmail("invalid_twice");
            CreateAccount(email);

            int resetId = CreateResetRequest(email, CODE_VALID, DateTime.UtcNow.AddMinutes(ACTIVE_MINUTES_OFFSET));

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_INVALID,
                NewPassword = PASSWORD_NEW_STRONG
            };

            FaultAssert.Capture(() => workflow.Execute(request));
            FaultAssert.Capture(() => workflow.Execute(request));

            int attempts = ReadResetAttempts(resetId);
            Assert.AreEqual(ATTEMPTS_SECOND, attempts);
        }

        [TestMethod]
        public void Execute_WhenResetExpired_DoesNotIncrementAttempts()
        {
            string email = BuildEmail("expired_no_attempts");
            CreateAccount(email);

            int resetId = CreateResetRequest(email, CODE_VALID, DateTime.UtcNow.AddMinutes(EXPIRED_MINUTES_OFFSET));

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_INVALID,
                NewPassword = PASSWORD_NEW_STRONG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_EXPIRED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED, fault.Message);

            int attempts = ReadResetAttempts(resetId);
            Assert.AreEqual(0, attempts);
        }

        [TestMethod]
        public void Execute_WhenSuccess_ThenReusingSameCode_ThrowsCodeExpired()
        {
            string email = BuildEmail("reuse");
            CreateAccount(email);

            CreateResetRequest(email, CODE_VALID, DateTime.UtcNow.AddMinutes(ACTIVE_MINUTES_OFFSET));

            var workflow = new CompletePasswordResetWorkflow(authRepository, passwordPolicy, passwordService);

            workflow.Execute(new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_VALID,
                NewPassword = PASSWORD_NEW_STRONG
            });

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(new CompletePasswordResetRequest
            {
                Email = email,
                Code = CODE_VALID,
                NewPassword = "AnotherPassword123!"
            }));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_EXPIRED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED, fault.Message);
        }

        private int ReadResetAttempts(int resetId)
        {
            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(SQL_SELECT_RESET_ATTEMPTS, connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;

                connection.Open();

                object result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(result);
            }
        }

        private static string BuildEmail(string prefix)
        {
            return string.Concat(
                "tc.completepasswordreset.",
                prefix,
                ".",
                Guid.NewGuid().ToString("N"),
                EMAIL_DOMAIN);
        }

        private void CreateAccount(string email)
        {
            string passwordHash = passwordService.Hash(PASSWORD_OLD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(null, null));

            authRepository.CreateAccountAndUser(data);
        }

        private int CreateResetRequest(string email, string code, DateTime expiresAtUtc)
        {
            byte[] codeHash = SecurityUtil.Sha256(code);

            authRepository.CreatePasswordResetRequest(email, codeHash, expiresAtUtc);

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(lookup.Found);

            return lookup.Reset.Id;
        }
    }
}
