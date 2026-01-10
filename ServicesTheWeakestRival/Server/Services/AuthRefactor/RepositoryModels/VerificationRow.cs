using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels
{
    public sealed class VerificationRow
    {
        private const int ID_EMPTY = 0;

        public static VerificationRow Empty => new VerificationRow(ID_EMPTY, DateTime.MinValue, false);

        public int Id { get; }
        public DateTime ExpiresAtUtc { get; }
        public bool Used { get; }

        public VerificationRow(int id, DateTime expiresAtUtc, bool used)
        {
            Id = id;
            ExpiresAtUtc = expiresAtUtc;
            Used = used;
        }
    }
}
