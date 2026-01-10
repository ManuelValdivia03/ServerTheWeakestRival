using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using System.Data;
using System.Data.SqlClient;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class CompleteRegisterWorkflowTests : AuthTestBase
    {
        private const string EMAIL_DOMAIN = "@test.local";

        private const string DISPLAY_NAME = "Test User";
        private const string DISPLAY_NAME_WITH_SPACES = "  Test User  ";

        private const string PASSWORD_STRONG = "Password123!";
        private const string PASSWORD_WEAK = "short";

        private const string CODE_VALID = "111111";
        private const string CODE_INVALID = "222222";
        private const string WHITESPACE = " ";

        private static readonly byte[] VALID_JPEG_BYTES =
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00
        };

        private static readonly byte[] PNG_SIGNATURE_BYTES =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        private static readonly byte[] EMPTY_IMAGE_BYTES = Array.Empty<byte>();
        private const string EMPTY_CONTENT_TYPE = "";

        [TestMethod]
        public void Execute_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenRequiredFieldsMissing_ThrowsInvalidRequest()
        {
            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = " ",
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_COMPLETE_REGISTER_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenPasswordIsWeak_ThrowsWeakPassword()
        {
            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = BuildEmail("weakpwd"),
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_WEAK,
                Code = CODE_VALID,
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
        public void Execute_WhenNoPendingVerification_ThrowsCodeMissing()
        {
            string email = BuildEmail("missingcode");

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_MISSING, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_MISSING, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenVerificationExpired_ThrowsCodeExpired()
        {
            string email = BuildEmail("expired");
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(-1);

            CreateVerification(email, CODE_VALID, expiresAtUtc);

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_EXPIRED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_EXPIRED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenCodeInvalid_ThrowsCodeInvalid()
        {
            string email = BuildEmail("invalidcode");
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            CreateVerification(email, CODE_VALID, expiresAtUtc);

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_INVALID,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_INVALID, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_INVALID, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailAlreadyRegistered_ThrowsEmailTaken()
        {
            string email = BuildEmail("taken");
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            CreateVerification(email, CODE_VALID, expiresAtUtc);
            CreateAccount(email);

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_TAKEN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_TAKEN, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenSuccess_CreatesAccount_MarksVerificationUsed_AndReturnsResponse()
        {
            string emailTrimmed = BuildEmail("success");
            string emailWithSpaces = "  " + emailTrimmed + "  ";

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            CreateVerification(emailTrimmed, CODE_VALID, expiresAtUtc);

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = emailWithSpaces,
                DisplayName = DISPLAY_NAME_WITH_SPACES,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
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

            VerificationLookupResult stillPending = authRepository.ReadLatestVerification(emailTrimmed);
            Assert.IsFalse(stillPending.Found);
        }

        [TestMethod]
        public void Execute_WhenDisplayNameIsWhitespace_ThrowsInvalidRequest()
        {
            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = BuildEmail("dnws"),
                DisplayName = WHITESPACE,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_COMPLETE_REGISTER_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenPasswordIsWhitespace_ThrowsInvalidRequest()
        {
            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = BuildEmail("pwdws"),
                DisplayName = DISPLAY_NAME,
                Password = WHITESPACE,
                Code = CODE_VALID,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_COMPLETE_REGISTER_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenCodeIsWhitespace_ThrowsInvalidRequest()
        {
            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = BuildEmail("codews"),
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = WHITESPACE,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_COMPLETE_REGISTER_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenMultipleVerificationsExist_UsesLatest_AndThrowsCodeInvalidForOldCode()
        {
            string email = BuildEmail("multiver");

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            CreateVerification(email, CODE_VALID, expiresAtUtc);
            CreateVerification(email, CODE_INVALID, expiresAtUtc); // invalida la anterior

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_INVALID, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_INVALID, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenCodeInvalid_IncrementsVerificationAttempts()
        {
            string email = BuildEmail("attempts");
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            CreateVerification(email, CODE_VALID, expiresAtUtc);

            VerificationLookupResult before = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(before.Found);

            int verificationId = before.Verification.Id;
            int attemptsBefore = ReadVerificationAttempts(verificationId);

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_INVALID,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_INVALID, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_INVALID, fault.Message);

            int attemptsAfter = ReadVerificationAttempts(verificationId);
            Assert.AreEqual(attemptsBefore + 1, attemptsAfter);
        }

        [TestMethod]
        public void Execute_WhenImageBytesProvidedButContentTypeMissing_ThrowsInvalidRequest()
        {
            string email = BuildEmail("imgnocontenttype");
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            CreateVerification(email, CODE_VALID, expiresAtUtc);

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = VALID_JPEG_BYTES,
                ProfileImageContentType = WHITESPACE
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image content type is required.", fault.Message);
        }

        [TestMethod]
        public void Execute_WhenImageContentTypeNotAllowed_ThrowsInvalidRequest()
        {
            string email = BuildEmail("imgbadtype");
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            CreateVerification(email, CODE_VALID, expiresAtUtc);

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = VALID_JPEG_BYTES,
                ProfileImageContentType = "image/gif"
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Only PNG and JPG profile images are allowed.", fault.Message);
        }

        [TestMethod]
        public void Execute_WhenImageSignatureDoesNotMatchDeclaredType_ThrowsInvalidRequest()
        {
            string email = BuildEmail("imgmismatch");
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            CreateVerification(email, CODE_VALID, expiresAtUtc);

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = PNG_SIGNATURE_BYTES,
                ProfileImageContentType = ProfileImageConstants.CONTENT_TYPE_JPEG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image file does not match the declared format.", fault.Message);
        }

        [TestMethod]
        public void Execute_WhenImageIsTooLarge_ThrowsInvalidRequest()
        {
            string email = BuildEmail("imgtoolarge");
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            CreateVerification(email, CODE_VALID, expiresAtUtc);

            int tooLargeBytesLength = ProfileImageConstants.DEFAULT_MAX_IMAGE_BYTES + 1;
            byte[] tooLarge = new byte[tooLargeBytesLength];
            tooLarge[0] = 0x89;

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = email,
                DisplayName = DISPLAY_NAME,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = tooLarge,
                ProfileImageContentType = ProfileImageConstants.CONTENT_TYPE_PNG
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);

            string expected = string.Format(
                "Profile image is too large. Max allowed is {0} KB.",
                ProfileImageConstants.DEFAULT_MAX_IMAGE_BYTES / ProfileImageConstants.ONE_KILOBYTE_BYTES);

            Assert.AreEqual(expected, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenSuccess_PersistsTrimmedDisplayName_AndNullImageFields()
        {
            string email = BuildEmail("dbassert");
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            CreateVerification(email, CODE_VALID, expiresAtUtc);

            var workflow = new CompleteRegisterWorkflow(authRepository, passwordPolicy, passwordService);

            var request = new CompleteRegisterRequest
            {
                Email = "  " + email + "  ",
                DisplayName = DISPLAY_NAME_WITH_SPACES,
                Password = PASSWORD_STRONG,
                Code = CODE_VALID,
                ProfileImageBytes = null,
                ProfileImageContentType = null
            };

            RegisterResponse response = workflow.Execute(request);

            string displayNameFromDb = ReadUserDisplayName(response.UserId);
            Assert.AreEqual(DISPLAY_NAME, displayNameFromDb);

            bool imageIsNull = ReadUserProfileImageIsNull(response.UserId);
            Assert.IsTrue(imageIsNull);
        }

        private int ReadVerificationAttempts(int verificationId)
        {
            const string sql = @"
SELECT attempts
FROM dbo.EmailVerifications
WHERE verification_id = @Id;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                connection.Open();

                object value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
        }

        private string ReadUserDisplayName(int userId)
        {
            const string sql = @"
SELECT display_name
FROM dbo.Users
WHERE user_id = @UserId;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();

                object value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? string.Empty : Convert.ToString(value);
            }
        }

        private bool ReadUserProfileImageIsNull(int userId)
        {
            const string sql = @"
                SELECT profile_image, profile_image_content_type, profile_image_updated_at_utc
                FROM dbo.Users
                WHERE user_id = @UserId;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();

                using (var reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return true;
                    }

                    bool imageNull = reader.IsDBNull(0);
                    bool typeNull = reader.IsDBNull(1);
                    bool updatedNull = reader.IsDBNull(2);

                    return imageNull && typeNull && updatedNull;
                }
            }
        }

        private static string BuildEmail(string prefix)
        {
            return string.Concat(
                "tc.completeregister.",
                prefix,
                ".",
                Guid.NewGuid().ToString("N"),
                EMAIL_DOMAIN);
        }

        private void CreateVerification(string email, string code, DateTime expiresAtUtc)
        {
            byte[] codeHash = SecurityUtil.Sha256(code);
            authRepository.CreateRegisterVerification(email, codeHash, expiresAtUtc);
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
