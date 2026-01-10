namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels
{
    public sealed class LoginAccountRow
    {
        private const int USER_ID_EMPTY = 0;
        private const byte STATUS_EMPTY = 0;

        public static LoginAccountRow Empty => new LoginAccountRow(USER_ID_EMPTY, string.Empty, STATUS_EMPTY);

        public int UserId { get; }
        public string PasswordHash { get; }
        public byte Status { get; }

        public LoginAccountRow(int userId, string passwordHash, byte status)
        {
            UserId = userId;
            PasswordHash = passwordHash ?? string.Empty;
            Status = status;
        }
    }
}
