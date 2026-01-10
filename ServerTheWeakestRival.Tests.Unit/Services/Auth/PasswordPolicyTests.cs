using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class PasswordPolicyTests
    {
        private const string PASSWORD_STRONG = "Password123!";
        private const string PASSWORD_WEAK = "short";
        private const string PASSWORD_WRONG = "WrongPassword123!";

        private const string EMPTY = "";
        private const string WHITESPACE = " ";

        private PasswordService passwordService;
        private PasswordPolicy passwordPolicy;

        [TestInitialize]
        public void SetUp()
        {
            passwordService = new PasswordService(AuthServiceConstants.PASSWORD_MIN_LENGTH);
            passwordPolicy = new PasswordPolicy(passwordService);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenPasswordIsNull_ThrowsWeakPassword()
        {
            var fault = FaultAssert.Capture(() => passwordPolicy.ValidateOrThrow(null));

            AssertWeakPasswordFault(fault);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenPasswordIsEmpty_ThrowsWeakPassword()
        {
            var fault = FaultAssert.Capture(() => passwordPolicy.ValidateOrThrow(EMPTY));

            AssertWeakPasswordFault(fault);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenPasswordIsWhitespace_ThrowsWeakPassword()
        {
            var fault = FaultAssert.Capture(() => passwordPolicy.ValidateOrThrow(WHITESPACE));

            AssertWeakPasswordFault(fault);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenPasswordIsWeak_ThrowsWeakPassword()
        {
            var fault = FaultAssert.Capture(() => passwordPolicy.ValidateOrThrow(PASSWORD_WEAK));

            AssertWeakPasswordFault(fault);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenPasswordIsStrong_DoesNotThrow()
        {
            passwordPolicy.ValidateOrThrow(PASSWORD_STRONG);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void VerifyOrThrow_WhenStoredHashIsEmpty_ThrowsInvalidCredentials()
        {
            var fault = FaultAssert.Capture(() => passwordPolicy.VerifyOrThrow(PASSWORD_STRONG, string.Empty));

            AssertInvalidCredentialsFault(fault);
        }

        [TestMethod]
        public void VerifyOrThrow_WhenStoredHashIsWhitespace_ThrowsInvalidCredentials()
        {
            var fault = FaultAssert.Capture(() => passwordPolicy.VerifyOrThrow(PASSWORD_STRONG, WHITESPACE));

            AssertInvalidCredentialsFault(fault);
        }

        [TestMethod]
        public void VerifyOrThrow_WhenPasswordHasSurroundingSpaces_ThrowsInvalidCredentials()
        {
            string storedHash = passwordService.Hash(PASSWORD_STRONG);

            string passwordWithSpaces = "  " + PASSWORD_STRONG + "  ";

            var fault = FaultAssert.Capture(() => passwordPolicy.VerifyOrThrow(passwordWithSpaces, storedHash));

            AssertInvalidCredentialsFault(fault);
        }

        [TestMethod]
        public void VerifyOrThrow_WhenPasswordIsIncorrect_ThrowsInvalidCredentials()
        {
            string storedHash = passwordService.Hash(PASSWORD_STRONG);

            var fault = FaultAssert.Capture(() => passwordPolicy.VerifyOrThrow(PASSWORD_WRONG, storedHash));

            AssertInvalidCredentialsFault(fault);
        }

        [TestMethod]
        public void VerifyOrThrow_WhenPasswordIsCorrect_DoesNotThrow()
        {
            string storedHash = passwordService.Hash(PASSWORD_STRONG);

            passwordPolicy.VerifyOrThrow(PASSWORD_STRONG, storedHash);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void VerifyOrThrow_WhenStoredHashIsNull_ThrowsInvalidCredentials()
        {
            var fault = FaultAssert.Capture(() => passwordPolicy.VerifyOrThrow(PASSWORD_STRONG, null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }

        [TestMethod]
        public void VerifyOrThrow_WhenStoredHashIsMalformed_ThrowsInvalidCredentials()
        {
            const string HASH_MALFORMED = "not-a-bcrypt-hash";

            var fault = FaultAssert.Capture(() => passwordPolicy.VerifyOrThrow(PASSWORD_STRONG, HASH_MALFORMED));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }


        private static void AssertWeakPasswordFault(ServicesTheWeakestRival.Contracts.Data.ServiceFault fault)
        {
            Assert.AreEqual(AuthServiceConstants.ERROR_WEAK_PASSWORD, fault.Code);

            string expectedMessage = string.Format(
                AuthServiceConstants.MESSAGE_PASSWORD_MIN_LENGTH_NOT_MET,
                AuthServiceConstants.PASSWORD_MIN_LENGTH);

            Assert.AreEqual(expectedMessage, fault.Message);
        }

        private static void AssertInvalidCredentialsFault(ServicesTheWeakestRival.Contracts.Data.ServiceFault fault)
        {
            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_CREDENTIALS, fault.Message);
        }
    }
}
