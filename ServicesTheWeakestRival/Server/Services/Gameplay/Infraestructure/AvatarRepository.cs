using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using TheWeakestRival.Data;

namespace ServicesTheWeakestRival.Server.Services.Gameplay.Infrastructure
{
    internal sealed class AvatarRepository : IAvatarRepository
    {
        public AvatarAppearanceDto LoadAvatarByUserId(int userId)
        {
            var avatarSql = new UserAvatarSql(GameplayServiceContext.GetConnectionString());
            UserAvatarEntity avatarEntity = avatarSql.GetByUserId(userId);
            return MapAvatar(avatarEntity);
        }

        private static AvatarAppearanceDto MapAvatar(UserAvatarEntity entity)
        {
            if (entity == null)
            {
                return new AvatarAppearanceDto
                {
                    BodyColor = AvatarBodyColor.Blue,
                    PantsColor = AvatarPantsColor.Black,
                    HatType = AvatarHatType.None,
                    HatColor = AvatarHatColor.Default,
                    FaceType = AvatarFaceType.Default,
                    UseProfilePhotoAsFace = false
                };
            }

            return new AvatarAppearanceDto
            {
                BodyColor = (AvatarBodyColor)entity.BodyColor,
                PantsColor = (AvatarPantsColor)entity.PantsColor,
                HatType = (AvatarHatType)entity.HatType,
                HatColor = (AvatarHatColor)entity.HatColor,
                FaceType = (AvatarFaceType)entity.FaceType,
                UseProfilePhotoAsFace = entity.UseProfilePhoto
            };
        }
    }
}
