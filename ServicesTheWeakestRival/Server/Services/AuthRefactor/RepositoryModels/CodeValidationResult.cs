namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels
{
    public sealed class CodeValidationResult
    {
        public bool IsValid { get; }

        private CodeValidationResult(bool isValid)
        {
            IsValid = isValid;
        }

        public static CodeValidationResult Valid() => new CodeValidationResult(true);

        public static CodeValidationResult Invalid() => new CodeValidationResult(false);
    }
}
