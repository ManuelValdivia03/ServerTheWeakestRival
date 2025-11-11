using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public enum AvatarBodyColor
    {
        [EnumMember] Red = 0,
        [EnumMember] Blue = 1,
        [EnumMember] Green = 2,
        [EnumMember] Yellow = 3,
        [EnumMember] Purple = 4,
        [EnumMember] Gray = 5
    }

    [DataContract]
    public enum AvatarPantsColor
    {
        [EnumMember] Black = 0,
        [EnumMember] DarkGray = 1,
        [EnumMember] BlueJeans = 2
    }

    [DataContract]
    public enum AvatarHatType
    {
        [EnumMember] None = 0,
        [EnumMember] Cap = 1,
        [EnumMember] TopHat = 2,
        [EnumMember] Beanie = 3
    }

    [DataContract]
    public enum AvatarHatColor
    {
        [EnumMember] Default = 0,
        [EnumMember] Red = 1,
        [EnumMember] Blue = 2,
        [EnumMember] Black = 3
    }

    [DataContract]
    public enum AvatarFaceType
    {
        [EnumMember] Default = 0,
        [EnumMember] Angry = 1,
        [EnumMember] Happy = 2,
        [EnumMember] Sleepy = 3
    }


    [DataContract]
    public sealed class AvatarAppearanceDto
    {
        [DataMember]
        public AvatarBodyColor BodyColor { get; set; }

        [DataMember]
        public AvatarPantsColor PantsColor { get; set; }

        [DataMember]
        public AvatarHatType HatType { get; set; }

        [DataMember]
        public AvatarHatColor HatColor { get; set; }

        [DataMember]
        public AvatarFaceType FaceType { get; set; }

        [DataMember]
        public bool UseProfilePhotoAsFace { get; set; }
    }
}
