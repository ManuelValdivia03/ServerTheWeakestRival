using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public static class ProfileImageValidator
    {
        public static void ValidateOrThrow(byte[] imageBytes, string contentType, int maxBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return;
            }

            if (imageBytes.Length > maxBytes)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    string.Format("Profile image is too large. Max allowed is {0} KB.", maxBytes / ProfileImageConstants.ONE_KILOBYTE_BYTES));
            }

            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    "Profile image content type is required.");
            }

            bool isAllowedType =
                string.Equals(contentType, ProfileImageConstants.CONTENT_TYPE_PNG, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contentType, ProfileImageConstants.CONTENT_TYPE_JPEG, StringComparison.OrdinalIgnoreCase);

            if (!isAllowedType)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    "Only PNG and JPG profile images are allowed.");
            }

            if (!MatchesSignature(imageBytes, contentType))
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    "Profile image file does not match the declared format.");
            }
        }

        private static bool MatchesSignature(byte[] bytes, string contentType)
        {
            if (bytes == null || bytes.Length < 8)
            {
                return false;
            }

            if (string.Equals(contentType, ProfileImageConstants.CONTENT_TYPE_PNG, StringComparison.OrdinalIgnoreCase))
            {
                return bytes[0] == 0x89 &&
                       bytes[1] == 0x50 &&
                       bytes[2] == 0x4E &&
                       bytes[3] == 0x47 &&
                       bytes[4] == 0x0D &&
                       bytes[5] == 0x0A &&
                       bytes[6] == 0x1A &&
                       bytes[7] == 0x0A;
            }

            if (string.Equals(contentType, ProfileImageConstants.CONTENT_TYPE_JPEG, StringComparison.OrdinalIgnoreCase))
            {
                return bytes[0] == 0xFF && bytes[1] == 0xD8;
            }

            return false;
        }
    }
}
