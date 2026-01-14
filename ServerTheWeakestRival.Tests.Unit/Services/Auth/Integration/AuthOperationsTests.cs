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
using ServicesTheWeakestRival.Server.Services;
using System.Linq;
using System.Reflection;


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

        private static string GetActiveTokenOrFail(int userId)
        {
            bool found = TokenStore.ActiveTokenByUserId.TryGetValue(userId, out string activeToken);

            Assert.IsTrue(found);
            Assert.IsFalse(string.IsNullOrWhiteSpace(activeToken));

            return activeToken;
        }

        private static string TryResolveTokenFromLogin(LoginResponse response)
        {
            if (response == null || response.Token == null)
            {
                return string.Empty;
            }

            string token = TryReadStringProperty(response.Token, "Token");
            token = FirstNonEmpty(token, TryReadStringProperty(response.Token, "TokenValue"));
            token = FirstNonEmpty(token, TryReadStringProperty(response.Token, "Value"));
            token = FirstNonEmpty(token, TryReadStringProperty(response.Token, "AccessToken"));
            token = FirstNonEmpty(token, TryReadStringProperty(response.Token, "SessionToken"));

            return (token ?? string.Empty).Trim();
        }

        private static string TryReadStringProperty(object instance, string propertyName)
        {
            if (instance == null)
            {
                return string.Empty;
            }

            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            if (property == null || property.PropertyType != typeof(string))
            {
                return string.Empty;
            }

            return (string)property.GetValue(instance) ?? string.Empty;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? second : first;
        }

        private static string TryGetTokenValue(LoginResponse response)
        {
            if (response == null)
            {
                return string.Empty;
            }

            if (response.Token == null)
            {
                return string.Empty;
            }

            return (response.Token.Token ?? string.Empty).Trim();
        }

        private const string TOKEN_COLUMN_NAME = "token";

        private const string USER_ID_COLUMN_NAME = "user_id";
        private const string ACCOUNT_ID_COLUMN_NAME = "account_id";

        private const string CREATED_AT_UTC_COLUMN_NAME = "created_at_utc";
        private const string ISSUED_AT_UTC_COLUMN_NAME = "issued_at_utc";
        private const string EXPIRES_AT_UTC_COLUMN_NAME = "expires_at_utc";

        private const string SQL_FIND_TOKEN_TABLE = @"
SELECT TOP (1)
    sch.name AS schema_name,
    t.name AS table_name
FROM sys.tables t
INNER JOIN sys.schemas sch ON sch.schema_id = t.schema_id
WHERE EXISTS (
    SELECT 1
    FROM sys.columns c
    WHERE c.object_id = t.object_id
      AND LOWER(c.name) = @TokenColumn
)
AND EXISTS (
    SELECT 1
    FROM sys.columns c
    WHERE c.object_id = t.object_id
      AND LOWER(c.name) IN (@UserIdColumn, @AccountIdColumn)
)
ORDER BY
    CASE
        WHEN LOWER(t.name) LIKE '%token%' THEN 0
        WHEN LOWER(t.name) LIKE '%auth%' THEN 1
        WHEN LOWER(t.name) LIKE '%session%' THEN 2
        ELSE 3
    END,
    t.name;";

        private const string SQL_FIND_BEST_USER_COLUMN = @"
SELECT TOP (1) c.name
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(@FullTableName)
  AND LOWER(c.name) IN (@UserIdColumn, @AccountIdColumn)
ORDER BY CASE WHEN LOWER(c.name) = @UserIdColumn THEN 0 ELSE 1 END;";

        private const string SQL_FIND_BEST_ORDER_COLUMN = @"
SELECT TOP (1) c.name
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(@FullTableName)
  AND LOWER(c.name) IN (@CreatedAtUtc, @IssuedAtUtc, @ExpiresAtUtc)
ORDER BY CASE
    WHEN LOWER(c.name) = @CreatedAtUtc THEN 0
    WHEN LOWER(c.name) = @IssuedAtUtc THEN 1
    WHEN LOWER(c.name) = @ExpiresAtUtc THEN 2
    ELSE 3
END;";


        private static class TokenValueResolver
        {
            private const string PropertyTokenContainer = "Token";

            private static readonly string[] CandidateTokenValuePropertyNames =
            {
        "Token",
        "TokenValue",
        "Value",
        "AccessToken",
        "SessionToken",
        "AuthToken"
    };

            internal static string GetTokenOrFail(object response, string responseName)
            {
                if (response == null)
                {
                    Assert.Fail(string.Concat(responseName, " was null."));
                }

                object tokenContainer = TryGetPropertyValue(response, PropertyTokenContainer);
                if (tokenContainer == null)
                {
                    Assert.Fail(string.Concat(
                        responseName,
                        " did not contain Token container. Public properties: ",
                        DescribePublicProperties(response)));
                }

                string tokenValue = TryResolveTokenValue(tokenContainer);
                if (!string.IsNullOrWhiteSpace(tokenValue))
                {
                    return tokenValue.Trim();
                }

                Assert.Fail(string.Concat(
                    responseName,
                    " Token container did not contain a token value. Token properties: ",
                    DescribePublicProperties(tokenContainer)));
                return string.Empty;
            }

            private static string TryResolveTokenValue(object tokenContainer)
            {
                if (tokenContainer == null)
                {
                    return string.Empty;
                }

                Type type = tokenContainer.GetType();

                foreach (string name in CandidateTokenValuePropertyNames)
                {
                    PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                    if (prop == null || prop.PropertyType != typeof(string))
                    {
                        continue;
                    }

                    string value = (string)prop.GetValue(tokenContainer);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                // Fallback: if there is a nested object with token-ish strings, try one level deeper.
                foreach (string name in CandidateTokenValuePropertyNames)
                {
                    PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                    if (prop == null || prop.PropertyType == typeof(string))
                    {
                        continue;
                    }

                    object nested = prop.GetValue(tokenContainer);
                    string nestedValue = TryResolveTokenValue(nested);
                    if (!string.IsNullOrWhiteSpace(nestedValue))
                    {
                        return nestedValue;
                    }
                }

                return string.Empty;
            }

            private static object TryGetPropertyValue(object instance, string propertyName)
            {
                PropertyInfo prop = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (prop == null)
                {
                    return null;
                }

                return prop.GetValue(instance);
            }

            private static string DescribePublicProperties(object instance)
            {
                PropertyInfo[] props = instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

                return string.Join(
                    ", ",
                    props.Select(p => string.Concat(p.Name, ":", p.PropertyType.Name)));
            }
        }
    }
}
