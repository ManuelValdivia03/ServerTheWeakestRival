using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class GetProfileImageWorkflowTests : AuthTestBase
    {
        private const string EMAIL_DOMAIN = "@test.local";

        private const string DISPLAY_NAME = "Test User";
        private const string PASSWORD = "Password123!";

        private const string CONTENT_TYPE_PNG = "image/png";

        private const string TOKEN_WHITESPACE = "   ";
        private const string TOKEN_EMPTY = "";
        private const int NON_EXISTING_USER_ID = 987654321;

        private static readonly byte[] PNG_IMAGE_BYTES = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x01, 0x02
        };

        [TestMethod]
        public void Execute_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            var workflow = new GetProfileImageWorkflow(authRepository);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenTokenIsInvalid_ThrowsInvalidSession()
        {
            int userId = 1;

            var workflow = new GetProfileImageWorkflow(authRepository);

            var request = new GetProfileImageRequest
            {
                Token = Guid.NewGuid().ToString("N"),
                UserId = userId
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenTokenIsNull_ThrowsInvalidSession()
        {
            var workflow = new GetProfileImageWorkflow(authRepository);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(new GetProfileImageRequest
            {
                Token = null,
                UserId = 1
            }));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenTokenIsEmpty_ThrowsInvalidSession()
        {
            var workflow = new GetProfileImageWorkflow(authRepository);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(new GetProfileImageRequest
            {
                Token = TOKEN_EMPTY,
                UserId = 1
            }));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenTokenIsWhitespace_ThrowsInvalidSession()
        {
            var workflow = new GetProfileImageWorkflow(authRepository);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(new GetProfileImageRequest
            {
                Token = TOKEN_WHITESPACE,
                UserId = 1
            }));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenTokenIsExpired_ThrowsInvalidSession()
        {
            string email = BuildEmail("expiredtoken");
            int userId = CreateAccountWithoutImage(email);

            AuthToken token = AuthServiceContext.IssueToken(userId);
            token.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);

            var workflow = new GetProfileImageWorkflow(authRepository);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(new GetProfileImageRequest
            {
                Token = token.Token,
                UserId = userId
            }));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenUserIdIsInvalid_ThrowsUserIdRequired()
        {
            string email = BuildEmail("useridinvalid");
            CreateAccountWithoutImage(email);

            string token = LoginAndGetToken(email);

            var workflow = new GetProfileImageWorkflow(authRepository);

            var request = new GetProfileImageRequest
            {
                Token = token,
                UserId = 0
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_USER_ID_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenUserHasNoImage_ReturnsEmptyImageResponse()
        {
            string email = BuildEmail("noimage");
            int userId = CreateAccountWithoutImage(email);

            string token = LoginAndGetToken(email);

            var workflow = new GetProfileImageWorkflow(authRepository);

            GetProfileImageResponse response = workflow.Execute(new GetProfileImageRequest
            {
                Token = token,
                UserId = userId
            });

            Assert.IsNotNull(response);
            Assert.AreEqual(userId, response.UserId);
            Assert.IsFalse(response.HasImage);

            Assert.IsNotNull(response.ImageBytes);
            Assert.AreEqual(0, response.ImageBytes.Length);

            Assert.IsNotNull(response.ContentType);
            Assert.AreEqual(string.Empty, response.ContentType);

            Assert.IsNull(response.UpdatedAtUtc);
        }

        [TestMethod]
        public void Execute_WhenUserIdDoesNotExist_ReturnsEmptyImageResponse()
        {
            string email = BuildEmail("tokenok_userdoesnotexist");
            int userId = CreateAccountWithoutImage(email);

            string token = LoginAndGetToken(email);

            var workflow = new GetProfileImageWorkflow(authRepository);

            GetProfileImageResponse response = workflow.Execute(new GetProfileImageRequest
            {
                Token = token,
                UserId = NON_EXISTING_USER_ID
            });

            Assert.IsNotNull(response);
            Assert.AreEqual(NON_EXISTING_USER_ID, response.UserId);
            Assert.IsFalse(response.HasImage);

            Assert.IsNotNull(response.ImageBytes);
            Assert.AreEqual(0, response.ImageBytes.Length);

            Assert.IsNotNull(response.ContentType);
            Assert.AreEqual(string.Empty, response.ContentType);

            Assert.IsNull(response.UpdatedAtUtc);
        }

        [TestMethod]
        public void Execute_WhenUserHasImage_ReturnsImageResponse()
        {
            string email = BuildEmail("hasimage");
            int userId = CreateAccountWithImage(email, PNG_IMAGE_BYTES, CONTENT_TYPE_PNG);

            string token = LoginAndGetToken(email);

            var workflow = new GetProfileImageWorkflow(authRepository);

            GetProfileImageResponse response = workflow.Execute(new GetProfileImageRequest
            {
                Token = token,
                UserId = userId
            });

            Assert.IsNotNull(response);
            Assert.AreEqual(userId, response.UserId);
            Assert.IsTrue(response.HasImage);

            CollectionAssert.AreEqual(PNG_IMAGE_BYTES, response.ImageBytes);
            Assert.AreEqual(CONTENT_TYPE_PNG, response.ContentType);
            Assert.IsNotNull(response.UpdatedAtUtc);
        }

        [TestMethod]
        public void Execute_WhenTokenBelongsToAnotherUser_AllowsReadingRequestedUserImage()
        {
            string emailA = BuildEmail("usera");
            CreateAccountWithoutImage(emailA);
            string tokenA = LoginAndGetToken(emailA);

            string emailB = BuildEmail("userb_target");
            int userIdB = CreateAccountWithImage(emailB, PNG_IMAGE_BYTES, CONTENT_TYPE_PNG);

            var workflow = new GetProfileImageWorkflow(authRepository);

            GetProfileImageResponse response = workflow.Execute(new GetProfileImageRequest
            {
                Token = tokenA,
                UserId = userIdB
            });

            Assert.IsNotNull(response);
            Assert.AreEqual(userIdB, response.UserId);
            Assert.IsTrue(response.HasImage);
            CollectionAssert.AreEqual(PNG_IMAGE_BYTES, response.ImageBytes);
            Assert.AreEqual(CONTENT_TYPE_PNG, response.ContentType);
        }

        private static string BuildEmail(string prefix)
        {
            return string.Concat(
                "tc.getprofileimage.",
                prefix,
                ".",
                Guid.NewGuid().ToString("N"),
                EMAIL_DOMAIN);
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

        private int CreateAccountWithoutImage(string email)
        {
            string passwordHash = passwordService.Hash(PASSWORD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(null, null));

            return authRepository.CreateAccountAndUser(data);
        }

        private int CreateAccountWithImage(string email, byte[] imageBytes, string contentType)
        {
            string passwordHash = passwordService.Hash(PASSWORD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(imageBytes, contentType));

            return authRepository.CreateAccountAndUser(data);
        }
    }
}
