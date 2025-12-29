using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Friends.Infrastructure
{
    internal sealed class FriendRequestRepository : Friends.IFriendRequestRepository
    {
        public bool FriendshipExists(FriendDbContext db, int targetAccountId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.EXISTS_FRIEND, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_TARGET, SqlDbType.Int).Value = targetAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_ACCEPTED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Accepted;

                object scalarValue = command.ExecuteScalar();
                return scalarValue != null && scalarValue != DBNull.Value;
            }
        }

        public int? GetPendingOutgoingId(FriendDbContext db, int targetAccountId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.PENDING_OUT, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_TARGET, SqlDbType.Int).Value = targetAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_PENDING, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Pending;

                return FriendServiceContext.ExecuteScalarInt(command);
            }
        }

        public int? GetPendingIncomingId(FriendDbContext db, int targetAccountId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.PENDING_IN, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_TARGET, SqlDbType.Int).Value = targetAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_PENDING, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Pending;

                return FriendServiceContext.ExecuteScalarInt(command);
            }
        }

        public int AcceptIncomingRequest(FriendDbContext db, int requestId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.ACCEPT_INCOMING, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_REQUEST_ID, SqlDbType.Int).Value = requestId;
                command.Parameters.Add(FriendServiceContext.PARAM_ACCEPTED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Accepted;

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public int InsertNewRequest(FriendDbContext db, int targetAccountId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.INSERT_REQUEST, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_TARGET, SqlDbType.Int).Value = targetAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_PENDING, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Pending;

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public int ReopenRequest(FriendDbContext db, int targetAccountId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.REOPEN_REQUEST, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_TARGET, SqlDbType.Int).Value = targetAccountId;

                command.Parameters.Add(FriendServiceContext.PARAM_PENDING, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Pending;
                command.Parameters.Add(FriendServiceContext.PARAM_DECLINED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Declined;
                command.Parameters.Add(FriendServiceContext.PARAM_CANCELLED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Cancelled;

                int? reopenedId = FriendServiceContext.ExecuteScalarInt(command);
                if (!reopenedId.HasValue)
                {
                    throw FriendServiceContext.ThrowFault(
                        FriendServiceContext.ERROR_RACE,
                        FriendServiceContext.MESSAGE_FR_RACE_REOPEN_FAILED);
                }

                return reopenedId.Value;
            }
        }

        public FriendRequestRow ReadRequestRow(FriendDbContext db, string sqlText, int requestId)
        {
            using (SqlCommand command = new SqlCommand(sqlText, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ID, SqlDbType.Int).Value = requestId;

                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        throw FriendServiceContext.ThrowFault(
                            FriendServiceContext.ERROR_FR_NOT_FOUND,
                            FriendServiceContext.MESSAGE_FR_NOT_FOUND);
                    }

                    int fromAccountId = reader.GetInt32(1);
                    int toAccountId = reader.GetInt32(2);
                    byte currentStatus = reader.GetByte(3);

                    return new FriendRequestRow(fromAccountId, toAccountId, currentStatus);
                }
            }
        }

        public int AcceptRequest(FriendDbContext db, int requestId, int myAccountId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.ACCEPT_REQUEST, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ACCEPTED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Accepted;
                command.Parameters.Add(FriendServiceContext.PARAM_PENDING, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Pending;
                command.Parameters.Add(FriendServiceContext.PARAM_ID, SqlDbType.Int).Value = requestId;
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = myAccountId;

                return command.ExecuteNonQuery();
            }
        }

        public int RejectAsReceiver(FriendDbContext db, int requestId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.REJECT_REQUEST, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ID, SqlDbType.Int).Value = requestId;
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_REJECTED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Declined;
                command.Parameters.Add(FriendServiceContext.PARAM_PENDING, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Pending;

                return command.ExecuteNonQuery();
            }
        }

        public int CancelAsSender(FriendDbContext db, int requestId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.CANCEL_REQUEST, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ID, SqlDbType.Int).Value = requestId;
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_CANCELLED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Cancelled;
                command.Parameters.Add(FriendServiceContext.PARAM_PENDING, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Pending;

                return command.ExecuteNonQuery();
            }
        }

        public int? GetLatestAcceptedFriendRequestId(FriendDbContext db, int otherAccountId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.LATEST_ACCEPTED, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ME, SqlDbType.Int).Value = db.MyAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_OTHER, SqlDbType.Int).Value = otherAccountId;
                command.Parameters.Add(FriendServiceContext.PARAM_ACCEPTED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Accepted;

                return FriendServiceContext.ExecuteScalarInt(command);
            }
        }

        public int MarkFriendRequestCancelled(FriendDbContext db, int requestId)
        {
            using (SqlCommand command = new SqlCommand(FriendSql.Text.MARK_CANCELLED, db.Connection, db.Transaction))
            {
                command.Parameters.Add(FriendServiceContext.PARAM_ID, SqlDbType.Int).Value = requestId;
                command.Parameters.Add(FriendServiceContext.PARAM_CANCELLED, SqlDbType.TinyInt).Value =
                    (byte)FriendRequestState.Cancelled;

                return command.ExecuteNonQuery();
            }
        }
    }
}
