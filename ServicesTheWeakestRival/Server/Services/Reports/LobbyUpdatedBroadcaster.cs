using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using TheWeakestRival.Data;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class LobbyUpdatedBroadcaster
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyUpdatedBroadcaster));

        private const string ContextBroadcastLobbyUpdated = "LobbyUpdatedBroadcaster.BroadcastLobbyUpdated";
        private const string ContextBuildLobbyInfo = "LobbyUpdatedBroadcaster.BuildLobbyInfo";

        private const string ParamLobbyUid = "@u";
        private const string ParamLobbyId = "@id";
        private const string ParamLobbyIdInt = "@LobbyId";

        private const string LogBroadcastFailedFormat =
            "BroadcastLobbyUpdated: failed callback. LobbyUid={0}, AccountId={1}";

        internal bool TryBroadcastLobbyUpdated(Guid lobbyUid)
        {
            if (lobbyUid == Guid.Empty)
            {
                return false;
            }

            try
            {
                using (SqlConnection connection = CreateOpenConnection())
                {
                    LobbyInfo lobbyInfo = BuildLobbyInfoSafe(connection, lobbyUid);

                    bool canBroadcast = lobbyInfo != null && lobbyInfo.Players != null && lobbyInfo.Players.Count > 0;
                    if (canBroadcast)
                    {
                        BroadcastLobbyUpdatedToPlayers(lobbyUid, lobbyInfo);
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ContextBroadcastLobbyUpdated, ex);
                return false;
            }
        }

        private static SqlConnection CreateOpenConnection()
        {
            string connectionString =
                ConfigurationManager.ConnectionStrings[ReportSql.MAIN_CONNECTION_STRING_NAME ].ConnectionString;

            var connection = new SqlConnection(connectionString);
            connection.Open();

            return connection;
        }

        private static LobbyInfo BuildLobbyInfoSafe(SqlConnection connection, Guid lobbyUid)
        {
            try
            {
                return BuildLobbyInfo(connection, lobbyUid);
            }
            catch (Exception ex)
            {
                Logger.Warn(ContextBuildLobbyInfo, ex);
                return null;
            }
        }

        private static LobbyInfo BuildLobbyInfo(SqlConnection connection, Guid lobbyUid)
        {
            int lobbyId = GetLobbyIdFromUid(connection, lobbyUid);
            if (lobbyId <= 0)
            {
                return null;
            }

            LobbyInfo baseInfo = LoadLobbyInfoByIntId(connection, lobbyId);
            if (baseInfo == null)
            {
                return null;
            }

            List<LobbyMembers> members = GetLobbyMembers(connection, lobbyId);

            var avatarSql = new UserAvatarSql(connection.ConnectionString);
            baseInfo.Players = MapToAccountMini(members, avatarSql);

            return baseInfo;
        }

        private static void BroadcastLobbyUpdatedToPlayers(Guid lobbyUid, LobbyInfo lobbyInfo)
        {
            if (lobbyInfo != null && lobbyInfo.Players != null)
            {
                foreach (AccountMini player in lobbyInfo.Players)
                {
                    if (IsValidAccount(player))
                    {
                        SendLobbyUpdatedToPlayerSafe(lobbyUid, player.AccountId, lobbyInfo);
                    }
                }
            }
        }

        private static bool IsValidAccount(AccountMini player)
        {
            return player != null && player.AccountId > 0;
        }

        private static void SendLobbyUpdatedToPlayerSafe(Guid lobbyUid, int accountId, LobbyInfo lobbyInfo)
        {
            bool hasCallback = LobbyCallbackRegistry.TryGet(accountId, out var callback) && callback != null;
            if (hasCallback)
            {
                try
                {
                    callback.OnLobbyUpdated(lobbyInfo);
                }
                catch (Exception ex)
                {
                    Logger.WarnFormat(LogBroadcastFailedFormat, lobbyUid, accountId);
                    Logger.Warn(ContextBroadcastLobbyUpdated, ex);

                    LobbyCallbackRegistry.Remove(accountId);
                }
            }
        }

        private static int GetLobbyIdFromUid(SqlConnection connection, Guid lobbyUid)
        {
            using (var cmd = new SqlCommand(LobbySql.Text.GET_LOBBY_ID_FROM_UID, connection))
            {
                cmd.Parameters.Add(ParamLobbyUid, SqlDbType.UniqueIdentifier).Value = lobbyUid;

                object obj = cmd.ExecuteScalar();
                if (obj == null || obj == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(obj);
            }
        }

        private static LobbyInfo LoadLobbyInfoByIntId(SqlConnection connection, int lobbyId)
        {
            using (var cmd = new SqlCommand(LobbySql.Text.GET_LOBBY_BY_ID, connection))
            {
                cmd.Parameters.Add(ParamLobbyId, SqlDbType.Int).Value = lobbyId;

                using (var reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new LobbyInfo
                    {
                        LobbyId = reader.GetGuid(0),
                        LobbyName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        MaxPlayers = reader.GetByte(2),
                        AccessCode = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Players = new List<AccountMini>()
                    };
                }
            }
        }

        private static List<LobbyMembers> GetLobbyMembers(SqlConnection connection, int lobbyId)
        {
            var members = new List<LobbyMembers>();

            using (var cmd = new SqlCommand(LobbySql.Text.GET_LOBBY_MEMBERS_WITH_USERS, connection))
            {
                cmd.Parameters.Add(ParamLobbyIdInt, SqlDbType.Int).Value = lobbyId;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        members.Add(
                            new LobbyMembers
                            {
                                lobby_id = reader.GetInt32(0),
                                user_id = reader.GetInt32(1),
                                role = reader.GetByte(2),
                                joined_at_utc = reader.GetDateTime(3),
                                left_at_utc = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                is_active = reader.GetBoolean(5),
                                Users = new Users
                                {
                                    user_id = reader.GetInt32(6),
                                    display_name = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    profile_image = reader.IsDBNull(8) ? null : (byte[])reader.GetValue(8),
                                    profile_image_content_type = reader.IsDBNull(9) ? null : reader.GetString(9)
                                }
                            });
                    }
                }
            }

            return members;
        }

        private static List<AccountMini> MapToAccountMini(List<LobbyMembers> members, UserAvatarSql avatarSql)
        {
            var result = new List<AccountMini>();

            if (members != null)
            {
                foreach (LobbyMembers member in members)
                {
                    if (member?.Users == null)
                    {
                        continue;
                    }

                    if (!member.is_active || member.left_at_utc.HasValue)
                    {
                        continue;
                    }

                    var avatarEntity = avatarSql.GetByUserId(member.user_id);

                    byte[] profileImageBytes = member.Users.profile_image ?? Array.Empty<byte>();
                    bool hasProfileImage = profileImageBytes.Length > 0;

                    string email = member.Users.Accounts != null
                        ? (member.Users.Accounts.email ?? string.Empty)
                        : string.Empty;

                    result.Add(
                        new AccountMini
                        {
                            AccountId = member.user_id,
                            DisplayName = member.Users.display_name ?? string.Empty,
                            Email = email,
                            HasProfileImage = hasProfileImage,
                            ProfileImageCode = string.Empty,
                            Avatar = MapAvatar(avatarEntity)
                        });
                }
            }

            return result;
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
