namespace ServicesTheWeakestRival.Contracts.Data
{
    public sealed class UpdateAvatarRequest
    {
        public string Token { get; set; }

        public byte BodyColor { get; set; }
        public byte PantsColor { get; set; }
        public byte HatType { get; set; }
        public byte HatColor { get; set; }
        public byte FaceType { get; set; }
        public bool UseProfilePhotoAsFace { get; set; }
    }
}
