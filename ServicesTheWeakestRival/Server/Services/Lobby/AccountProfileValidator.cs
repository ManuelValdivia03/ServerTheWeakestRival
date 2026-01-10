using System;
using System.Net.Mail;

namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public static class AccountProfileValidator
    {
        private const int MAX_PROFILE_IMAGE_BYTES = 524288; // 512 KB
        private const int MAX_PROFILE_IMAGE_CONTENT_TYPE_LENGTH = 64;

        private const string CONTENT_TYPE_PNG = "image/png";
        private const string CONTENT_TYPE_JPEG = "image/jpeg";

        public static void ValidateProfileChanges(UpdateAccountRequestData data)
        {
            if (data == null)
            {
                throw LobbyServiceContext.ThrowFault(LobbyServiceConstants.ERROR_INVALID_REQUEST, "Request nulo.");
            }

            if (data.HasDisplayNameChange && data.DisplayNameLength > LobbyServiceConstants.MAX_DISPLAY_NAME_LENGTH)
            {
                throw LobbyServiceContext.ThrowFault(
                    LobbyServiceConstants.ERROR_VALIDATION_ERROR,
                    string.Format("DisplayName máximo {0}.", LobbyServiceConstants.MAX_DISPLAY_NAME_LENGTH));
            }

            if (!data.HasProfileImageChange)
            {
                return;
            }

            if (data.RemoveProfileImage)
            {
                return;
            }

            if (data.ProfileImageBytesLength <= 0)
            {
                throw LobbyServiceContext.ThrowFault(
                    LobbyServiceConstants.ERROR_VALIDATION_ERROR,
                    "Imagen de perfil inválida.");
            }

            if (data.ProfileImageBytesLength > MAX_PROFILE_IMAGE_BYTES)
            {
                throw LobbyServiceContext.ThrowFault(
                    LobbyServiceConstants.ERROR_VALIDATION_ERROR,
                    string.Format("La imagen de perfil excede el tamaño máximo ({0} KB).", MAX_PROFILE_IMAGE_BYTES / 1024));
            }

            if (data.ProfileImageContentTypeLength <= 0 || data.ProfileImageContentTypeLength > MAX_PROFILE_IMAGE_CONTENT_TYPE_LENGTH)
            {
                throw LobbyServiceContext.ThrowFault(
                    LobbyServiceConstants.ERROR_VALIDATION_ERROR,
                    "Tipo de imagen de perfil inválido.");
            }

            if (!IsAllowedContentType(data.ProfileImageContentType))
            {
                throw LobbyServiceContext.ThrowFault(
                    LobbyServiceConstants.ERROR_VALIDATION_ERROR,
                    "Solo se permite PNG o JPG.");
            }
        }

        public static string ValidateAndNormalizeEmail(string email)
        {
            string trimmedEmail = (email ?? string.Empty).Trim();

            if (!IsValidEmail(trimmedEmail))
            {
                throw LobbyServiceContext.ThrowFault(LobbyServiceConstants.ERROR_VALIDATION_ERROR, "Email inválido.");
            }

            if (trimmedEmail.Length > LobbyServiceConstants.MAX_EMAIL_LENGTH)
            {
                throw LobbyServiceContext.ThrowFault(
                    LobbyServiceConstants.ERROR_VALIDATION_ERROR,
                    string.Format("Email máximo {0}.", LobbyServiceConstants.MAX_EMAIL_LENGTH));
            }

            return trimmedEmail;
        }

        private static bool IsAllowedContentType(string contentType)
        {
            return string.Equals(contentType, CONTENT_TYPE_PNG, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, CONTENT_TYPE_JPEG, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                _ = new MailAddress(email);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    public sealed class UpdateAccountRequestData
    {
        public bool HasDisplayNameChange { get; set; }
        public bool HasProfileImageChange { get; set; }
        public bool RemoveProfileImage { get; set; }

        public int DisplayNameLength { get; set; }

        public int ProfileImageBytesLength { get; set; }
        public int ProfileImageContentTypeLength { get; set; }
        public string ProfileImageContentType { get; set; }
    }
}
