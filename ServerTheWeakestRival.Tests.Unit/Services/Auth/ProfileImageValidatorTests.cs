using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class ProfileImageValidatorTests
    {
        private const int MAX_BYTES = ProfileImageConstants.DEFAULT_MAX_IMAGE_BYTES;

        private const string CONTENT_TYPE_PNG = ProfileImageConstants.CONTENT_TYPE_PNG;
        private const string CONTENT_TYPE_JPEG = ProfileImageConstants.CONTENT_TYPE_JPEG;

        private const string CONTENT_TYPE_EMPTY = "";
        private const string CONTENT_TYPE_UNSUPPORTED = "image/gif";

        private static readonly byte[] PNG_BYTES_VALID = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x01, 0x02, 0x03
        };

        private static readonly byte[] JPEG_BYTES_VALID = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46
        };


        private static readonly byte[] PNG_BYTES_BAD_SIGNATURE = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x02
        };

        private static readonly byte[] JPEG_BYTES_BAD_SIGNATURE = new byte[]
        {
            0xFF, 0x00, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46
        };

        [TestMethod]
        public void ValidateOrThrow_WhenImageBytesIsNull_DoesNotThrow()
        {
            AssertDoesNotThrow(() => ProfileImageValidator.ValidateOrThrow(null, null, MAX_BYTES));
        }

        [TestMethod]
        public void ValidateOrThrow_WhenImageBytesIsEmpty_DoesNotThrow()
        {
            AssertDoesNotThrow(() =>
                ProfileImageValidator.ValidateOrThrow(Array.Empty<byte>(), CONTENT_TYPE_PNG, MAX_BYTES));
        }

        [TestMethod]
        public void ValidateOrThrow_WhenImageTooLarge_ThrowsInvalidRequest()
        {
            byte[] largeBytes = new byte[MAX_BYTES + 1];
            largeBytes[0] = 0x89;

            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImageValidator.ValidateOrThrow(largeBytes, CONTENT_TYPE_PNG, MAX_BYTES));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            StringAssert.Contains(fault.Message, "Profile image is too large.");
        }

        [TestMethod]
        public void ValidateOrThrow_WhenContentTypeMissing_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImageValidator.ValidateOrThrow(PNG_BYTES_VALID, CONTENT_TYPE_EMPTY, MAX_BYTES));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image content type is required.", fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenContentTypeUnsupported_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImageValidator.ValidateOrThrow(PNG_BYTES_VALID, CONTENT_TYPE_UNSUPPORTED, MAX_BYTES));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Only PNG and JPG profile images are allowed.", fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenPngSignatureDoesNotMatch_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImageValidator.ValidateOrThrow(PNG_BYTES_BAD_SIGNATURE, CONTENT_TYPE_PNG, MAX_BYTES));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image file does not match the declared format.", fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenJpegSignatureDoesNotMatch_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImageValidator.ValidateOrThrow(JPEG_BYTES_BAD_SIGNATURE, CONTENT_TYPE_JPEG, MAX_BYTES));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image file does not match the declared format.", fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenPngIsValid_DoesNotThrow()
        {
            AssertDoesNotThrow(() =>
                ProfileImageValidator.ValidateOrThrow(PNG_BYTES_VALID, CONTENT_TYPE_PNG, MAX_BYTES));
        }


        [TestMethod]
        public void ValidateOrThrow_WhenJpegIsValid_DoesNotThrow()
        {
            AssertDoesNotThrow(() =>
                ProfileImageValidator.ValidateOrThrow(JPEG_BYTES_VALID, CONTENT_TYPE_JPEG, MAX_BYTES));
        }

        [TestMethod]
        public void ValidateOrThrow_WhenContentTypeIsNull_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImageValidator.ValidateOrThrow(PNG_BYTES_VALID, null, MAX_BYTES));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image content type is required.", fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenContentTypeIsWhitespace_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImageValidator.ValidateOrThrow(PNG_BYTES_VALID, "   ", MAX_BYTES));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image content type is required.", fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenBytesLengthEqualsMaxBytes_DoesNotThrow()
        {
            byte[] exactMax = new byte[MAX_BYTES];
            Array.Copy(PNG_BYTES_VALID, exactMax, Math.Min(PNG_BYTES_VALID.Length, exactMax.Length));

            AssertDoesNotThrow(() =>
                ProfileImageValidator.ValidateOrThrow(exactMax, CONTENT_TYPE_PNG, MAX_BYTES));
        }

        [TestMethod]
        public void ValidateOrThrow_WhenBytesTooShortForPngSignature_ThrowsInvalidRequest()
        {
            byte[] tooShort = new byte[] { 0x89, 0x50 };

            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImageValidator.ValidateOrThrow(tooShort, CONTENT_TYPE_PNG, MAX_BYTES));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image file does not match the declared format.", fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenDeclaredJpegButBytesArePng_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImageValidator.ValidateOrThrow(PNG_BYTES_VALID, CONTENT_TYPE_JPEG, MAX_BYTES));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image file does not match the declared format.", fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenDeclaredPngButBytesAreJpeg_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImageValidator.ValidateOrThrow(JPEG_BYTES_VALID, CONTENT_TYPE_PNG, MAX_BYTES));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual("Profile image file does not match the declared format.", fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenImageBytesEmpty_AndContentTypeNull_DoesNotThrow()
        {
            AssertDoesNotThrow(() =>
                ProfileImageValidator.ValidateOrThrow(Array.Empty<byte>(), null, MAX_BYTES));
        }

        private static void AssertDoesNotThrow(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Assert.Fail("Expected no exception, but got: " + ex.GetType().Name + " - " + ex.Message);
            }
        }


    }
}
