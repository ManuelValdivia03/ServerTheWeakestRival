using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class ProfileImagePolicyTests
    {
        private const int EMPTY_LENGTH = 0;

        private const string MSG_TOO_LARGE_CONTAINS = "La imagen de perfil es demasiado grande. El máximo permitido es 512 KB.";
        private const string MSG_CONTENT_TYPE_REQUIRED = "El tipo de contenido de la imagen de perfil es obligatorio.";
        private const string MSG_ONLY_PNG_JPG = "Solo se permiten imágenes de perfil PNG y JPG.";
        private const string MSG_SIGNATURE_MISMATCH = "El archivo de la imagen de perfil no coincide con el formato declarado.";

        private static readonly byte[] PngMinimalValidBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        [TestMethod]
        public void ValidateOrThrow_WhenImageBytesNull_DoesNotThrow()
        {
            ProfileImagePolicy.ValidateOrThrow(null, null);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenImageBytesEmpty_DoesNotThrow()
        {
            ProfileImagePolicy.ValidateOrThrow(Array.Empty<byte>(), null);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenPngIsValid_DoesNotThrow()
        {
            ProfileImagePolicy.ValidateOrThrow(PngMinimalValidBytes, ProfileImageConstants.CONTENT_TYPE_PNG);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenImageTooLarge_ThrowsInvalidRequest()
        {
            int maxBytes = ProfileImageConstants.DEFAULT_MAX_IMAGE_BYTES;

            byte[] tooLarge = new byte[maxBytes + 1];
            CopyPrefix(PngMinimalValidBytes, tooLarge);

            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImagePolicy.ValidateOrThrow(tooLarge, ProfileImageConstants.CONTENT_TYPE_PNG));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            StringAssert.Contains(fault.Message, MSG_TOO_LARGE_CONTAINS);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenContentTypeMissing_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImagePolicy.ValidateOrThrow(PngMinimalValidBytes, string.Empty));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(MSG_CONTENT_TYPE_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenContentTypeNotAllowed_ThrowsInvalidRequest()
        {
            const string contentTypeNotAllowed = "image/gif";

            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImagePolicy.ValidateOrThrow(PngMinimalValidBytes, contentTypeNotAllowed));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(MSG_ONLY_PNG_JPG, fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenSignatureDoesNotMatchDeclaredFormat_ThrowsInvalidRequest()
        {
            byte[] invalidForPng = new byte[PngMinimalValidBytes.Length];
            Array.Copy(PngMinimalValidBytes, invalidForPng, PngMinimalValidBytes.Length);

            invalidForPng[0] = 0x00;

            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImagePolicy.ValidateOrThrow(invalidForPng, ProfileImageConstants.CONTENT_TYPE_PNG));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(MSG_SIGNATURE_MISMATCH, fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenImageLengthEqualsMax_DoesNotThrow()
        {
            int maxBytes = ProfileImageConstants.DEFAULT_MAX_IMAGE_BYTES;

            byte[] exactMax = new byte[maxBytes];
            CopyPrefix(PngMinimalValidBytes, exactMax);

            ProfileImagePolicy.ValidateOrThrow(exactMax, ProfileImageConstants.CONTENT_TYPE_PNG);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenBytesProvidedButContentTypeIsNull_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImagePolicy.ValidateOrThrow(PngMinimalValidBytes, null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(MSG_CONTENT_TYPE_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenBytesProvidedButContentTypeIsWhitespace_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImagePolicy.ValidateOrThrow(PngMinimalValidBytes, "   "));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(MSG_CONTENT_TYPE_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenEmptyBytesButContentTypeProvided_DoesNotThrow()
        {
            ProfileImagePolicy.ValidateOrThrow(Array.Empty<byte>(), "image/gif");
        }

        [TestMethod]
        public void ValidateOrThrow_WhenBytesTooShortForSignature_ThrowsInvalidRequestSignatureMismatch()
        {
            byte[] tooShort = new byte[] { 0x89 };

            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImagePolicy.ValidateOrThrow(tooShort, ProfileImageConstants.CONTENT_TYPE_PNG));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(MSG_SIGNATURE_MISMATCH, fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenJpgIsValid_DoesNotThrow()
        {
            const string contentTypeJpg = "image/jpeg";

            byte[] jpgMinimal = new byte[]
            {
                0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10,
                0x4A, 0x46, 0x49, 0x46, 0x00, 
                0x01, 0x01, 0x00,
                0xFF, 0xD9
            };

            ProfileImagePolicy.ValidateOrThrow(jpgMinimal, contentTypeJpg);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenDeclaredJpgButBytesArePng_ThrowsInvalidRequestSignatureMismatch()
        {
            const string contentTypeJpg = "image/jpeg";

            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImagePolicy.ValidateOrThrow(PngMinimalValidBytes, contentTypeJpg));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(MSG_SIGNATURE_MISMATCH, fault.Message);
        }

        [TestMethod]
        public void ValidateOrThrow_WhenDeclaredPngButBytesAreJpg_ThrowsInvalidRequestSignatureMismatch()
        {
            byte[] jpgMinimal = new byte[]
            {
                0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10,
                0x4A, 0x46, 0x49, 0x46, 0x00,
                0x01, 0x01, 0x00,
                0xFF, 0xD9
            };

            ServiceFault fault = FaultAssert.Capture(() =>
                ProfileImagePolicy.ValidateOrThrow(jpgMinimal, ProfileImageConstants.CONTENT_TYPE_PNG));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(MSG_SIGNATURE_MISMATCH, fault.Message);
        }


        private static void CopyPrefix(byte[] source, byte[] destination)
        {
            if (source == null || destination == null || destination.Length == EMPTY_LENGTH)
            {
                return;
            }

            int lengthToCopy = Math.Min(source.Length, destination.Length);
            Array.Copy(source, destination, lengthToCopy);
        }
    }
}
