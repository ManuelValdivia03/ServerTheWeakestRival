using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Fakes;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth.Integration
{
    [TestClass]
    public sealed class AuthServiceTests
    {
        private const string EMAIL_DOMAIN = "@test.local";
        private const string DISPLAY_NAME = "Service Test User";

        private const string PASSWORD = "ValidPass123!";
        private const string PASSWORD_WEAK = "short";
        private const string NEW_PASSWORD = "NewValidPass123!";

        private const string WHITESPACE = " ";

        private AuthService authService;
        private FakeEmailService fakeEmailService;

        [TestInitialize]
        public void SetUp()
        {
            TestConfigBootstrapper.EnsureLoaded();
            DbTestCleaner.CleanupAll();
            TokenStoreTestCleaner.ClearAllTokens();

            fakeEmailService = new FakeEmailService();
            var passwordService = new PasswordService(AuthServiceConstants.PASSWORD_MIN_LENGTH);

            authService = new AuthService(passwordService, fakeEmailService);
        }

        [TestCleanup]
        public void TearDown()
        {
            TokenStoreTestCleaner.ClearAllTokens();
            DbTestCleaner.CleanupAll();
        }

        [TestMethod]
        public void Ping_WhenCalled_ReturnsEcho()
        {
            var request = new PingRequest
            {
                Message = "ping"
            };

            PingResponse response = authService.Ping(request);

            Assert.IsNotNull(response);
            Assert.AreEqual(request.Message, response.Echo);
        }

        [TestMethod]
        public void BeginRegister_WhenRequestIsNull_ThrowsInvalidRequestEmailRequired()
        {
            ServiceFault fault = FaultAssert.Capture(() => authService.BeginRegister(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void BeginRegister_WhenEmailIsWhitespace_ThrowsInvalidRequestEmailRequired()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                authService.BeginRegister(new BeginRegisterRequest { Email = WHITESPACE }));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void BeginRegister_WhenEmailAlreadyRegistered_ThrowsEmailTaken()
        {
            string email = CreateUniqueEmail("taken");

            RegisterResponse reg = authService.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            ServiceFault fault = FaultAssert.Capture(() =>
                authService.BeginRegister(new BeginRegisterRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_TAKEN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_TAKEN, fault.Message);

            authService.Logout(new LogoutRequest { Token = reg.Token.Token });
        }

        [TestMethod]
        public void BeginRegister_WhenRequestedTooSoon_ThrowsTooSoon()
        {
            string email = CreateUniqueEmail("cooldown");

            authService.BeginRegister(new BeginRegisterRequest { Email = email });

            ServiceFault fault = FaultAssert.Capture(() =>
                authService.BeginRegister(new BeginRegisterRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);
        }

        [TestMethod]
        public void BeginRegister_WhenEmailValid_SendsVerificationEmail()
        {
            string email = CreateUniqueEmail("ok");

            BeginRegisterResponse response = authService.BeginRegister(new BeginRegisterRequest
            {
                Email = email
            });

            Assert.IsNotNull(response);
            Assert.IsTrue(response.ExpiresAtUtc > DateTime.UtcNow);

            Assert.AreEqual(email, fakeEmailService.LastVerificationEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastVerificationCode));
        }

        [TestMethod]
        public void CompleteRegister_WhenCodeIsInvalid_ThrowsCodeInvalid()
        {
            string email = CreateUniqueEmail("invalidcode");

            authService.BeginRegister(new BeginRegisterRequest { Email = email });

            ServiceFault fault = FaultAssert.Capture(() =>
                authService.CompleteRegister(new CompleteRegisterRequest
                {
                    Email = email,
                    Code = "000000",
                    Password = PASSWORD,
                    DisplayName = DISPLAY_NAME,
                    ProfileImageBytes = Array.Empty<byte>(),
                    ProfileImageContentType = string.Empty
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_INVALID, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_INVALID, fault.Message);
        }

        [TestMethod]
        public void Register_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            ServiceFault fault = FaultAssert.Capture(() => authService.Register(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void Register_WhenRequiredFieldsMissing_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                authService.Register(new RegisterRequest
                {
                    Email = WHITESPACE,
                    Password = PASSWORD,
                    DisplayName = DISPLAY_NAME,
                    ProfileImageBytes = null,
                    ProfileImageContentType = null
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_REGISTER_REQUIRED_FIELDS, fault.Message);
        }

        [TestMethod]
        public void Register_WhenPasswordIsWeak_ThrowsWeakPassword()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                authService.Register(new RegisterRequest
                {
                    Email = CreateUniqueEmail("weakpwd"),
                    Password = PASSWORD_WEAK,
                    DisplayName = DISPLAY_NAME,
                    ProfileImageBytes = Array.Empty<byte>(),
                    ProfileImageContentType = string.Empty
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_WEAK_PASSWORD, fault.Code);

            string expectedMessage = string.Format(
                AuthServiceConstants.MESSAGE_PASSWORD_MIN_LENGTH_NOT_MET,
                AuthServiceConstants.PASSWORD_MIN_LENGTH);

            Assert.AreEqual(expectedMessage, fault.Message);
        }

        [TestMethod]
        public void Register_WhenEmailAlreadyRegistered_ThrowsEmailTaken()
        {
            string email = CreateUniqueEmail("registertaken");

            RegisterResponse reg = authService.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            ServiceFault fault = FaultAssert.Capture(() =>
                authService.Register(new RegisterRequest
                {
                    Email = email,
                    Password = PASSWORD,
                    DisplayName = DISPLAY_NAME,
                    ProfileImageBytes = Array.Empty<byte>(),
                    ProfileImageContentType = string.Empty
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_TAKEN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_TAKEN, fault.Message);

            authService.Logout(new LogoutRequest { Token = reg.Token.Token });
        }

        [TestMethod]
        public void Register_WhenValid_CreatesAccount()
        {
            string email = CreateUniqueEmail("registerok");

            RegisterResponse response = authService.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            Assert.IsNotNull(response);
            Assert.IsTrue(response.UserId > 0);
            Assert.IsNotNull(response.Token);

            authService.Logout(new LogoutRequest { Token = response.Token.Token });
        }

        [TestMethod]
        public void Login_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            ServiceFault fault = FaultAssert.Capture(() => authService.Login(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void BeginPasswordReset_WhenEmailNotRegistered_ThrowsEmailNotFound()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                authService.BeginPasswordReset(new BeginPasswordResetRequest
                {
                    Email = CreateUniqueEmail("missing")
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_NOT_FOUND, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_NOT_REGISTERED, fault.Message);
        }

        [TestMethod]
        public void BeginPasswordReset_WhenAccountExists_SendsResetEmail()
        {
            string email = CreateUniqueEmail("reset");

            RegisterResponse reg = authService.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            BeginPasswordResetResponse response = authService.BeginPasswordReset(new BeginPasswordResetRequest
            {
                Email = email
            });

            Assert.IsNotNull(response);
            Assert.IsTrue(response.ExpiresAtUtc > DateTime.UtcNow);

            Assert.AreEqual(email, fakeEmailService.LastResetEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastResetCode));

            authService.Logout(new LogoutRequest { Token = reg.Token.Token });
        }

        [TestMethod]
        public void GetProfileImage_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            ServiceFault fault = FaultAssert.Capture(() => authService.GetProfileImage(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void Logout_WhenTokenIsUnknown_DoesNotThrow()
        {
            string unknownToken = Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);

            authService.Logout(new LogoutRequest
            {
                Token = unknownToken
            });

            bool exists = AuthServiceContext.TryGetUserId(unknownToken, out int userId);
            Assert.IsFalse(exists);
            Assert.AreEqual(0, userId);
        }

        private static string CreateUniqueEmail(string prefix)
        {
            string suffix = Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);
            return string.Concat("tc.authservice.", prefix, ".", suffix, EMAIL_DOMAIN);
        }
    }
}
