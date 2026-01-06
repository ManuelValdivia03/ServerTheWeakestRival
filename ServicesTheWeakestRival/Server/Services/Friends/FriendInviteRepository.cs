using System;
using System.Data;
using System.Data.SqlClient;
using ServicesTheWeakestRival.Server.Services.Logic;

namespace ServicesTheWeakestRival.Server.Services.Friends
{
    internal sealed class FriendInviteRepository : IFriendInviteRepository
    {
        public bool ExistsAcceptedFriendship(SqlConnection connection, int myAccountId, int targetAccountId)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            using (var command = new SqlCommand(FriendSql.Text.EXISTS_FRIEND, connection))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.AddWithValue(FriendServiceContext.PARAM_ME, myAccountId);
                command.Parameters.AddWithValue(FriendServiceContext.PARAM_TARGET, targetAccountId);
                command.Parameters.AddWithValue(FriendServiceContext.PARAM_ACCEPTED, (byte)FriendRequestState.Accepted);

                object scalar = command.ExecuteScalar();
                return scalar != null && scalar != DBNull.Value;
            }
        }

        public AccountContactLookup GetAccountContact(SqlConnection connection, int accountId)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            using (var command = new SqlCommand(FriendSql.Text.ACCOUNT_CONTACT, connection))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.AddWithValue(FriendServiceContext.PARAM_ID, accountId);

                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return AccountContactLookup.NotFound();
                    }

                    string email = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    string displayName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);

                    return AccountContactLookup.Found(email, displayName);
                }
            }
        }
    }

    internal sealed class AccountContactLookup
    {
        private AccountContactLookup(bool isFound, string email, string displayName)
        {
            IsFound = isFound;
            Email = email ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        public bool IsFound { get; }

        public string Email { get; }

        public string DisplayName { get; }

        public static AccountContactLookup NotFound()
        {
            return new AccountContactLookup(false, string.Empty, string.Empty);
        }

        public static AccountContactLookup Found(string email, string displayName)
        {
            return new AccountContactLookup(true, email, displayName);
        }
    }
}
