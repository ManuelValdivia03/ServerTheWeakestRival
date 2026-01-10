using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class FriendAccountLogic
    {
        private const string CONTEXT_GET_PROFILE_IMAGE = "FriendAccountLogic.GetProfileImage";

        private readonly IFriendAccountRepository accountRepository;

        public FriendAccountLogic(IFriendAccountRepository accountRepository)
        {
            this.accountRepository = accountRepository ??
                throw new ArgumentNullException(nameof(accountRepository));
        }

        public SearchAccountsResponse SearchAccounts(SearchAccountsRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);
            string query = FriendServiceContext.NormalizeQuery(request.Query);
            int maxResults = FriendServiceContext.NormalizeMaxResults(request.MaxResults);
            string likeQuery = FriendServiceContext.BuildLikeQuery(query);

            return FriendServiceContext.ExecuteDbOperation(
                FriendServiceContext.CONTEXT_SEARCH_ACCOUNTS,
                connection =>
                {
                    SearchAccountItem[] results = accountRepository.SearchAccounts(
                        connection,
                        myAccountId,
                        likeQuery,
                        maxResults) ?? Array.Empty<SearchAccountItem>();

                    FriendServiceContext.Logger.InfoFormat(
                        "SearchAccounts: Me={0}, Query='{1}', Results={2}, MaxResults={3}",
                        myAccountId,
                        query,
                        results.Length,
                        maxResults);

                    return new SearchAccountsResponse
                    {
                        Results = results
                    };
                });
        }

        public GetAccountsByIdsResponse GetAccountsByIds(GetAccountsByIdsRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);

            int[] ids = request.AccountIds ?? Array.Empty<int>();
            if (ids.Length == 0)
            {
                return new GetAccountsByIdsResponse
                {
                    Accounts = Array.Empty<AccountMini>()
                };
            }

            var avatarSql = new UserAvatarSql(FriendServiceContext.GetConnectionString());

            return FriendServiceContext.ExecuteDbOperation(
                FriendServiceContext.CONTEXT_GET_ACCOUNTS_BY_IDS,
                connection =>
                {
                    var avatarsByUserId = avatarSql.GetByUserIds(ids);

                    var accounts = accountRepository.GetAccountsByIds(
                        connection,
                        myAccountId,
                        ids,
                        avatarsByUserId);

                    FriendServiceContext.Logger.DebugFormat(
                        "GetAccountsByIds: Me={0}, Requested={1}, Found={2}",
                        myAccountId,
                        ids.Length,
                        accounts == null ? 0 : accounts.Count);

                    return new GetAccountsByIdsResponse
                    {
                        Accounts = (accounts ?? new System.Collections.Generic.List<AccountMini>()).ToArray()
                    };
                });
        }

        public GetProfileImageResponse GetProfileImage(GetProfileImageRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);

            int targetAccountId = request.AccountId;
            if (targetAccountId <= 0)
            {
                return new GetProfileImageResponse
                {
                    ImageBytes = Array.Empty<byte>(),
                    ContentType = string.Empty,
                    UpdatedAtUtc = null,
                    ProfileImageCode = string.Empty
                };
            }

            return FriendServiceContext.ExecuteDbOperation(
                CONTEXT_GET_PROFILE_IMAGE,
                connection =>
                {
                    UserProfileImageEntity entity = accountRepository.GetProfileImage(connection, targetAccountId);

                    FriendServiceContext.Logger.DebugFormat(
                        "GetProfileImage: Me={0}, Target={1}, Bytes={2}",
                        myAccountId,
                        targetAccountId,
                        entity == null ? 0 : entity.Bytes.Length);

                    return new GetProfileImageResponse
                    {
                        ImageBytes = entity == null ? Array.Empty<byte>() : entity.Bytes,
                        ContentType = entity == null ? string.Empty : entity.ContentType,
                        UpdatedAtUtc = entity == null ? null : entity.UpdatedAtUtc,
                        ProfileImageCode = entity == null ? string.Empty : entity.ProfileImageCode
                    };
                });
        }
    }
}
