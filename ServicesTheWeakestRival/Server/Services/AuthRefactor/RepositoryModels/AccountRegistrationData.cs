using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels
{
    public sealed class ProfileImagePayload
    {
        public byte[] Bytes { get; }
        public string ContentType { get; }

        public bool HasImage => Bytes.Length > 0;

        public ProfileImagePayload(byte[] bytes, string contentType)
        {
            Bytes = bytes ?? Array.Empty<byte>();
            ContentType = contentType ?? string.Empty;
        }
    }

    public sealed class AccountRegistrationData
    {
        public string Email { get; }
        public string PasswordHash { get; }
        public string DisplayName { get; }
        public ProfileImagePayload ProfileImage { get; }

        public AccountRegistrationData(
            string email,
            string passwordHash,
            string displayName,
            ProfileImagePayload profileImage)
        {
            Email = email ?? string.Empty;
            PasswordHash = passwordHash ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ProfileImage = profileImage ?? new ProfileImagePayload(Array.Empty<byte>(), string.Empty);
        }
    }
}
