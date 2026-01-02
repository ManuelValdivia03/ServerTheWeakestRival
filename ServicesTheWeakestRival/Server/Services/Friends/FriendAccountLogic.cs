using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Friends;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class FriendAccountLogic
    {
        private readonly IFriendAccountRepository _accountRepository;

        public FriendAccountLogic(IFriendAccountRepository accountRepository)
        {
            _accountRepository = accountRepository ??
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
                    SearchAccountItem[] results = _accountRepository.SearchAccounts(
                        connection,
                        myAccountId,
                        likeQuery,
                        maxResults);

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
                    Dictionary<int, UserAvatarEntity> avatarsByUserId = avatarSql.GetByUserIds(ids);

                    List<AccountMini> accounts = _accountRepository.GetAccountsByIds(
                        connection,
                        myAccountId,
                        ids,
                        avatarsByUserId);

                    FriendServiceContext.Logger.DebugFormat(
                        "GetAccountsByIds: Me={0}, Requested={1}, Found={2}",
                        myAccountId,
                        ids.Length,
                        accounts.Count);

                    return new GetAccountsByIdsResponse
                    {
                        Accounts = accounts.ToArray()
                    };
                });
        }
    }
}
