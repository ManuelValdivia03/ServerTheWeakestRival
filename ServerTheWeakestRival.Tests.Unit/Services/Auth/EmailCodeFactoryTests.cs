using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Email;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class EmailCodeFactoryTests
    {
        private const int EXPECTED_CODE_LENGTH = AuthServiceConstants.EMAIL_CODE_LENGTH;

        [TestMethod]
        public void Create_WhenCalled_ReturnsNonNullInfo()
        {
            EmailCodeInfo info = EmailCodeFactory.Create();

            Assert.IsNotNull(info);
        }

        [TestMethod]
        public void Create_WhenCalled_ReturnsNumericCodeWithExpectedLength()
        {
            EmailCodeInfo info = EmailCodeFactory.Create();

            Assert.AreEqual(EXPECTED_CODE_LENGTH, info.Code.Length);

            foreach (char c in info.Code)
            {
                Assert.IsTrue(char.IsDigit(c));
            }
        }

        [TestMethod]
        public void Create_WhenCalled_ReturnsHashWithExpectedLength()
        {
            EmailCodeInfo info = EmailCodeFactory.Create();

            Assert.IsNotNull(info.Hash);
            Assert.AreEqual(AuthServiceConstants.SHA256_HASH_BYTES, info.Hash.Length);
        }

        [TestMethod]
        public void Create_WhenCalled_SetsExpiresAtUtcInFuture()
        {
            DateTime before = DateTime.UtcNow;
            EmailCodeInfo info = EmailCodeFactory.Create();
            DateTime after = DateTime.UtcNow;

            Assert.IsTrue(info.ExpiresAtUtc > before);
            Assert.IsTrue(info.ExpiresAtUtc > after.AddSeconds(-1));
        }

        [TestMethod]
        public void Create_WhenCalled_HashMatchesSha256OfCode()
        {
            EmailCodeInfo info = EmailCodeFactory.Create();

            byte[] expectedHash = SecurityUtil.Sha256(info.Code);

            CollectionAssert.AreEqual(expectedHash, info.Hash);
        }

        [TestMethod]
        public void Create_WhenCalled_ExpiresAtUtcIsApproximatelyNowPlusTtlMinutes()
        {
            const int TOLERANCE_SECONDS = 5;

            DateTime before = DateTime.UtcNow;
            EmailCodeInfo info = EmailCodeFactory.Create();
            DateTime expected = before.AddMinutes(AuthServiceContext.CodeTtlMinutes);

            Assert.IsTrue(
                info.ExpiresAtUtc >= expected.AddSeconds(-TOLERANCE_SECONDS),
                "ExpiresAtUtc is earlier than expected tolerance window.");

            Assert.IsTrue(
                info.ExpiresAtUtc <= expected.AddSeconds(TOLERANCE_SECONDS),
                "ExpiresAtUtc is later than expected tolerance window.");
        }

        [TestMethod]
        public void Create_WhenCalled_ExpiresAtUtcIsUtcKind()
        {
            EmailCodeInfo info = EmailCodeFactory.Create();

            Assert.AreEqual(DateTimeKind.Utc, info.ExpiresAtUtc.Kind);
        }

        [TestMethod]
        public void Create_WhenCalledTwice_ReturnsDifferentInstances()
        {
            EmailCodeInfo a = EmailCodeFactory.Create();
            EmailCodeInfo b = EmailCodeFactory.Create();

            Assert.AreNotSame(a, b);
        }
    }
}
