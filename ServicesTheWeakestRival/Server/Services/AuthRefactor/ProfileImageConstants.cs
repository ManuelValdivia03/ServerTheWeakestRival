namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public static class ProfileImageConstants
    {
        public const int ONE_KILOBYTE_BYTES = 1024;

        public const int DEFAULT_MAX_IMAGE_KB = 512;
        public const int DEFAULT_MAX_IMAGE_BYTES = DEFAULT_MAX_IMAGE_KB * ONE_KILOBYTE_BYTES;

        public const int CONTENT_TYPE_MAX_LENGTH = 50;

        public const string CONTENT_TYPE_PNG = "image/png";
        public const string CONTENT_TYPE_JPEG = "image/jpeg";
    }
}
