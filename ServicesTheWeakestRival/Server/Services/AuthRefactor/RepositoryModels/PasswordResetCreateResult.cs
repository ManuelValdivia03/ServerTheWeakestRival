namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels
{
    public sealed class PasswordResetCreateResult
    {
        public bool Success 
        { 
            get; 
        }
        public bool EmailNotFound 
        { 
            get; 
        }
        public bool TooSoon 
        { 
            get; 
        }

        private PasswordResetCreateResult(bool success, bool emailNotFound, bool tooSoon)
        {
            Success = success;
            EmailNotFound = emailNotFound;
            TooSoon = tooSoon;
        }

        public static PasswordResetCreateResult Ok() => new PasswordResetCreateResult(true, false, false);

        public static PasswordResetCreateResult FailEmailNotFound() => new PasswordResetCreateResult(false, true, false);

        public static PasswordResetCreateResult FailTooSoon() => new PasswordResetCreateResult(false, false, true);
    }
}
