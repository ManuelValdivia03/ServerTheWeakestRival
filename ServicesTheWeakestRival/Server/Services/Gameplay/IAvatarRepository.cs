using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal interface IAvatarRepository
    {
        AvatarAppearanceDto LoadAvatarByUserId(int userId);
    }
}
