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
        private const string PROFILE_IMAGE_CODE_EMPTY = "";

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
            var workflow = new GetProfileImageWorkflow(authRepository);

            var request = new GetProfileImageRequest
            {
                Token = Guid.NewGuid().ToString("N"),
                AccountId = 1,
                ProfileImageCode = PROFILE_IMAGE_CODE_EMPTY
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenAccountIdIsInvalid_ThrowsUserIdRequired()
        {
            string email = BuildEmail("accountidinvalid");
            CreateAccountWithoutImage(email);

            string token = LoginAndGetToken(email);

            var workflow = new GetProfileImageWorkflow(authRepository);

            var request = new GetProfileImageRequest
            {
                Token = token,
                AccountId = 0,
                ProfileImageCode = PROFILE_IMAGE_CODE_EMPTY
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_USER_ID_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenAccountHasNoImage_ReturnsEmptyImageResponse()
        {
            string email = BuildEmail("noimage");
            int accountId = CreateAccountWithoutImage(email);

            string token = LoginAndGetToken(email);

            var workflow = new GetProfileImageWorkflow(authRepository);

            GetProfileImageResponse response = workflow.Execute(new GetProfileImageRequest
            {
                Token = token,
                AccountId = accountId,
                ProfileImageCode = PROFILE_IMAGE_CODE_EMPTY
            });

            Assert.IsNotNull(response);

            Assert.IsNotNull(response.ImageBytes);
            Assert.AreEqual(0, response.ImageBytes.Length);

            Assert.IsNotNull(response.ContentType);
            Assert.AreEqual(string.Empty, response.ContentType);

            Assert.IsNull(response.UpdatedAtUtc);

            Assert.IsNotNull(response.ProfileImageCode);
            Assert.AreEqual(string.Empty, response.ProfileImageCode);
        }

        [TestMethod]
        public void Execute_WhenAccountHasImage_ReturnsImageResponse()
        {
            string email = BuildEmail("hasimage");
            int accountId = CreateAccountWithImage(email, PNG_IMAGE_BYTES, CONTENT_TYPE_PNG);

            string token = LoginAndGetToken(email);

            var workflow = new GetProfileImageWorkflow(authRepository);

            GetProfileImageResponse response = workflow.Execute(new GetProfileImageRequest
            {
                Token = token,
                AccountId = accountId,
                ProfileImageCode = PROFILE_IMAGE_CODE_EMPTY
            });

            Assert.IsNotNull(response);

            CollectionAssert.AreEqual(PNG_IMAGE_BYTES, response.ImageBytes);
            Assert.AreEqual(CONTENT_TYPE_PNG, response.ContentType);
            Assert.IsNotNull(response.UpdatedAtUtc);

            Assert.IsFalse(string.IsNullOrWhiteSpace(response.ProfileImageCode));
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
