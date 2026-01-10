using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Server.Services.Auth;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class PasswordServiceTests
    {
        private const int MIN_LENGTH = 8;

        private const string VALID_PASSWORD = "Abcd1234!";
        private const string VALID_PASSWORD_2 = "AnotherPass123!";

        private const string SHORT_PASSWORD = "123";
        private const string EMPTY = "";
        private const string STORED_HASH_EMPTY = "";

        [TestMethod]
        public void IsValid_WhenPasswordIsNull_ReturnsFalse()
        {
            var service = new PasswordService(MIN_LENGTH);

            bool isValid = service.IsValid(null);

            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void IsValid_WhenPasswordIsWhitespace_ReturnsFalse()
        {
            var service = new PasswordService(MIN_LENGTH);

            bool isValid = service.IsValid("   ");

            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void IsValid_WhenPasswordShorterThanMin_ReturnsFalse()
        {
            var service = new PasswordService(MIN_LENGTH);

            bool isValid = service.IsValid(SHORT_PASSWORD);

            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void IsValid_WhenPasswordMeetsMin_ReturnsTrue()
        {
            var service = new PasswordService(MIN_LENGTH);

            bool isValid = service.IsValid(VALID_PASSWORD);

            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void Hash_WhenPasswordIsNull_ReturnsNonEmptyHash()
        {
            var service = new PasswordService(MIN_LENGTH);

            string hash = service.Hash(null);

            Assert.IsFalse(string.IsNullOrWhiteSpace(hash));
        }

        [TestMethod]
        public void Hash_WhenSamePasswordHashedTwice_ReturnsDifferentHashes()
        {
            var service = new PasswordService(MIN_LENGTH);

            string hash1 = service.Hash(VALID_PASSWORD);
            string hash2 = service.Hash(VALID_PASSWORD);

            Assert.AreNotEqual(hash1, hash2);
        }

        [TestMethod]
        public void Verify_WhenStoredHashEmpty_ReturnsFalse()
        {
            var service = new PasswordService(MIN_LENGTH);

            bool ok = service.Verify(VALID_PASSWORD, STORED_HASH_EMPTY);

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void Verify_WhenPasswordMatchesStoredHash_ReturnsTrue()
        {
            var service = new PasswordService(MIN_LENGTH);

            string hash = service.Hash(VALID_PASSWORD);

            bool ok = service.Verify(VALID_PASSWORD, hash);

            Assert.IsTrue(ok);
        }

        [TestMethod]
        public void Verify_WhenPasswordDoesNotMatchStoredHash_ReturnsFalse()
        {
            var service = new PasswordService(MIN_LENGTH);

            string hash = service.Hash(VALID_PASSWORD);

            bool ok = service.Verify(VALID_PASSWORD_2, hash);

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void Verify_WhenPasswordIsNullAndHashFromEmptyPassword_ReturnsTrue()
        {
            var service = new PasswordService(MIN_LENGTH);

            string emptyPasswordHash = service.Hash(EMPTY);

            bool ok = service.Verify(null, emptyPasswordHash);

            Assert.IsTrue(ok);
        }

        [TestMethod]
        public void Ctor_WhenMinLengthIsZero_ThrowsArgumentOutOfRangeException()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PasswordService(0));
        }

        [TestMethod]
        public void Ctor_WhenMinLengthIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PasswordService(-1));
        }

        [TestMethod]
        public void IsValid_WhenPasswordLengthExactlyMin_ReturnsTrue()
        {
            var service = new PasswordService(MIN_LENGTH);

            string passwordExact = "12345678";

            bool isValid = service.IsValid(passwordExact);

            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void IsValid_WhenPasswordLengthMinMinusOne_ReturnsFalse()
        {
            var service = new PasswordService(MIN_LENGTH);

            string passwordShort = "1234567";

            bool isValid = service.IsValid(passwordShort);

            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void Hash_WhenPasswordIsWhitespace_ReturnsNonEmptyHash()
        {
            var service = new PasswordService(MIN_LENGTH);

            string hash = service.Hash("   ");

            Assert.IsFalse(string.IsNullOrWhiteSpace(hash));
        }

        [TestMethod]
        public void Verify_WhenStoredHashIsNull_ReturnsFalse()
        {
            var service = new PasswordService(MIN_LENGTH);

            bool ok = service.Verify(VALID_PASSWORD, null);

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void Verify_WhenStoredHashIsWhitespace_ReturnsFalse()
        {
            var service = new PasswordService(MIN_LENGTH);

            bool ok = service.Verify(VALID_PASSWORD, "   ");

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void Verify_WhenStoredHashIsMalformed_ReturnsFalse()
        {
            var service = new PasswordService(MIN_LENGTH);

            bool ok = service.Verify(VALID_PASSWORD, "not-a-bcrypt-hash");

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void Verify_WhenHashWasCreatedFromNullPassword_VerifyEmptyPasswordReturnsTrue()
        {
            var service = new PasswordService(MIN_LENGTH);

            string hashFromNull = service.Hash(null);

            bool ok = service.Verify(EMPTY, hashFromNull);

            Assert.IsTrue(ok);
        }

        [TestMethod]
        public void Verify_WhenHashWasCreatedFromEmptyPassword_VerifyNonEmptyPasswordReturnsFalse()
        {
            var service = new PasswordService(MIN_LENGTH);

            string hashFromEmpty = service.Hash(EMPTY);

            bool ok = service.Verify(VALID_PASSWORD, hashFromEmpty);

            Assert.IsFalse(ok);
        }

    }
}
