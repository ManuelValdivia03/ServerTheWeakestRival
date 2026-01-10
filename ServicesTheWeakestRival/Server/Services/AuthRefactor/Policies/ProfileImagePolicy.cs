namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Policies
{
    public static class ProfileImagePolicy
    {
        public static void ValidateOrThrow(byte[] profileImageBytes, string profileImageContentType)
        {
            int maxBytes = ProfileImageConstants.DEFAULT_MAX_IMAGE_BYTES;
            ProfileImageValidator.ValidateOrThrow(profileImageBytes, profileImageContentType, maxBytes);
        }
    }
}
