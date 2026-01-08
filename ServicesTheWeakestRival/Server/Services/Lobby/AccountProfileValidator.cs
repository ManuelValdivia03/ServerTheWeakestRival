using System;
using System.Net.Mail;

namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public static class AccountProfileValidator
    {
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

            if (data.HasProfileImageChange && data.ProfileImageUrlLength > LobbyServiceConstants.MAX_PROFILE_IMAGE_URL_LENGTH)
            {
                throw LobbyServiceContext.ThrowFault(
                    LobbyServiceConstants.ERROR_VALIDATION_ERROR,
                    string.Format("ProfileImageUrl máximo {0}.", LobbyServiceConstants.MAX_PROFILE_IMAGE_URL_LENGTH));
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
        }
    }

    public sealed class UpdateAccountRequestData
    {
        public bool HasDisplayNameChange { get; set; }
        public bool HasProfileImageChange { get; set; }

        public int DisplayNameLength { get; set; }
        public int ProfileImageUrlLength { get; set; }
    }
}
