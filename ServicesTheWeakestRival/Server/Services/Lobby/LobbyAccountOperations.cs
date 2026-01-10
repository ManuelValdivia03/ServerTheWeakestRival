using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public sealed class LobbyAccountOperations
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyAccountOperations));

        private readonly LobbyRepository lobbyRepository;
        private readonly Func<UserAvatarSql> avatarSqlFactory;

        public LobbyAccountOperations(LobbyRepository lobbyRepository, Func<UserAvatarSql> avatarSqlFactory)
        {
            this.lobbyRepository = lobbyRepository ?? throw new ArgumentNullException(nameof(lobbyRepository));
            this.avatarSqlFactory = avatarSqlFactory ?? throw new ArgumentNullException(nameof(avatarSqlFactory));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            int userId = LobbyServiceContext.Authenticate(token);

            try
            {
                UpdateAccountResponse response = lobbyRepository.GetMyProfile(userId);

                UserAvatarSql avatarSql = avatarSqlFactory();
                UserAvatarEntity avatarEntity = avatarSql.GetByUserId(userId);
                response.Avatar = LobbyMappers.MapAvatar(avatarEntity);

                return response;
            }
            catch (System.ServiceModel.FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw LobbyServiceContext.ThrowTechnicalFault(
                    LobbyServiceConstants.ERROR_DB,
                    LobbyServiceConstants.MESSAGE_DB_ERROR,
                    LobbyServiceConstants.CTX_GET_MY_PROFILE,
                    ex);
            }
            catch (Exception ex)
            {
                throw LobbyServiceContext.ThrowTechnicalFault(
                    LobbyServiceConstants.ERROR_UNEXPECTED,
                    LobbyServiceConstants.MESSAGE_UNEXPECTED_ERROR,
                    LobbyServiceConstants.CTX_GET_MY_PROFILE,
                    ex);
            }
        }

        public UpdateAccountResponse UpdateAccount(UpdateAccountRequest request)
        {
            LobbyServiceContext.ValidateRequest(request);

            int userId = LobbyServiceContext.Authenticate(request.Token);

            try
            {
                bool hasDisplayNameChange = !string.IsNullOrWhiteSpace(request.DisplayName);
                bool hasEmailChange = !string.IsNullOrWhiteSpace(request.Email);

                bool removeProfileImage = request.RemoveProfileImage;
                bool hasProfileImagePayload =
                    request.ProfileImageBytes != null ||
                    !string.IsNullOrWhiteSpace(request.ProfileImageContentType);

                bool hasProfileImageChange = removeProfileImage || hasProfileImagePayload;

                if (!hasDisplayNameChange && !hasProfileImageChange && !hasEmailChange)
                {
                    Logger.DebugFormat("UpdateAccount: no changes detected. UserId={0}", userId);
                    return GetMyProfile(request.Token);
                }

                string normalizedContentType = (request.ProfileImageContentType ?? string.Empty).Trim();

                if (removeProfileImage && hasProfileImagePayload)
                {
                    throw LobbyServiceContext.ThrowFault(
                        LobbyServiceConstants.ERROR_VALIDATION_ERROR,
                        "Solicitud inválida para imagen de perfil.");
                }

                if (hasProfileImagePayload && (request.ProfileImageBytes == null || string.IsNullOrWhiteSpace(normalizedContentType)))
                {
                    throw LobbyServiceContext.ThrowFault(
                        LobbyServiceConstants.ERROR_VALIDATION_ERROR,
                        "Imagen de perfil inválida.");
                }

                AccountProfileValidator.ValidateProfileChanges(
                    new UpdateAccountRequestData
                    {
                        HasDisplayNameChange = hasDisplayNameChange,
                        HasProfileImageChange = hasProfileImageChange,
                        RemoveProfileImage = removeProfileImage,
                        DisplayNameLength = hasDisplayNameChange ? request.DisplayName.Trim().Length : 0,
                        ProfileImageBytesLength = request.ProfileImageBytes != null ? request.ProfileImageBytes.Length : 0,
                        ProfileImageContentTypeLength = normalizedContentType.Length,
                        ProfileImageContentType = normalizedContentType
                    });

                string normalizedEmail = null;
                if (hasEmailChange)
                {
                    normalizedEmail = AccountProfileValidator.ValidateAndNormalizeEmail(request.Email);

                    if (lobbyRepository.EmailExistsExceptUserId(normalizedEmail, userId))
                    {
                        throw LobbyServiceContext.ThrowFault(LobbyServiceConstants.ERROR_EMAIL_TAKEN, "Ese email ya está en uso.");
                    }
                }

                if (hasDisplayNameChange || hasProfileImageChange)
                {
                    byte[] imageBytesToSave = removeProfileImage ? null : request.ProfileImageBytes;
                    string imageContentTypeToSave = removeProfileImage ? null : normalizedContentType;

                    lobbyRepository.UpdateUserProfile(
                        userId,
                        request.DisplayName,
                        imageBytesToSave,
                        imageContentTypeToSave,
                        hasDisplayNameChange,
                        hasProfileImageChange);
                }

                if (hasEmailChange)
                {
                    lobbyRepository.UpdateUserEmail(normalizedEmail, userId);
                }

                Logger.InfoFormat(
                    "UpdateAccount: UserId={0}, DisplayNameChange={1}, ProfileImageChange={2}, RemoveProfileImage={3}, EmailChange={4}",
                    userId,
                    hasDisplayNameChange,
                    hasProfileImageChange,
                    removeProfileImage,
                    hasEmailChange);

                return GetMyProfile(request.Token);
            }
            catch (System.ServiceModel.FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw LobbyServiceContext.ThrowTechnicalFault(
                    LobbyServiceConstants.ERROR_DB,
                    LobbyServiceConstants.MESSAGE_DB_ERROR,
                    LobbyServiceConstants.CTX_UPDATE_ACCOUNT,
                    ex);
            }
            catch (Exception ex)
            {
                throw LobbyServiceContext.ThrowTechnicalFault(
                    LobbyServiceConstants.ERROR_UNEXPECTED,
                    LobbyServiceConstants.MESSAGE_UNEXPECTED_ERROR,
                    LobbyServiceConstants.CTX_UPDATE_ACCOUNT,
                    ex);
            }
        }

        public void UpdateAvatar(UpdateAvatarRequest request)
        {
            LobbyServiceContext.ValidateRequest(request);

            int userId = LobbyServiceContext.Authenticate(request.Token);

            try
            {
                var avatarEntity = new UserAvatarEntity
                {
                    UserId = userId,
                    BodyColor = request.BodyColor,
                    PantsColor = request.PantsColor,
                    HatType = request.HatType,
                    HatColor = request.HatColor,
                    FaceType = request.FaceType,
                    UseProfilePhoto = request.UseProfilePhotoAsFace
                };

                UserAvatarSql avatarSql = avatarSqlFactory();
                avatarSql.Save(avatarEntity);

                Logger.InfoFormat(
                    "UpdateAvatar: avatar updated. UserId={0}, BodyColor={1}, PantsColor={2}, HatType={3}, HatColor={4}, FaceType={5}, UsePhoto={6}",
                    userId,
                    avatarEntity.BodyColor,
                    avatarEntity.PantsColor,
                    avatarEntity.HatType,
                    avatarEntity.HatColor,
                    avatarEntity.FaceType,
                    avatarEntity.UseProfilePhoto);
            }
            catch (SqlException ex)
            {
                throw LobbyServiceContext.ThrowTechnicalFault(
                    LobbyServiceConstants.ERROR_DB,
                    LobbyServiceConstants.MESSAGE_DB_ERROR,
                    LobbyServiceConstants.CTX_UPDATE_AVATAR,
                    ex);
            }
            catch (Exception ex)
            {
                throw LobbyServiceContext.ThrowTechnicalFault(
                    LobbyServiceConstants.ERROR_UNEXPECTED,
                    LobbyServiceConstants.MESSAGE_UNEXPECTED_ERROR,
                    LobbyServiceConstants.CTX_UPDATE_AVATAR,
                    ex);
            }
        }
    }
}
