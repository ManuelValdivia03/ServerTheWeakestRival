using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Data;
using System.Data.SqlClient;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth.Integration
{
    [TestClass]
    public sealed class AuthRepositoryIntegrationTests
    {
        private const int SQL_TIMEOUT_SECONDS = 30;

        private const string EMAIL_DOMAIN = "@test.local";
        private const string DISPLAY_NAME = "Integration Test User";

        private const string PASSWORD = "ValidPass123!";
        private const string NEW_PASSWORD = "NewValidPass123!";

        private const string CODE_OK = "123456";
        private const string CODE_WRONG = "000000";

        private const string SQL_SELECT_VERIFICATION_ATTEMPTS = @"
            SELECT attempts
            FROM dbo.EmailVerifications
            WHERE verification_id = @Id;";

        private const string SQL_SELECT_RESET_ATTEMPTS = @"
            SELECT Attempts
            FROM dbo.PasswordResetRequests
            WHERE Id = @Id;";

        private const string PARAM_ID = "@Id";

        private const string SQL_SELECT_LATEST_PENDING_VERIFICATION_ID = @"
            SELECT TOP(1) verification_id
            FROM dbo.EmailVerifications
            WHERE email = @Email AND used = 0
            ORDER BY created_at_utc DESC;";

        private const string SQL_UPDATE_VERIFICATION_CREATED_AT_OLDER = @"
            UPDATE dbo.EmailVerifications
            SET created_at_utc = DATEADD(MINUTE, -10, SYSUTCDATETIME())
            WHERE verification_id = @Id;";

        private const string SQL_SELECT_LATEST_RESET_ID = @"
            SELECT TOP(1) Id
            FROM dbo.PasswordResetRequests
            WHERE Email = @Email
            ORDER BY CreatedAtUtc DESC;";

        private const string SQL_UPDATE_RESET_CREATED_AT_OLDER = @"
            UPDATE dbo.PasswordResetRequests
            SET CreatedAtUtc = DATEADD(MINUTE, -10, SYSUTCDATETIME())
            WHERE Id = @Id;";

        private const int EXPECTED_ATTEMPTS_1 = 1;
        private const int EXPECTED_ATTEMPTS_2 = 2;

        private AuthRepository authRepository;
        private PasswordService passwordService;

        [TestInitialize]
        public void SetUp()
        {
            DbTestCleaner.CleanupAll();
            TokenStoreTestCleaner.ClearAllTokens();

            authRepository = new AuthRepository(() => DbTestConfig.GetMainConnectionString());
            passwordService = new PasswordService(AuthServiceConstants.PASSWORD_MIN_LENGTH);
        }

        [TestCleanup]
        public void TearDown()
        {
            TokenStoreTestCleaner.ClearAllTokens();
            DbTestCleaner.CleanupAll();
        }

        [TestMethod]
        public void ExistsAccountByEmail_WhenAccountDoesNotExist_ReturnsFalse()
        {
            string email = CreateUniqueEmail();

            bool exists = authRepository.ExistsAccountByEmail(email);

            Assert.IsFalse(exists);
        }

        [TestMethod]
        public void CreateRegisterVerification_ThenReadLatestVerification_ReturnsFound()
        {
            string email = CreateUniqueEmail();

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            byte[] hash = SecurityUtil.Sha256(CODE_OK);

            authRepository.CreateRegisterVerification(email, hash, expiresAtUtc);

            VerificationLookupResult result = authRepository.ReadLatestVerification(email);

            Assert.IsTrue(result.Found);
            Assert.IsNotNull(result.Verification);
            Assert.IsTrue(result.Verification.Id > 0);
            Assert.IsFalse(result.Verification.Used);
        }

        [TestMethod]
        public void ValidateVerificationCodeOrThrow_WhenHashMatches_ReturnsValid()
        {
            string email = CreateUniqueEmail();

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            byte[] hash = SecurityUtil.Sha256(CODE_OK);

            authRepository.CreateRegisterVerification(email, hash, expiresAtUtc);

            VerificationLookupResult lookup = authRepository.ReadLatestVerification(email);
            CodeValidationResult result = authRepository.ValidateVerificationCodeOrThrow(lookup.Verification.Id, hash);

            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void ValidateVerificationCodeOrThrow_WhenHashDoesNotMatch_ReturnsInvalidAndIncrementsAttempts()
        {
            string email = CreateUniqueEmail();

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            byte[] correctHash = SecurityUtil.Sha256(CODE_OK);

            authRepository.CreateRegisterVerification(email, correctHash, expiresAtUtc);

            VerificationLookupResult lookup = authRepository.ReadLatestVerification(email);

            byte[] wrongHash = SecurityUtil.Sha256(CODE_WRONG);
            CodeValidationResult result = authRepository.ValidateVerificationCodeOrThrow(lookup.Verification.Id, wrongHash);

            Assert.IsFalse(result.IsValid);

            int attempts = ExecuteScalarInt(
                SQL_SELECT_VERIFICATION_ATTEMPTS,
                lookup.Verification.Id);

            Assert.AreEqual(1, attempts);
        }

        [TestMethod]
        public void MarkVerificationUsed_WhenCalled_RemovesPendingVerificationFromLookup()
        {
            string email = CreateUniqueEmail();

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            byte[] hash = SecurityUtil.Sha256(CODE_OK);

            authRepository.CreateRegisterVerification(email, hash, expiresAtUtc);

            VerificationLookupResult lookup = authRepository.ReadLatestVerification(email);
            authRepository.MarkVerificationUsed(lookup.Verification.Id);

            VerificationLookupResult after = authRepository.ReadLatestVerification(email);

            Assert.IsFalse(after.Found);
        }

        [TestMethod]
        public void CreateAccountAndUser_ThenGetAccountForLogin_ReturnsFoundActive()
        {
            string email = CreateUniqueEmail();
            string passwordHash = passwordService.Hash(PASSWORD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(Array.Empty<byte>(), string.Empty));

            int newUserId = authRepository.CreateAccountAndUser(data);

            Assert.IsTrue(newUserId > 0);

            bool exists = authRepository.ExistsAccountByEmail(email);
            Assert.IsTrue(exists);

            LoginLookupResult loginLookup = authRepository.GetAccountForLogin(email);

            Assert.IsTrue(loginLookup.Found);
            Assert.IsNotNull(loginLookup.Account);
            Assert.AreEqual(newUserId, loginLookup.Account.UserId);
            Assert.AreEqual(AuthServiceConstants.ACCOUNT_STATUS_ACTIVE, loginLookup.Account.Status);

            bool verified = passwordService.Verify(PASSWORD, loginLookup.Account.PasswordHash);
            Assert.IsTrue(verified);
        }

        [TestMethod]
        public void UpdateAccountPassword_WhenEmailExists_UpdatesPasswordHash()
        {
            string email = CreateUniqueEmail();
            string passwordHash = passwordService.Hash(PASSWORD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(Array.Empty<byte>(), string.Empty));

            int newUserId = authRepository.CreateAccountAndUser(data);

            string newHash = passwordService.Hash(NEW_PASSWORD);
            int rows = authRepository.UpdateAccountPassword(email, newHash);

            Assert.IsTrue(rows > 0);

            LoginLookupResult loginLookup = authRepository.GetAccountForLogin(email);

            Assert.IsTrue(loginLookup.Found);
            Assert.AreEqual(newUserId, loginLookup.Account.UserId);

            bool oldOk = passwordService.Verify(PASSWORD, loginLookup.Account.PasswordHash);
            Assert.IsFalse(oldOk);

            bool newOk = passwordService.Verify(NEW_PASSWORD, loginLookup.Account.PasswordHash);
            Assert.IsTrue(newOk);
        }

        [TestMethod]
        public void ReadUserProfileImage_WhenNoImageSaved_ReturnsEmptyImageRecord()
        {
            string email = CreateUniqueEmail();
            string passwordHash = passwordService.Hash(PASSWORD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(Array.Empty<byte>(), string.Empty));

            int newUserId = authRepository.CreateAccountAndUser(data);

            ProfileImageRecord record = authRepository.ReadUserProfileImage(newUserId);

            Assert.IsNotNull(record);
            Assert.AreEqual(newUserId, record.UserId);
            Assert.AreEqual(0, record.ImageBytes.Length);
            Assert.AreEqual(string.Empty, record.ContentType);
            Assert.IsNull(record.UpdatedAtUtc);
        }

        [TestMethod]
        public void CreatePasswordResetRequest_ThenReadLatestReset_ReturnsFound()
        {
            string email = CreateUniqueEmail();
            CreateAccountForEmail(email);

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            byte[] hash = SecurityUtil.Sha256(CODE_OK);

            authRepository.CreatePasswordResetRequest(email, hash, expiresAtUtc);

            ResetLookupResult result = authRepository.ReadLatestReset(email);

            Assert.IsTrue(result.Found);
            Assert.IsNotNull(result.Reset);
            Assert.IsTrue(result.Reset.Id > 0);
            Assert.IsFalse(result.Reset.Used);
        }

        [TestMethod]
        public void ValidateResetCodeOrThrow_WhenHashDoesNotMatch_ReturnsInvalidAndIncrementsAttempts()
        {
            string email = CreateUniqueEmail();
            CreateAccountForEmail(email);

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            byte[] correctHash = SecurityUtil.Sha256(CODE_OK);

            authRepository.CreatePasswordResetRequest(email, correctHash, expiresAtUtc);

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);

            byte[] wrongHash = SecurityUtil.Sha256(CODE_WRONG);
            CodeValidationResult result = authRepository.ValidateResetCodeOrThrow(lookup.Reset.Id, wrongHash);

            Assert.IsFalse(result.IsValid);

            int attempts = ExecuteScalarInt(
                SQL_SELECT_RESET_ATTEMPTS,
                lookup.Reset.Id);

            Assert.AreEqual(1, attempts);
        }

        [TestMethod]
        public void MarkResetUsed_WhenCalled_MarksResetAsUsed()
        {
            string email = CreateUniqueEmail();
            CreateAccountForEmail(email);

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            byte[] hash = SecurityUtil.Sha256(CODE_OK);

            authRepository.CreatePasswordResetRequest(email, hash, expiresAtUtc);

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            authRepository.MarkResetUsed(lookup.Reset.Id);

            ResetLookupResult after = authRepository.ReadLatestReset(email);

            Assert.IsTrue(after.Found);
            Assert.IsTrue(after.Reset.Used);
        }

        [TestMethod]
        public void ReadLatestVerification_WhenNone_ReturnsNotFound()
        {
            string email = CreateUniqueEmail();

            VerificationLookupResult result = authRepository.ReadLatestVerification(email);

            Assert.IsFalse(result.Found);
        }

        [TestMethod]
        public void ReadLatestVerification_WhenMultiplePending_ReturnsLatestDeterministically()
        {
            string email = CreateUniqueEmail();

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            byte[] hash1 = SecurityUtil.Sha256(CODE_OK);
            authRepository.CreateRegisterVerification(email, hash1, expiresAtUtc);
            VerificationLookupResult first = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(first.Found);

            ExecuteNonQuery(SQL_UPDATE_VERIFICATION_CREATED_AT_OLDER, first.Verification.Id);

            byte[] hash2 = SecurityUtil.Sha256(CODE_WRONG);
            authRepository.CreateRegisterVerification(email, hash2, expiresAtUtc);

            VerificationLookupResult latest = authRepository.ReadLatestVerification(email);

            Assert.IsTrue(latest.Found);

            int latestIdFromSql = ExecuteScalarIntByEmail(SQL_SELECT_LATEST_PENDING_VERIFICATION_ID, email);
            Assert.AreEqual(latestIdFromSql, latest.Verification.Id);
            Assert.AreNotEqual(first.Verification.Id, latest.Verification.Id);
        }

        [TestMethod]
        public void MarkVerificationUsed_WhenCreatingNewVerificationInvalidatesPrevious_ReadLatestReturnsNotFound()
        {
            string email = CreateUniqueEmail();

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            authRepository.CreateRegisterVerification(email, SecurityUtil.Sha256(CODE_OK), expiresAtUtc);
            VerificationLookupResult first = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(first.Found);

            authRepository.CreateRegisterVerification(email, SecurityUtil.Sha256(CODE_WRONG), expiresAtUtc);
            VerificationLookupResult latest = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(latest.Found);
            Assert.AreNotEqual(first.Verification.Id, latest.Verification.Id);

            authRepository.MarkVerificationUsed(latest.Verification.Id);

            VerificationLookupResult after = authRepository.ReadLatestVerification(email);
            Assert.IsFalse(after.Found);
        }


        [TestMethod]
        public void ValidateVerificationCodeOrThrow_WhenWrongTwice_IncrementsAttemptsToTwo()
        {
            string email = CreateUniqueEmail();

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            byte[] correctHash = SecurityUtil.Sha256(CODE_OK);

            authRepository.CreateRegisterVerification(email, correctHash, expiresAtUtc);

            VerificationLookupResult lookup = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(lookup.Found);

            byte[] wrongHash = SecurityUtil.Sha256(CODE_WRONG);

            CodeValidationResult r1 = authRepository.ValidateVerificationCodeOrThrow(lookup.Verification.Id, wrongHash);
            CodeValidationResult r2 = authRepository.ValidateVerificationCodeOrThrow(lookup.Verification.Id, wrongHash);

            Assert.IsFalse(r1.IsValid);
            Assert.IsFalse(r2.IsValid);

            int attempts = ExecuteScalarInt(SQL_SELECT_VERIFICATION_ATTEMPTS, lookup.Verification.Id);
            Assert.AreEqual(EXPECTED_ATTEMPTS_2, attempts);
        }

        [TestMethod]
        public void GetAccountForLogin_WhenEmailDoesNotExist_ReturnsNotFound()
        {
            string email = CreateUniqueEmail();

            LoginLookupResult login = authRepository.GetAccountForLogin(email);

            Assert.IsFalse(login.Found);
        }

        [TestMethod]
        public void UpdateAccountPassword_WhenEmailDoesNotExist_ReturnsZeroRows()
        {
            string email = CreateUniqueEmail();

            string newHash = passwordService.Hash(NEW_PASSWORD);

            int rows = authRepository.UpdateAccountPassword(email, newHash);

            Assert.AreEqual(0, rows);
        }

        [TestMethod]
        public void ReadLatestReset_WhenNone_ReturnsNotFound()
        {
            string email = CreateUniqueEmail();

            ResetLookupResult result = authRepository.ReadLatestReset(email);

            Assert.IsFalse(result.Found);
        }

        [TestMethod]
        public void ValidateResetCodeOrThrow_WhenHashMatches_ReturnsValid()
        {
            string email = CreateUniqueEmail();
            CreateAccountForEmail(email);

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            byte[] correctHash = SecurityUtil.Sha256(CODE_OK);

            authRepository.CreatePasswordResetRequest(email, correctHash, expiresAtUtc);

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(lookup.Found);

            CodeValidationResult result = authRepository.ValidateResetCodeOrThrow(lookup.Reset.Id, correctHash);

            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public void ReadLatestReset_WhenMultipleRequests_ReturnsLatestDeterministically()
        {
            string email = CreateUniqueEmail();
            CreateAccountForEmail(email);

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);

            byte[] hash1 = SecurityUtil.Sha256(CODE_OK);
            authRepository.CreatePasswordResetRequest(email, hash1, expiresAtUtc);

            ResetLookupResult first = authRepository.ReadLatestReset(email);
            Assert.IsTrue(first.Found);

            ExecuteNonQuery(SQL_UPDATE_RESET_CREATED_AT_OLDER, first.Reset.Id);

            byte[] hash2 = SecurityUtil.Sha256(CODE_WRONG);
            authRepository.CreatePasswordResetRequest(email, hash2, expiresAtUtc);

            ResetLookupResult latest = authRepository.ReadLatestReset(email);

            Assert.IsTrue(latest.Found);

            int latestIdFromSql = ExecuteScalarIntByEmail(SQL_SELECT_LATEST_RESET_ID, email);
            Assert.AreEqual(latestIdFromSql, latest.Reset.Id);
            Assert.AreNotEqual(first.Reset.Id, latest.Reset.Id);
        }

        [TestMethod]
        public void ValidateResetCodeOrThrow_WhenWrongTwice_IncrementsAttemptsToTwo()
        {
            string email = CreateUniqueEmail();
            CreateAccountForEmail(email);

            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES);
            byte[] correctHash = SecurityUtil.Sha256(CODE_OK);

            authRepository.CreatePasswordResetRequest(email, correctHash, expiresAtUtc);

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(lookup.Found);

            byte[] wrongHash = SecurityUtil.Sha256(CODE_WRONG);

            CodeValidationResult r1 = authRepository.ValidateResetCodeOrThrow(lookup.Reset.Id, wrongHash);
            CodeValidationResult r2 = authRepository.ValidateResetCodeOrThrow(lookup.Reset.Id, wrongHash);

            Assert.IsFalse(r1.IsValid);
            Assert.IsFalse(r2.IsValid);

            int attempts = ExecuteScalarInt(SQL_SELECT_RESET_ATTEMPTS, lookup.Reset.Id);
            Assert.AreEqual(EXPECTED_ATTEMPTS_2, attempts);
        }

        private void CreateAccountForEmail(string email)
        {
            string passwordHash = passwordService.Hash(PASSWORD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(Array.Empty<byte>(), string.Empty));

            int userId = authRepository.CreateAccountAndUser(data);

            Assert.IsTrue(userId > 0);
        }

        private static string CreateUniqueEmail()
        {
            string suffix = Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);
            return string.Concat("user+", suffix, EMAIL_DOMAIN);
        }

        private static int ExecuteScalarInt(string sql, int id)
        {
            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = SQL_TIMEOUT_SECONDS;
                cmd.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = id;

                connection.Open();

                object result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(result);
            }
        }

        private static void ExecuteNonQuery(string sql, int id)
        {
            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = SQL_TIMEOUT_SECONDS;
                cmd.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = id;

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static int ExecuteScalarIntByEmail(string sql, string email)
        {
            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = SQL_TIMEOUT_SECONDS;

                cmd.Parameters.Add(
                    AuthServiceConstants.PARAMETER_EMAIL,
                    SqlDbType.NVarChar,
                    AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                connection.Open();

                object result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(result);
            }
        }
    }
}
