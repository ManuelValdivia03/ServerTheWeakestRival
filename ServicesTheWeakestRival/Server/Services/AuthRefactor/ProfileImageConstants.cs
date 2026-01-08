namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    internal static class ProfileImageConstants
    {
        internal const int ONE_KILOBYTE_BYTES = 1024;

        internal const int DEFAULT_MAX_IMAGE_KB = 512;
        internal const int DEFAULT_MAX_IMAGE_BYTES = DEFAULT_MAX_IMAGE_KB * ONE_KILOBYTE_BYTES;

        internal const int CONTENT_TYPE_MAX_LENGTH = 50;

        internal const string CONTENT_TYPE_PNG = "image/png";
        internal const string CONTENT_TYPE_JPEG = "image/jpeg";
    }
}
