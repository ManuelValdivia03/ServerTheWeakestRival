namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels
{
    public sealed class LoginLookupResult
    {
        public bool Found { get; }
        public LoginAccountRow Account { get; }

        private LoginLookupResult(bool found, LoginAccountRow account)
        {
            Found = found;
            Account = account ?? LoginAccountRow.Empty;
        }

        public static LoginLookupResult NotFound() => new LoginLookupResult(false, LoginAccountRow.Empty);

        public static LoginLookupResult FoundAccount(LoginAccountRow account) => new LoginLookupResult(true, account);
    }
}
