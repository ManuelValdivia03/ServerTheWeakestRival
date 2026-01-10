using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using System.Data;
using System.Data.SqlClient;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class RegisterWorkflowTests : AuthTestBase
    {
        private const string EMAIL_DOMAIN = "@test.local";

        private const string DISPLAY_NAME = "Test User";
        private const string DISPLAY_NAME_WITH_SPACES = "  Test User  ";

        private const string PASSWORD_STRONG = "Password123!";
        private const string PASSWORD_WEAK = "short";

        private static readonly byte[] EMPTY_IMAGE_BYTES = Array.Empty<byte>();
        private const string EMPTY_CONTENT_TYPE = "";

        [TestMethod]
        public void Execute_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenRequiredFieldsMissing_ThrowsInvalidRequest()
        {
            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new RegisterRequest
            {
                Email = " ",
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_REGISTER_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenPasswordIsWeak_ThrowsWeakPassword()
        {
            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new RegisterRequest
            {
                Email = BuildEmail("weakpwd"),
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_WEAK,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_WEAK_PASSWORD, fault.Code);

            string expectedMessage = string.Format(
                AuthServiceConstants.MESSAGE_PASSWORD_MIN_LENGTH_NOT_MET,
                AuthServiceConstants.PASSWORD_MIN_LENGTH);

            Assert.AreEqual(expectedMessage, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailAlreadyRegistered_ThrowsEmailTaken()
        {
            string email = BuildEmail("taken");
            CreateAccount(email);

            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new RegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_TAKEN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_TAKEN, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenSuccess_CreatesAccount_AndReturnsResponse()
        {
            string emailTrimmed = BuildEmail("success");
            string emailWithSpaces = "  " + emailTrimmed + "  ";

            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new RegisterRequest
            {
                Email = emailWithSpaces,
                DisplayName = DISPLAY_NAME_WITH_SPACES,
                Password = PASSWORD_STRONG,
                ProfileImageBytes = EMPTY_IMAGE_BYTES,
                ProfileImageContentType = EMPTY_CONTENT_TYPE
            };

            RegisterResponse response = workflow.Execute(request);

            Assert.IsNotNull(response);
            Assert.IsTrue(response.UserId > 0);

            Assert.IsNotNull(response.Token);
            Assert.AreEqual(response.UserId, response.Token.UserId);
            Assert.AreEqual(string.Empty, response.Token.Token);
            Assert.AreEqual(DateTime.MinValue, response.Token.ExpiresAtUtc);

            bool exists = authRepository.ExistsAccountByEmail(emailTrimmed);
            Assert.IsTrue(exists);
        }

        [TestMethod]
        public void Execute_WhenEmailIsNull_ThrowsInvalidRequest()
        {
            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new RegisterRequest
            {
                Email = null,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_REGISTER_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenDisplayNameIsWhitespace_ThrowsInvalidRequest()
        {
            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new RegisterRequest
            {
                Email = BuildEmail("nodisplay"),
                DisplayName = "   ",
                Password = PASSWORD_STRONG,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_REGISTER_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenPasswordIsWhitespace_ThrowsInvalidRequest()
        {
            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new RegisterRequest
            {
                Email = BuildEmail("nopwd"),
                DisplayName = DISPLAY_NAME,
                Password = "   ",
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_REGISTER_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenImageBytesProvidedButContentTypeMissing_ThrowsInvalidRequest()
        {
            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new RegisterRequest
            {
                Email = BuildEmail("img.ctype.missing"),
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                ProfileImageBytes = PngMinimalValidBytesForWorkflow,
                ProfileImageContentType = " "
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image content type is required.", fault.Message);
        }

        [TestMethod]
        public void Execute_WhenImageContentTypeUnsupported_ThrowsInvalidRequest()
        {
            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new RegisterRequest
            {
                Email = BuildEmail("img.unsupported"),
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                ProfileImageBytes = PngMinimalValidBytesForWorkflow,
                ProfileImageContentType = "image/gif"
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Only PNG and JPG profile images are allowed.", fault.Message);
        }

        [TestMethod]
        public void Execute_WhenImageSignatureMismatch_ThrowsInvalidRequest()
        {
            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var badPng = (byte[])PngMinimalValidBytesForWorkflow.Clone();
            badPng[0] = 0x00;

            var request = new RegisterRequest
            {
                Email = BuildEmail("img.sig.mismatch"),
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                ProfileImageBytes = badPng,
                ProfileImageContentType = ProfileImageConstants.CONTENT_TYPE_PNG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image file does not match the declared format.", fault.Message);
        }

        [TestMethod]
        public void Execute_WhenImageTooLarge_ThrowsInvalidRequest()
        {
            int maxBytes = ProfileImageConstants.DEFAULT_MAX_IMAGE_BYTES;

            byte[] tooLarge = new byte[maxBytes + 1];
            Array.Copy(PngMinimalValidBytesForWorkflow, tooLarge, PngMinimalValidBytesForWorkflow.Length);

            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new RegisterRequest
            {
                Email = BuildEmail("img.toolarge"),
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                ProfileImageBytes = tooLarge,
                ProfileImageContentType = ProfileImageConstants.CONTENT_TYPE_PNG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            StringAssert.Contains(fault.Message, "Profile image is too large");
        }

        [TestMethod]
        public void Execute_WhenSuccess_PersistsTrimmedDisplayName()
        {
            string emailTrimmed = BuildEmail("persist.display");
            string emailWithSpaces = "  " + emailTrimmed + "  ";

            var workflow = new RegisterWorkflow(authRepository, passwordPolicy, passwordService);

            RegisterResponse response = workflow.Execute(new RegisterRequest
            {
                Email = emailWithSpaces,
                DisplayName = DISPLAY_NAME_WITH_SPACES,
                Password = PASSWORD_STRONG,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            });

            Assert.IsNotNull(response);
            Assert.IsTrue(response.UserId > 0);

            string stored = ReadDisplayNameByUserId(response.UserId);
            Assert.AreEqual(DISPLAY_NAME, stored);
        }

        private static readonly byte[] PngMinimalValidBytesForWorkflow = new byte[]
        {
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        private static string ReadDisplayNameByUserId(int userId)
        {
            const string SQL_SELECT_DISPLAY_NAME = @"
            SELECT display_name
            FROM dbo.Users
            WHERE user_id = @UserId;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(SQL_SELECT_DISPLAY_NAME, connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                connection.Open();

                object value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return string.Empty;
                }

                return (string)value;
            }
        }


        private static string BuildEmail(string prefix)
        {
            return string.Concat(
                "tc.register.",
                prefix,
                ".",
                Guid.NewGuid().ToString("N"),
                EMAIL_DOMAIN);
        }

        private void CreateAccount(string email)
        {
            string passwordHash = passwordService.Hash(PASSWORD_STRONG);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(null, null));

            authRepository.CreateAccountAndUser(data);
        }
    }
}
