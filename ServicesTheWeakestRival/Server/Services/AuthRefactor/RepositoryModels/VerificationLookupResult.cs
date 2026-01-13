namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels
{
    public sealed class VerificationLookupResult
    {
        public bool Found 
        { 
            get;
        }
        public VerificationRow Verification 
        { 
            get;
        }

        private VerificationLookupResult(bool found, VerificationRow verification)
        {
            Found = found;
            Verification = verification ?? VerificationRow.Empty;
        }

        public static VerificationLookupResult NotFound() => new VerificationLookupResult(false, VerificationRow.Empty);

        public static VerificationLookupResult FoundVerification(VerificationRow verification) =>
            new VerificationLookupResult(true, verification);
    }
}
