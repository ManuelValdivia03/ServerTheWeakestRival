// ReportRequestLogic.cs (solo cambios para AccountMini: HasProfileImage/ProfileImageCode/Email)
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;
using TheWeakestRival.Data;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportRequestLogic
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ReportRequestLogic));

        private const string ContextSubmit = "ReportRequestLogic.SubmitPlayerReport";

        private const string FaultInvalidTarget = "REPORT_INVALID_TARGET";
        private const string FaultSelfReport = "REPORT_SELF";
        private const string FaultCooldown = "REPORT_COOLDOWN";
        private const string FaultInvalidReason = "REPORT_INVALID_REASON";
        private const string FaultReporterNotActive = "REPORT_REPORTER_NOT_ACTIVE";
        private const string FaultReportedNotActive = "REPORT_REPORTED_NOT_ACTIVE";
        private const string FaultCommentTooLong = "REPORT_COMMENT_TOO_LONG";
        private const string FaultSanctionPolicyMissing = "REPORT_SANCTION_POLICY_MISSING";
        private const string FaultDb = "REPORT_DB_ERROR";
        private const string FaultUnexpected = "REPORT_UNEXPECTED";
        private const string FORCED_LOGOUT_CODE_SANCTION_APPLIED = "SANCTION_APPLIED";
        private const string SP_LOBBY_LEAVE_ALL_BY_USER = "dbo.usp_Lobby_LeaveAllByUser";
        private const string ParamUserId = "@UserId";
        private const string ContextKickFromLobby = "ReportRequestLogic.KickFromLobby";
        private const string ContextBroadcastLobbyUpdated = "ReportRequestLogic.BroadcastLobbyUpdated";
        private const string ParamLobbyUid = "@u";
        private const string ParamLobbyId = "@id";
        private const string ContextResolveUserId = "ReportRequestLogic.ResolveUserIdFromAccountId";
        private const string SqlResolveUserIdFromAccountId =
            "SELECT TOP (1) u.user_id FROM dbo.Users u WHERE u.account_id = @AccountId;";
        private const string ParamAccountId = "@AccountId";

        internal SubmitPlayerReportResponse SubmitPlayerReport(SubmitPlayerReportRequest request)
        {
            ReportServiceContext.ValidateRequest(request);

            int reporterAccountId = ReportServiceContext.Authenticate(request.Token);

            if (request.ReportedAccountId <= 0)
            {
                throw ReportServiceContext.ThrowFault(FaultInvalidTarget, "Jugador inválido.");
            }

            if (reporterAccountId == request.ReportedAccountId)
            {
                throw ReportServiceContext.ThrowFault(FaultSelfReport, "No puedes reportarte a ti mismo.");
            }

            string connectionString =
                ConfigurationManager.ConnectionStrings[ReportSql.MainConnectionStringName].ConnectionString;

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand(ReportSql.SpSubmitPlayerReport, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.Add(ReportSql.ParamReporterAccountId, SqlDbType.Int).Value = reporterAccountId;
                        command.Parameters.Add(ReportSql.ParamReportedAccountId, SqlDbType.Int).Value = request.ReportedAccountId;

                        var lobbyIdParam = command.Parameters.Add(ReportSql.ParamLobbyId, SqlDbType.UniqueIdentifier);
                        lobbyIdParam.Value = request.LobbyId.HasValue
                            ? (object)request.LobbyId.Value
                            : DBNull.Value;

                        command.Parameters.Add(ReportSql.ParamReasonCode, SqlDbType.TinyInt).Value = (byte)request.ReasonCode;

                        var commentParam = command.Parameters.Add(
                            ReportSql.ParamComment,
                            SqlDbType.NVarChar,
                            ReportSql.CommentMaxLength);

                        commentParam.Value = string.IsNullOrWhiteSpace(request.Comment)
                            ? (object)DBNull.Value
                            : request.Comment;

                        var outReportId = command.Parameters.Add(ReportSql.OutReportId, SqlDbType.BigInt);
                        outReportId.Direction = ParameterDirection.Output;

                        var outSanctionApplied = command.Parameters.Add(ReportSql.OutSanctionApplied, SqlDbType.Bit);
                        outSanctionApplied.Direction = ParameterDirection.Output;

                        var outSanctionType = command.Parameters.Add(ReportSql.OutSanctionType, SqlDbType.TinyInt);
                        outSanctionType.Direction = ParameterDirection.Output;

                        var outSanctionEndAtUtc = command.Parameters.Add(ReportSql.OutSanctionEndAtUtc, SqlDbType.DateTime2);
                        outSanctionEndAtUtc.Direction = ParameterDirection.Output;

                        command.ExecuteNonQuery();

                        var response = new SubmitPlayerReportResponse
                        {
                            ReportId = Convert.ToInt64(outReportId.Value),
                            SanctionApplied = Convert.ToBoolean(outSanctionApplied.Value),
                            SanctionType = outSanctionType.Value == DBNull.Value ? (byte)0 : Convert.ToByte(outSanctionType.Value),
                            SanctionEndAtUtc = outSanctionEndAtUtc.Value == DBNull.Value ? (DateTime?)null : (DateTime)outSanctionEndAtUtc.Value
                        };

                        if (response.SanctionApplied)
                        {
                            int targetUserId = ResolveUserIdFromAccountId(connection, request.ReportedAccountId);
                            int effectiveUserId = targetUserId > 0 ? targetUserId : request.ReportedAccountId;

                            TokenStore.RevokeAllForUser(effectiveUserId);

                            LobbyService.ForceLogoutAndKickFromLobby(
                               effectiveUserId,
                               response.SanctionType,
                               response.SanctionEndAtUtc);

                            if (request.LobbyId.HasValue && request.LobbyId.Value != Guid.Empty)
                            {
                                TryBroadcastLobbyUpdatedFromDb(connection, request.LobbyId.Value);
                            }

                            TokenStore.RevokeAllForUser(effectiveUserId);

                            var notification = new ForcedLogoutNotification
                            {
                                SanctionType = response.SanctionType,
                                SanctionEndAtUtc = response.SanctionEndAtUtc,
                                Code = FORCED_LOGOUT_CODE_SANCTION_APPLIED
                            };

                            TrySendForcedLogoutToAccount(effectiveUserId, notification);
                        }

                        return response;
                    }
                }
            }
            catch (SqlException ex)
            {
                Logger.Warn(ContextSubmit, ex);

                string message = ex.Message ?? string.Empty;

                if (ContainsToken(message, ReportSql.DbTokenDuplicateCooldown))
                {
                    throw ReportServiceContext.ThrowFault(FaultCooldown, "Debes esperar antes de reportar al mismo jugador otra vez.");
                }

                if (ContainsToken(message, ReportSql.DbTokenInvalidReason))
                {
                    throw ReportServiceContext.ThrowFault(FaultInvalidReason, "Motivo inválido.");
                }

                if (ContainsToken(message, ReportSql.DbTokenReporterNotActive))
                {
                    throw ReportServiceContext.ThrowFault(FaultReporterNotActive, "Tu cuenta no está activa.");
                }

                if (ContainsToken(message, ReportSql.DbTokenReportedNotActive))
                {
                    throw ReportServiceContext.ThrowFault(FaultReportedNotActive, "La cuenta del jugador no está activa.");
                }

                if (ContainsToken(message, ReportSql.DbTokenCommentTooLong))
                {
                    throw ReportServiceContext.ThrowFault(FaultCommentTooLong, "El comentario es demasiado largo.");
                }

                if (ContainsToken(message, ReportSql.DbTokenSanctionPolicyMissing))
                {
                    throw ReportServiceContext.ThrowFault(FaultSanctionPolicyMissing, "Configuración de sanciones incompleta.");
                }

                if (ContainsToken(message, ReportSql.DbTokenSelfReport))
                {
                    throw ReportServiceContext.ThrowFault(FaultSelfReport, "No puedes reportarte a ti mismo.");
                }

                throw ReportServiceContext.ThrowFault(FaultDb, "Ocurrió un error al enviar el reporte.");
            }
            catch (Exception ex)
            {
                Logger.Error(ContextSubmit, ex);
                throw ReportServiceContext.ThrowFault(FaultUnexpected, "Ocurrió un error inesperado al enviar el reporte.");
            }
        }

        private static bool ContainsToken(string text, string token)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void TrySendForcedLogoutToAccount(int accountId, ForcedLogoutNotification notification)
        {
            if (accountId <= 0 || notification == null)
            {
                return;
            }

            if (!LobbyCallbackRegistry.TryGet(accountId, out var callback) || callback == null)
            {
                return;
            }

            ICommunicationObject channelObject = callback as ICommunicationObject;

            try
            {
                callback.ForcedLogout(notification);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to send ForcedLogout callback.", ex);
            }
            finally
            {
                LobbyCallbackRegistry.Remove(accountId);

                try
                {
                    if (channelObject != null)
                    {
                        channelObject.Abort();
                    }
                }
                catch
                {
                    // no-op
                }
            }
        }

        private static void TryKickUserFromLobbiesInDb(SqlConnection connection, int userId)
        {
            if (connection == null || userId <= 0)
            {
                return;
            }

            try
            {
                using (var cmd = new SqlCommand(LobbySql.Text.SP_LOBBY_LEAVE_ALL_BY_USER, connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(ParamUserId, SqlDbType.Int).Value = userId;
                    cmd.ExecuteNonQuery();
                }

                Logger.InfoFormat("KickFromLobby: user removed from lobbies. UserId={0}", userId);
            }
            catch (Exception ex)
            {
                Logger.Warn(ContextKickFromLobby, ex);
            }
        }

        private static void TryBroadcastLobbyUpdatedFromDb(SqlConnection connection, Guid lobbyUid)
        {
            if (connection == null || lobbyUid == Guid.Empty)
            {
                return;
            }

            try
            {
                if (!TryBuildLobbyInfo(connection, lobbyUid, out var lobbyInfo))
                {
                    return;
                }

                if (lobbyInfo.Players == null)
                {
                    return;
                }

                foreach (var player in lobbyInfo.Players)
                {
                    if (player == null || player.AccountId <= 0)
                    {
                        continue;
                    }

                    if (!LobbyCallbackRegistry.TryGet(player.AccountId, out var callback) || callback == null)
                    {
                        continue;
                    }

                    try
                    {
                        callback.OnLobbyUpdated(lobbyInfo);
                    }
                    catch (Exception ex)
                    {
                        Logger.WarnFormat(
                            "BroadcastLobbyUpdated: failed callback. LobbyUid={0}, AccountId={1}",
                            lobbyUid,
                            player.AccountId);

                        Logger.Warn(ContextBroadcastLobbyUpdated, ex);
                        LobbyCallbackRegistry.Remove(player.AccountId);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ContextBroadcastLobbyUpdated, ex);
            }
        }

        private static bool TryBuildLobbyInfo(SqlConnection connection, Guid lobbyUid, out LobbyInfo lobbyInfo)
        {
            lobbyInfo = null;

            int lobbyId = GetLobbyIdFromUid(connection, lobbyUid);
            if (lobbyId <= 0)
            {
                return false;
            }

            var baseInfo = LoadLobbyInfoByIntId(connection, lobbyId);
            if (baseInfo == null)
            {
                return false;
            }

            var members = GetLobbyMembers(connection, lobbyId);
            var avatarSql = new UserAvatarSql(connection.ConnectionString);
            baseInfo.Players = MapToAccountMini(members, avatarSql);

            lobbyInfo = baseInfo;
            return true;
        }

        private static int GetLobbyIdFromUid(SqlConnection connection, Guid lobbyUid)
        {
            using (var cmd = new SqlCommand(LobbySql.Text.GET_LOBBY_ID_FROM_UID, connection))
            {
                cmd.Parameters.Add(ParamLobbyUid, SqlDbType.UniqueIdentifier).Value = lobbyUid;

                var obj = cmd.ExecuteScalar();
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
                cmd.Parameters.Add("@LobbyId", SqlDbType.Int).Value = lobbyId;

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
            if (members == null)
            {
                return result;
            }

            foreach (var member in members)
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

                result.Add(
                    new AccountMini
                    {
                        AccountId = member.user_id,
                        DisplayName = member.Users.display_name ?? string.Empty,
                        Email = member.Users.Accounts.email ?? string.Empty,
                        HasProfileImage = hasProfileImage,
                        ProfileImageCode = string.Empty,
                        Avatar = MapAvatar(avatarEntity)
                    });
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

        private static int ResolveUserIdFromAccountId(SqlConnection connection, int accountId)
        {
            if (connection == null || accountId <= 0)
            {
                return 0;
            }

            try
            {
                using (var cmd = new SqlCommand(SqlResolveUserIdFromAccountId, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(ParamAccountId, SqlDbType.Int).Value = accountId;

                    var obj = cmd.ExecuteScalar();
                    return obj == null || obj == DBNull.Value ? 0 : Convert.ToInt32(obj);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ContextResolveUserId, ex);
                return 0;
            }
        }
    }
}
