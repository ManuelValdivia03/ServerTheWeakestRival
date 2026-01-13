namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels
{
    public sealed class ResetLookupResult
    {
        public bool Found 
        { 
            get;
        }
        public ResetRow Reset 
        { 
            get;
        }

        private ResetLookupResult(bool found, ResetRow reset)
        {
            Found = found;
            Reset = reset ?? ResetRow.Empty;
        }

        public static ResetLookupResult NotFound() => new ResetLookupResult(false, ResetRow.Empty);

        public static ResetLookupResult FoundReset(ResetRow reset) => new ResetLookupResult(true, reset);
    }
}
