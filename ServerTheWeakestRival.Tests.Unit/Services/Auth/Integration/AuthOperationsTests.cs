using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Fakes;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Infrastructure;
using System.Data;
using System.Data.SqlClient;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth.Integration
{
    [TestClass]
    public sealed class AuthOperationsTests
    {
        private const string EMAIL_DOMAIN = "@test.local";
        private const string DISPLAY_NAME = "Test User";
        private const string PASSWORD = "ValidPass123!";
        private const string NEW_PASSWORD = "NewValidPass123!";

        private const string PASSWORD_WEAK = "short";
        private const string PASSWORD_WRONG = "WrongPass123!";

        private const string CODE_INVALID = "000000";

        private const string WHITESPACE = " ";
        private const int EXPIRED_OFFSET_MINUTES = -1;

        private const string PROFILE_IMAGE_CODE_EMPTY = "";

        private const string SQL_EXPIRE_LATEST_VERIFICATION = @"
            UPDATE dbo.EmailVerifications
            SET expires_at_utc = DATEADD(MINUTE, @OffsetMinutes, SYSUTCDATETIME())
            WHERE verification_id = (
                SELECT TOP(1) verification_id
                FROM dbo.EmailVerifications
                WHERE email = @Email
                ORDER BY created_at_utc DESC
            );";

        private AuthOperations authOperations;
        private FakeEmailService fakeEmailService;
        private AuthRepository authRepository;
        private PasswordService passwordService;

        [TestInitialize]
        public void SetUp()
        {
            TestConfigBootstrapper.EnsureLoaded();
            DbTestCleaner.CleanupAll();
            TokenStoreTestCleaner.ClearAllTokens();

            passwordService = new PasswordService(AuthServiceConstants.PASSWORD_MIN_LENGTH);
            fakeEmailService = new FakeEmailService();

            authRepository = new AuthRepository(() => DbTestConfig.GetMainConnectionString());
            authOperations = new AuthOperations(authRepository, passwordService, fakeEmailService);
        }

        [TestCleanup]
        public void TearDown()
        {
            TokenStoreTestCleaner.ClearAllTokens();
            DbTestCleaner.CleanupAll();
        }

        [TestMethod]
        public void Ping_WhenMessageProvided_EchoesMessageAndReturnsUtc()
        {
            var request = new PingRequest
            {
                Message = "hello"
            };

            PingResponse response = authOperations.Ping(request);

            Assert.IsNotNull(response);
            Assert.AreEqual(request.Message, response.Echo);
            Assert.AreNotEqual(DateTime.MinValue, response.Utc);
        }

        [TestMethod]
        public void BeginRegister_WhenEmailValid_SendsVerificationEmailAndReturnsExpiry()
        {
            string email = CreateUniqueEmail();

            BeginRegisterResponse response = authOperations.BeginRegister(new BeginRegisterRequest
            {
                Email = email
            });

            Assert.IsNotNull(response);
            Assert.IsTrue(response.ExpiresAtUtc > DateTime.UtcNow);
            Assert.AreEqual(AuthServiceContext.ResendCooldownSeconds, response.ResendAfterSeconds);

            Assert.AreEqual(email, fakeEmailService.LastVerificationEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastVerificationCode));
            Assert.AreEqual(AuthServiceConstants.EMAIL_CODE_LENGTH, fakeEmailService.LastVerificationCode.Length);
        }

        [TestMethod]
        public void Register_WhenValidInput_CreatesAccount()
        {
            string email = CreateUniqueEmail();

            RegisterResponse response = authOperations.Register(new RegisterRequest
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
        }

        [TestMethod]
        public void BeginPasswordReset_WhenAccountExists_SendsResetEmail()
        {
            string email = CreateUniqueEmail();

            authOperations.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            BeginPasswordResetResponse response = authOperations.BeginPasswordReset(new BeginPasswordResetRequest
            {
                Email = email
            });

            Assert.IsNotNull(response);
            Assert.IsTrue(response.ExpiresAtUtc > DateTime.UtcNow);
            Assert.AreEqual(AuthServiceContext.ResendCooldownSeconds, response.ResendAfterSeconds);

            Assert.AreEqual(email, fakeEmailService.LastResetEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastResetCode));
            Assert.AreEqual(AuthServiceConstants.EMAIL_CODE_LENGTH, fakeEmailService.LastResetCode.Length);
        }

        [TestMethod]
        public void BeginRegister_WhenRequestIsNull_ThrowsInvalidRequestEmailRequired()
        {
            ServiceFault fault = FaultAssert.Capture(() => authOperations.BeginRegister(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void BeginRegister_WhenEmailIsWhitespace_ThrowsInvalidRequestEmailRequired()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.BeginRegister(new BeginRegisterRequest { Email = WHITESPACE }));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void BeginRegister_WhenEmailAlreadyRegistered_ThrowsEmailTaken_AndDoesNotSendEmail()
        {
            string email = CreateUniqueEmail();
            RegisterAccount(email, PASSWORD);

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.BeginRegister(new BeginRegisterRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_TAKEN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_TAKEN, fault.Message);

            Assert.IsTrue(string.IsNullOrWhiteSpace(fakeEmailService.LastVerificationCode));
        }

        [TestMethod]
        public void BeginRegister_WhenRequestedTooSoon_ThrowsTooSoon_AndDoesNotChangeLastCode()
        {
            string email = CreateUniqueEmail();

            authOperations.BeginRegister(new BeginRegisterRequest { Email = email });
            string firstCode = fakeEmailService.LastVerificationCode;

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.BeginRegister(new BeginRegisterRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);

            Assert.AreEqual(firstCode, fakeEmailService.LastVerificationCode);
        }

        [TestMethod]
        public void CompleteRegister_WhenNoPendingVerification_ThrowsCodeMissing()
        {
            string email = CreateUniqueEmail();

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.CompleteRegister(new CompleteRegisterRequest
                {
                    Email = email,
                    Code = "111111",
                    Password = PASSWORD,
                    DisplayName = DISPLAY_NAME,
                    ProfileImageBytes = Array.Empty<byte>(),
                    ProfileImageContentType = string.Empty
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_MISSING, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_MISSING, fault.Message);
        }

        [TestMethod]
        public void CompleteRegister_WhenCodeInvalid_ThrowsCodeInvalid()
        {
            string email = CreateUniqueEmail();

            authOperations.BeginRegister(new BeginRegisterRequest { Email = email });

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.CompleteRegister(new CompleteRegisterRequest
                {
                    Email = email,
                    Code = CODE_INVALID,
                    Password = PASSWORD,
                    DisplayName = DISPLAY_NAME,
                    ProfileImageBytes = Array.Empty<byte>(),
                    ProfileImageContentType = string.Empty
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_INVALID, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_INVALID, fault.Message);
        }

        [TestMethod]
        public void CompleteRegister_WhenVerificationExpired_ThrowsCodeExpired()
        {
            string email = CreateUniqueEmail();

            authOperations.BeginRegister(new BeginRegisterRequest { Email = email });
            string code = fakeEmailService.LastVerificationCode;

            ExpireLatestVerification(email);

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.CompleteRegister(new CompleteRegisterRequest
                {
                    Email = email,
                    Code = code,
                    Password = PASSWORD,
                    DisplayName = DISPLAY_NAME,
                    ProfileImageBytes = Array.Empty<byte>(),
                    ProfileImageContentType = string.Empty
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_EXPIRED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_EXPIRED, fault.Message);
        }

        [TestMethod]
        public void CompleteRegister_WhenPasswordWeak_ThrowsWeakPassword()
        {
            string email = CreateUniqueEmail();

            authOperations.BeginRegister(new BeginRegisterRequest { Email = email });
            string code = fakeEmailService.LastVerificationCode;

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.CompleteRegister(new CompleteRegisterRequest
                {
                    Email = email,
                    Code = code,
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
        public void CompleteRegister_WhenCalledTwiceAfterSuccess_ThrowsCodeMissing()
        {
            string email = CreateUniqueEmail();

            authOperations.BeginRegister(new BeginRegisterRequest { Email = email });
            string code = fakeEmailService.LastVerificationCode;

            authOperations.CompleteRegister(new CompleteRegisterRequest
            {
                Email = email,
                Code = code,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.CompleteRegister(new CompleteRegisterRequest
                {
                    Email = email,
                    Code = code,
                    Password = PASSWORD,
                    DisplayName = DISPLAY_NAME,
                    ProfileImageBytes = Array.Empty<byte>(),
                    ProfileImageContentType = string.Empty
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_MISSING, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_CODE_MISSING, fault.Message);
        }

        [TestMethod]
        public void Login_WhenPasswordIncorrect_ThrowsInvalidCredentials()
        {
            string email = CreateUniqueEmail();
            RegisterAccount(email, PASSWORD);

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.Login(new LoginRequest { Email = email, Password = PASSWORD_WRONG }));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }

        [TestMethod]
        public void Login_WhenAlreadyLoggedIn_ThrowsAlreadyLoggedIn()
        {
            string email = CreateUniqueEmail();

            RegisterResponse register = authOperations.Register(new RegisterRequest
            {
                Email = email,
                Password = PASSWORD,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.Login(new LoginRequest { Email = email, Password = PASSWORD }));

            Assert.AreEqual(AuthServiceConstants.ERROR_ALREADY_LOGGED_IN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ALREADY_LOGGED_IN, fault.Message);

            authOperations.Logout(new LogoutRequest { Token = register.Token.Token });
        }

        [TestMethod]
        public void BeginPasswordReset_WhenEmailNotRegistered_ThrowsEmailNotFound()
        {
            string email = CreateUniqueEmail();

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.BeginPasswordReset(new BeginPasswordResetRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_NOT_FOUND, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_NOT_REGISTERED, fault.Message);

            Assert.IsTrue(string.IsNullOrWhiteSpace(fakeEmailService.LastResetCode));
        }

        [TestMethod]
        public void BeginPasswordReset_WhenRequestedTooSoon_ThrowsTooSoon_AndDoesNotChangeLastCode()
        {
            string email = CreateUniqueEmail();
            RegisterAccount(email, PASSWORD);

            authOperations.BeginPasswordReset(new BeginPasswordResetRequest { Email = email });
            string firstCode = fakeEmailService.LastResetCode;

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.BeginPasswordReset(new BeginPasswordResetRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);

            Assert.AreEqual(firstCode, fakeEmailService.LastResetCode);
        }

        [TestMethod]
        public void CompletePasswordReset_WhenCodeInvalid_ThrowsCodeInvalid()
        {
            string email = CreateUniqueEmail();
            RegisterAccount(email, PASSWORD);

            authOperations.BeginPasswordReset(new BeginPasswordResetRequest { Email = email });

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.CompletePasswordReset(new CompletePasswordResetRequest
                {
                    Email = email,
                    Code = CODE_INVALID,
                    NewPassword = NEW_PASSWORD
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_INVALID, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_INVALID, fault.Message);
        }

        [TestMethod]
        public void CompletePasswordReset_WhenNewPasswordWeak_ThrowsWeakPassword()
        {
            string email = CreateUniqueEmail();
            RegisterAccount(email, PASSWORD);

            authOperations.BeginPasswordReset(new BeginPasswordResetRequest { Email = email });
            string code = fakeEmailService.LastResetCode;

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.CompletePasswordReset(new CompletePasswordResetRequest
                {
                    Email = email,
                    Code = code,
                    NewPassword = PASSWORD_WEAK
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_WEAK_PASSWORD, fault.Code);
        }

        [TestMethod]
        public void CompletePasswordReset_WhenReusingUsedCode_ThrowsCodeExpired()
        {
            string email = CreateUniqueEmail();
            RegisterAccount(email, PASSWORD);

            authOperations.BeginPasswordReset(new BeginPasswordResetRequest { Email = email });
            string code = fakeEmailService.LastResetCode;

            authOperations.CompletePasswordReset(new CompletePasswordResetRequest
            {
                Email = email,
                Code = code,
                NewPassword = NEW_PASSWORD
            });

            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.CompletePasswordReset(new CompletePasswordResetRequest
                {
                    Email = email,
                    Code = code,
                    NewPassword = "AnotherNewValidPass123!"
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_CODE_EXPIRED, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_RESET_CODE_EXPIRED, fault.Message);
        }

        [TestMethod]
        public void GetProfileImage_WhenTokenInvalid_ThrowsInvalidSession()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                authOperations.GetProfileImage(new GetProfileImageRequest
                {
                    Token = Guid.NewGuid().ToString("N"),
                    AccountId = 1,
                    ProfileImageCode = PROFILE_IMAGE_CODE_EMPTY
                }));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_SESSION, fault.Message);
        }

        private void RegisterAccount(string email, string password)
        {
            authOperations.Register(new RegisterRequest
            {
                Email = email,
                Password = password,
                DisplayName = DISPLAY_NAME,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = string.Empty
            });
        }

        private static string CreateUniqueEmail()
        {
            string suffix = Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);
            return string.Concat("user+", suffix, EMAIL_DOMAIN);
        }

        private void ExpireLatestVerification(string email)
        {
            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(SQL_EXPIRE_LATEST_VERIFICATION, connection))
            {
                cmd.CommandType = CommandType.Text;

                cmd.Parameters.Add(
                    AuthServiceConstants.PARAMETER_EMAIL,
                    SqlDbType.NVarChar,
                    AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                cmd.Parameters.Add("@OffsetMinutes", SqlDbType.Int).Value = EXPIRED_OFFSET_MINUTES;

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
