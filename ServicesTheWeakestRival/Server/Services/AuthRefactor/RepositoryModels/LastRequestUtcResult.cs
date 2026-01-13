using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels
{
    public sealed class LastRequestUtcResult
    {
        public bool HasValue 
        { 
            get; 
        }
        public DateTime Utc 
        { 
            get; 
        }

        private LastRequestUtcResult(bool hasValue, DateTime utc)
        {
            HasValue = hasValue;
            Utc = utc;
        }

        public static LastRequestUtcResult None() => new LastRequestUtcResult(false, DateTime.MinValue);

        public static LastRequestUtcResult From(DateTime utc) => new LastRequestUtcResult(true, utc);
    }
}
