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
        private const string EMPTY = "";

        private const string CONTENT_TYPE_PNG = ProfileImageConstants.CONTENT_TYPE_PNG;

        private static readonly byte[] PNG_MINIMAL_VALID_BYTES = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

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

            authService.Register(new RegisterRequest
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
        public void CompleteRegister_WhenCodeIsCorrect_CreatesAccount_AndAllowsLogin()
        {
            string email = CreateUniqueEmail("completeregister");

            authService.BeginRegister(new BeginRegisterRequest { Email = email });
            string code = fakeEmailService.LastVerificationCode;

            RegisterResponse registerResponse = authService.CompleteRegister(new CompleteRegisterRequest
            {
                Email = "  " + email + "  ",
                Code = code,
                Password = PASSWORD,
                DisplayName = "  " + DISPLAY_NAME + "  ",
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            Assert.IsNotNull(registerResponse);
            Assert.IsTrue(registerResponse.UserId > 0);

            LoginResponse loginResponse = authService.Login(new LoginRequest
            {
                Email = email,
                Password = PASSWORD
            });

            Assert.IsNotNull(loginResponse);
            Assert.IsNotNull(loginResponse.Token);
            Assert.IsFalse(string.IsNullOrWhiteSpace(loginResponse.Token.Token));
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

            authService.Register(new RegisterRequest
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
        }

        [TestMethod]
        public void Login_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            ServiceFault fault = FaultAssert.Capture(() => authService.Login(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void Login_WhenValidCredentials_ReturnsToken()
        {
            string email = CreateUniqueEmail("loginok");

            authService.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            LoginResponse response = authService.Login(new LoginRequest
            {
                Email = email,
                Password = PASSWORD
            });

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Token);
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Token.Token));
        }

        [TestMethod]
        public void Login_WhenAlreadyLoggedIn_ThrowsAlreadyLoggedIn()
        {
            string email = CreateUniqueEmail("already");

            authService.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            authService.Login(new LoginRequest { Email = email, Password = PASSWORD });

            ServiceFault fault = FaultAssert.Capture(() =>
                authService.Login(new LoginRequest { Email = email, Password = PASSWORD }));

            Assert.AreEqual(AuthServiceConstants.ERROR_ALREADY_LOGGED_IN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ALREADY_LOGGED_IN, fault.Message);
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

            authService.Register(new RegisterRequest
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
        }

        [TestMethod]
        public void CompletePasswordReset_WhenCodeIsCorrect_UpdatesPasswordAndAllowsLogin()
        {
            string email = CreateUniqueEmail("completereset");

            authService.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            authService.BeginPasswordReset(new BeginPasswordResetRequest { Email = email });
            string code = fakeEmailService.LastResetCode;

            authService.CompletePasswordReset(new CompletePasswordResetRequest
            {
                Email = email,
                Code = code,
                NewPassword = NEW_PASSWORD
            });

            LoginResponse login = authService.Login(new LoginRequest
            {
                Email = email,
                Password = NEW_PASSWORD
            });

            Assert.IsNotNull(login);
            Assert.IsNotNull(login.Token);
            Assert.IsFalse(string.IsNullOrWhiteSpace(login.Token.Token));
        }

        [TestMethod]
        public void GetProfileImage_WhenRequestIsNull_ThrowsInvalidRequestPayloadNull()
        {
            ServiceFault fault = FaultAssert.Capture(() => authService.GetProfileImage(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PAYLOAD_NULL, fault.Message);
        }

        [TestMethod]
        public void GetProfileImage_WhenNoImageSaved_ReturnsEmptyImage()
        {
            string email = CreateUniqueEmail("noimage");

            authService.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            LoginResponse loginResponse = authService.Login(new LoginRequest
            {
                Email = email,
                Password = PASSWORD
            });

            GetProfileImageResponse response = authService.GetProfileImage(new GetProfileImageRequest
            {
                Token = loginResponse.Token.Token,
                AccountId = loginResponse.Token.UserId,
                ProfileImageCode = EMPTY
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
        public void GetProfileImage_WhenImageSaved_ReturnsImage()
        {
            string email = CreateUniqueEmail("hasimage");

            RegisterResponse reg = authService.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = PNG_MINIMAL_VALID_BYTES,
                ProfileImageContentType = CONTENT_TYPE_PNG
            });

            LoginResponse loginResponse = authService.Login(new LoginRequest
            {
                Email = email,
                Password = PASSWORD
            });

            GetProfileImageResponse response = authService.GetProfileImage(new GetProfileImageRequest
            {
                Token = loginResponse.Token.Token,
                AccountId = reg.UserId,
                ProfileImageCode = EMPTY
            });

            Assert.IsNotNull(response);

            Assert.IsNotNull(response.ImageBytes);
            Assert.IsTrue(response.ImageBytes.Length > 0);
            CollectionAssert.AreEqual(PNG_MINIMAL_VALID_BYTES, response.ImageBytes);

            Assert.IsNotNull(response.ContentType);
            Assert.AreEqual(CONTENT_TYPE_PNG, response.ContentType);

            Assert.IsNotNull(response.UpdatedAtUtc);

            Assert.IsNotNull(response.ProfileImageCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.ProfileImageCode));
        }

        [TestMethod]
        public void Logout_WhenTokenIsUnknown_DoesNotThrow()
        {
            authService.Logout(new LogoutRequest
            {
                Token = Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT)
            });

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void Logout_WhenTokenIsValid_RemovesToken()
        {
            string email = CreateUniqueEmail("logout");

            authService.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            LoginResponse login = authService.Login(new LoginRequest
            {
                Email = email,
                Password = PASSWORD
            });

            string tokenValue = login.Token.Token;

            bool okBefore = AuthServiceContext.TryGetUserId(tokenValue, out int userIdBefore);
            Assert.IsTrue(okBefore);
            Assert.IsTrue(userIdBefore > 0);

            authService.Logout(new LogoutRequest { Token = tokenValue });

            bool okAfter = AuthServiceContext.TryGetUserId(tokenValue, out int userIdAfter);
            Assert.IsFalse(okAfter);
            Assert.AreEqual(0, userIdAfter);
        }

        private static string CreateUniqueEmail(string prefix)
        {
            string suffix = Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);
            return string.Concat("tc.authservice.", prefix, ".", suffix, EMAIL_DOMAIN);
        }
    }
}
