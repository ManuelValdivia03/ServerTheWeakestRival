using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Validation;
using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows
{
    public sealed class GetProfileImageWorkflow
    {
        private const string OPERATION_KEY_PREFIX = AuthServiceConstants.KEY_PREFIX_GET_PROFILE_IMAGE;
        private const string DB_CONTEXT = AuthServiceConstants.CTX_GET_PROFILE_IMAGE;

        private readonly AuthRepository authRepository;

        public GetProfileImageWorkflow(AuthRepository authRepository)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
        }

        public GetProfileImageResponse Execute(GetProfileImageRequest request)
        {
            EnsureRequestNotNullOrThrow(request);

            AuthRequestValidator.EnsureValidSessionOrThrow(request.Token);
            AuthRequestValidator.EnsureValidUserIdOrThrow(request.UserId);

            ProfileImageRecord record = ReadProfileImageOrThrow(request.UserId);

            return BuildResponse(request.UserId, record);
        }

        private ProfileImageRecord ReadProfileImageOrThrow(int userId)
        {
            return SqlExceptionFaultGuard.Execute(
                () => authRepository.ReadUserProfileImage(userId),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);
        }

        private static GetProfileImageResponse BuildResponse(int userId, ProfileImageRecord record)
        {
            bool hasImage = record.ImageBytes.Length > 0;

            return new GetProfileImageResponse
            {
                UserId = userId,
                HasImage = hasImage,
                ImageBytes = record.ImageBytes,
                ContentType = record.ContentType,
                UpdatedAtUtc = record.UpdatedAtUtc
            };
        }

        private static void EnsureRequestNotNullOrThrow(GetProfileImageRequest request)
        {
            if (request == null)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    AuthServiceConstants.MESSAGE_PAYLOAD_NULL);
            }
        }
    }
}
