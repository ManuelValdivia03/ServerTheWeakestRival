namespace ServicesTheWeakestRival.Server.Services.Friends.Infrastructure
{
    internal readonly struct FriendRequestRow
    {
        public FriendRequestRow(int fromAccountId, int toAccountId, byte status)
        {
            FromAccountId = fromAccountId;
            ToAccountId = toAccountId;
            Status = status;
        }

        public int FromAccountId { get; }
        public int ToAccountId { get; }
        public byte Status { get; }
    }
}
