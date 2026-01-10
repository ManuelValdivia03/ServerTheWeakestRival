// LobbyMappers.cs
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
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

            if (members == null)
            {
                return accountMinis;
            }

            if (avatarSql == null)
            {
                Logger.Warn("MapToAccountMini: avatarSql is null. Returning empty list.");
                return accountMinis;
            }

            foreach (LobbyMembers member in members)
            {
                if (member == null)
                {
                    continue;
                }

                if (!member.is_active || member.left_at_utc.HasValue)
                {
                    continue;
                }

                if (member.Users == null)
                {
                    Logger.WarnFormat(
                        "MapToAccountMini: Users is null for member.user_id={0}.",
                        member.user_id);
                    continue;
                }

                UserAvatarEntity avatarEntity = avatarSql.GetByUserId(member.user_id);

                byte[] profileImageBytes = member.Users.profile_image ?? Array.Empty<byte>();
                bool hasProfileImage = profileImageBytes.Length > 0;

                accountMinis.Add(
                    new AccountMini
                    {
                        AccountId = member.user_id,
                        DisplayName = string.IsNullOrWhiteSpace(member.Users.display_name)
                            ? string.Concat(LobbyServiceConstants.DEFAULT_PLAYER_NAME_PREFIX, member.user_id)
                            : member.Users.display_name,
                        Email = member.Users.Accounts.email ?? string.Empty,
                        HasProfileImage = hasProfileImage,
                        ProfileImageCode = string.Empty,
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
                if (account == null)
                {
                    continue;
                }

                players.Add(
                    new PlayerSummary
                    {
                        UserId = account.AccountId,
                        DisplayName = account.DisplayName ?? string.Empty,
                        IsOnline = true,
                        Avatar = account.Avatar
                    });
            }

            return players;
        }
    }
}
