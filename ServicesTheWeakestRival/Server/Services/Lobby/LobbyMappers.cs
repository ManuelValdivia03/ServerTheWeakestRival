using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System.Collections.Generic;
using TheWeakestRival.Data;

namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public static class LobbyMappers
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyMappers));

        public static AvatarAppearanceDto MapAvatar(UserAvatarEntity entity)
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

        public static List<AccountMini> MapToAccountMini(List<LobbyMembers> members, UserAvatarSql avatarSql)
        {
            var accountMinis = new List<AccountMini>();

            foreach (LobbyMembers member in members)
            {
                if (!member.is_active || member.left_at_utc.HasValue)
                {
                    continue;
                }

                if (member.Users == null)
                {
                    Logger.WarnFormat(
                        "MapToAccountMini: Usuario nulo para member.user_id={0}",
                        member.user_id);
                    continue;
                }

                UserAvatarEntity avatarEntity = avatarSql.GetByUserId(member.user_id);

                accountMinis.Add(
                    new AccountMini
                    {
                        AccountId = member.user_id,
                        DisplayName = member.Users.display_name ?? (LobbyServiceConstants.DEFAULT_PLAYER_NAME_PREFIX + member.user_id),
                        AvatarUrl = member.Users.profile_image_url,
                        Avatar = MapAvatar(avatarEntity)
                    });
            }

            return accountMinis;
        }

        public static List<PlayerSummary> MapToPlayerSummaries(List<AccountMini> accounts)
        {
            var players = new List<PlayerSummary>();

            if (accounts == null)
            {
                return players;
            }

            foreach (AccountMini account in accounts)
            {
                players.Add(
                    new PlayerSummary
                    {
                        UserId = account.AccountId,
                        DisplayName = account.DisplayName,
                        IsOnline = true,
                        Avatar = account.Avatar
                    });
            }

            return players;
        }
    }
}
