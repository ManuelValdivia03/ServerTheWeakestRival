using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Validation;
using System;
using System.Globalization;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows
{
    public sealed class GetProfileImageWorkflow
    {
        private const string OPERATION_KEY_PREFIX = AuthServiceConstants.KEY_PREFIX_GET_PROFILE_IMAGE;
        private const string DB_CONTEXT = AuthServiceConstants.CTX_GET_PROFILE_IMAGE;

        private const string MESSAGE_ACCOUNT_ID_REQUIRED = "AccountId is required.";

        private const char PROFILE_IMAGE_CODE_SEPARATOR = '|';

        private readonly AuthRepository authRepository;

        public GetProfileImageWorkflow(AuthRepository authRepository)
        {
            this.authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
        }

        public GetProfileImageResponse Execute(GetProfileImageRequest request)
        {
            EnsureRequestNotNullOrThrow(request);

            AuthRequestValidator.EnsureValidSessionOrThrow(request.Token);
            EnsureValidAccountIdOrThrow(request.AccountId);

            ProfileImageRecord record = ReadProfileImageOrThrow(request.AccountId);

            return BuildResponse(request.ProfileImageCode, record);
        }

        private ProfileImageRecord ReadProfileImageOrThrow(int accountId)
        {
            ProfileImageRecord record = SqlExceptionFaultGuard.Execute(
                () => authRepository.ReadUserProfileImage(accountId),
                OPERATION_KEY_PREFIX,
                AuthServiceConstants.ERROR_DB_ERROR,
                DB_CONTEXT,
                AuthServiceContext.CreateSqlTechnicalFault);

            if (record != null)
            {
                return record;
            }

            return new ProfileImageRecord(
                accountId,
                Array.Empty<byte>(),
                string.Empty,
                null);
        }

        private static GetProfileImageResponse BuildResponse(string clientProfileImageCode, ProfileImageRecord record)
        {
            byte[] imageBytes = record.ImageBytes ?? Array.Empty<byte>();
            string contentType = record.ContentType ?? string.Empty;
            DateTime? updatedAtUtc = record.UpdatedAtUtc;

            string profileImageCode = BuildProfileImageCode(updatedAtUtc, imageBytes, contentType);

            bool clientHasSameImage =
                !string.IsNullOrWhiteSpace(clientProfileImageCode)
                && string.Equals(clientProfileImageCode, profileImageCode, StringComparison.Ordinal);

            bool hasImage = imageBytes.Length > 0;

            return new GetProfileImageResponse
            {
                ImageBytes = clientHasSameImage ? Array.Empty<byte>() : imageBytes,
                ContentType = !hasImage || clientHasSameImage ? string.Empty : contentType,
                UpdatedAtUtc = updatedAtUtc,
                ProfileImageCode = profileImageCode
            };
        }

        private static string BuildProfileImageCode(DateTime? updatedAtUtc, byte[] imageBytes, string contentType)
        {
            if (!updatedAtUtc.HasValue)
            {
                return string.Empty;
            }

            int byteCount = imageBytes?.Length ?? 0;
            if (byteCount <= 0)
            {
                return string.Empty;
            }

            string safeContentType = contentType ?? string.Empty;

            return string.Concat(
                updatedAtUtc.Value.Ticks.ToString(CultureInfo.InvariantCulture),
                PROFILE_IMAGE_CODE_SEPARATOR,
                byteCount.ToString(CultureInfo.InvariantCulture),
                PROFILE_IMAGE_CODE_SEPARATOR,
                safeContentType);
        }

        private static void EnsureValidAccountIdOrThrow(int accountId)
        {
            if (accountId <= 0)
            {
                throw AuthServiceContext.ThrowFault(
                    AuthServiceConstants.ERROR_INVALID_REQUEST,
                    MESSAGE_ACCOUNT_ID_REQUIRED);
            }
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
