using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public static class ProfileImageValidator
    {
        private const int EMPTY_BYTES_LENGTH = 0;

        private const int MIN_SIGNATURE_BYTES = 8;

        private static readonly byte[] PngSignature =
        {
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A
        };

        private static readonly byte[] JpegSignatureStart =
        {
            0xFF, 0xD8
        };

        private const int PNG_SIGNATURE_LENGTH = 8;
        private const int JPEG_SIGNATURE_LENGTH = 2;

        private const int SIGNATURE_INDEX_0 = 0;
        private const int SIGNATURE_INDEX_1 = 1;
        private const int SIGNATURE_INDEX_2 = 2;
        private const int SIGNATURE_INDEX_3 = 3;
        private const int SIGNATURE_INDEX_4 = 4;
        private const int SIGNATURE_INDEX_5 = 5;
        private const int SIGNATURE_INDEX_6 = 6;
        private const int SIGNATURE_INDEX_7 = 7;

        private const string MESSAGE_PROFILE_IMAGE_TOO_LARGE_TEMPLATE =
            "La imagen de perfil es demasiado grande. El máximo permitido es {0} KB.";

        private const string MESSAGE_PROFILE_IMAGE_CONTENT_TYPE_REQUIRED =
            "El tipo de contenido de la imagen de perfil es obligatorio.";

        private const string MESSAGE_PROFILE_IMAGE_ONLY_PNG_JPG_ALLOWED =
            "Solo se permiten imágenes de perfil PNG y JPG.";

        private const string MESSAGE_PROFILE_IMAGE_SIGNATURE_MISMATCH =
            "El archivo de la imagen de perfil no coincide con el formato declarado.";

        public static void ValidateOrThrow(byte[] imageBytes, string contentType, int maxBytes)
        {
            if (imageBytes == null || imageBytes.Length == EMPTY_BYTES_LENGTH)
            {
                return;
            }

            if (imageBytes.Length > maxBytes)
            {
                int maxAllowedKilobytes = maxBytes / ProfileImageConstants.ONE_KILOBYTE_BYTES;

                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    string.Format(MESSAGE_PROFILE_IMAGE_TOO_LARGE_TEMPLATE, maxAllowedKilobytes));
            }

            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    MESSAGE_PROFILE_IMAGE_CONTENT_TYPE_REQUIRED);
            }

            bool isAllowedType =
                string.Equals(contentType, ProfileImageConstants.CONTENT_TYPE_PNG, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contentType, ProfileImageConstants.CONTENT_TYPE_JPEG, StringComparison.OrdinalIgnoreCase);

            if (!isAllowedType)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    MESSAGE_PROFILE_IMAGE_ONLY_PNG_JPG_ALLOWED);
            }

            if (!MatchesSignature(imageBytes, contentType))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    MESSAGE_PROFILE_IMAGE_SIGNATURE_MISMATCH);
            }
        }

        private static bool MatchesSignature(byte[] bytes, string contentType)
        {
            if (bytes == null || bytes.Length < MIN_SIGNATURE_BYTES)
            {
                return false;
            }

            if (string.Equals(contentType, ProfileImageConstants.CONTENT_TYPE_PNG, StringComparison.OrdinalIgnoreCase))
            {
                return bytes[SIGNATURE_INDEX_0] == PngSignature[SIGNATURE_INDEX_0]
                    && bytes[SIGNATURE_INDEX_1] == PngSignature[SIGNATURE_INDEX_1]
                    && bytes[SIGNATURE_INDEX_2] == PngSignature[SIGNATURE_INDEX_2]
                    && bytes[SIGNATURE_INDEX_3] == PngSignature[SIGNATURE_INDEX_3]
                    && bytes[SIGNATURE_INDEX_4] == PngSignature[SIGNATURE_INDEX_4]
                    && bytes[SIGNATURE_INDEX_5] == PngSignature[SIGNATURE_INDEX_5]
                    && bytes[SIGNATURE_INDEX_6] == PngSignature[SIGNATURE_INDEX_6]
                    && bytes[SIGNATURE_INDEX_7] == PngSignature[SIGNATURE_INDEX_7];
            }

            if (string.Equals(contentType, ProfileImageConstants.CONTENT_TYPE_JPEG, StringComparison.OrdinalIgnoreCase))
            {
                return bytes[SIGNATURE_INDEX_0] == JpegSignatureStart[SIGNATURE_INDEX_0]
                    && bytes[SIGNATURE_INDEX_1] == JpegSignatureStart[SIGNATURE_INDEX_1];
            }

            return false;
        }
    }
}
