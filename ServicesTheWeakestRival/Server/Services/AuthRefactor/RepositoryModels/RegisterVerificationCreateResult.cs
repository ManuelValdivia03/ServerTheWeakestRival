namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels
{
    public sealed class RegisterVerificationCreateResult
    {
        public bool Success 
        { 
            get; 
        }
        public bool EmailTaken 
        { 
            get;
        }
        public bool TooSoon 
        { 
            get;
        }

        private RegisterVerificationCreateResult(bool success, bool emailTaken, bool tooSoon)
        {
            Success = success;
            EmailTaken = emailTaken;
            TooSoon = tooSoon;
        }

        public static RegisterVerificationCreateResult Ok() => new RegisterVerificationCreateResult(true, false, false);

        public static RegisterVerificationCreateResult FailEmailTaken() => new RegisterVerificationCreateResult(false, true, false);

        public static RegisterVerificationCreateResult FailTooSoon() => new RegisterVerificationCreateResult(false, false, true);
    }
}
