using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Friends.Infrastructure
{
    internal readonly struct FriendDbContext
    {
        public FriendDbContext(SqlConnection connection, SqlTransaction transaction, int myAccountId)
        {
            Connection = connection;
            Transaction = transaction;
            MyAccountId = myAccountId;
        }

        public SqlConnection Connection { get; }
        public SqlTransaction Transaction { get; }
        public int MyAccountId { get; }
    }
}
