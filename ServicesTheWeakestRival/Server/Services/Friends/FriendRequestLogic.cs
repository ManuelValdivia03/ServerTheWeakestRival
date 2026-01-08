using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using ServicesTheWeakestRival.Server.Services.Friends;
using ServicesTheWeakestRival.Server.Services.Friends.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class FriendRequestLogic
    {
        private readonly IFriendRequestRepository _friendRequestRepository;

        public FriendRequestLogic(IFriendRequestRepository friendRequestRepository)
        {
            _friendRequestRepository = friendRequestRepository ??
                throw new ArgumentNullException(nameof(friendRequestRepository));
        }

        public SendFriendRequestResponse SendFriendRequest(SendFriendRequestRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);
            int targetAccountId = request.TargetAccountId;

            if (targetAccountId <= 0)
            {
                throw FriendServiceContext.ThrowFault(
                    FriendServiceContext.ERROR_INVALID_REQUEST,
                    FriendServiceContext.ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (myAccountId == targetAccountId)
            {
                throw FriendServiceContext.ThrowFault(
                    FriendServiceContext.ERROR_FR_SELF,
                    FriendServiceContext.MESSAGE_FR_SELF);
            }

            return FriendServiceContext.ExecuteDbOperation(
                FriendServiceContext.CONTEXT_SEND_FRIEND_REQUEST,
                connection =>
                {
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        try
                        {
                            var db = new FriendDbContext(connection, transaction, myAccountId);

                            EnsureFriendshipDoesNotExist(_friendRequestRepository, db, targetAccountId);

                            int? existingOutgoingId = _friendRequestRepository.GetPendingOutgoingId(db, targetAccountId);
                            if (existingOutgoingId.HasValue)
                            {
                                transaction.Commit();

                                FriendServiceContext.Logger.InfoFormat(
                                    "SendFriendRequest: existing outgoing request reused. Me={0}, Target={1}, RequestId={2}",
                                    myAccountId,
                                    targetAccountId,
                                    existingOutgoingId.Value);

                                return CreateSendFriendRequestResponse(existingOutgoingId.Value, FriendRequestStatus.Pending);
                            }

                            int? incomingId = _friendRequestRepository.GetPendingIncomingId(db, targetAccountId);
                            if (incomingId.HasValue)
                            {
                                int acceptedId = _friendRequestRepository.AcceptIncomingRequest(db, incomingId.Value);

                                transaction.Commit();

                                FriendServiceContext.Logger.InfoFormat(
                                    "SendFriendRequest: converted incoming request to accepted. Me={0}, Target={1}, RequestId={2}",
                                    myAccountId,
                                    targetAccountId,
                                    acceptedId);

                                return CreateSendFriendRequestResponse(acceptedId, FriendRequestStatus.Accepted);
                            }

                            try
                            {
                                int newId = _friendRequestRepository.InsertNewRequest(db, targetAccountId);

                                transaction.Commit();

                                FriendServiceContext.Logger.InfoFormat(
                                    "SendFriendRequest: new request created. Me={0}, Target={1}, RequestId={2}",
                                    myAccountId,
                                    targetAccountId,
                                    newId);

                                return CreateSendFriendRequestResponse(newId, FriendRequestStatus.Pending);
                            }
                            catch (SqlException ex) when (FriendServiceContext.IsUniqueViolation(ex))
                            {
                                int reopenedId = _friendRequestRepository.ReopenRequest(db, targetAccountId);

                                transaction.Commit();

                                FriendServiceContext.Logger.InfoFormat(
                                    "SendFriendRequest: duplicate handled by reopening request. Me={0}, Target={1}, RequestId={2}",
                                    myAccountId,
                                    targetAccountId,
                                    reopenedId);

                                return CreateSendFriendRequestResponse(reopenedId, FriendRequestStatus.Pending);
                            }
                        }
                        catch (SqlException ex)
                        {
                            SqlFaultMapping mapped = SqlExceptionFaultMapper.Map(
                                ex,
                                FriendServiceContext.OP_KEY_SEND_FRIEND_REQUEST);

                            FriendServiceContext.Logger.Error("SendFriendRequest: SQL exception.", ex);
                            FriendServiceContext.Logger.ErrorFormat(
                                "SendFriendRequest: mapped SQL fault. Key={0}",
                                mapped.MessageKey);

                            throw FriendServiceContext.ThrowFault(
                                FriendServiceContext.ERROR_DB,
                                mapped.MessageKey);
                        }
                    }
                });
        }

        public AcceptFriendRequestResponse AcceptFriendRequest(AcceptFriendRequestRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);

                return FriendServiceContext.ExecuteDbOperation(
                FriendServiceContext.CONTEXT_ACCEPT_FRIEND_REQUEST,
                connection =>
                {
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        try
                        {
                            var db = new FriendDbContext(connection, transaction, myAccountId);

                            FriendRequestRow row = _friendRequestRepository.ReadRequestRow(
                                db,
                                FriendSql.Text.CHECK_REQUEST,
                                request.FriendRequestId);

                            EnsureRequestReceiver(row.ToAccountId, myAccountId);

                            EnsureRequestPending(
                                row.Status,
                                FriendServiceContext.ERROR_FR_NOT_PENDING,
                                FriendServiceContext.KEY_FR_REQUEST_ALREADY_PROCESSED);


                            int affectedRows = _friendRequestRepository.AcceptRequest(db, request.FriendRequestId, myAccountId);
                            EnsureAffectedRowsOrRace(affectedRows);

                            transaction.Commit();

                            FriendServiceContext.Logger.InfoFormat(
                                "AcceptFriendRequest: request accepted. RequestId={0}, Me={1}, From={2}",
                                request.FriendRequestId,
                                myAccountId,
                                row.FromAccountId);

                            DateTime sinceUtc = DateTime.UtcNow;
                            FriendSummary newFriend = CreateNewFriendSummary(row.FromAccountId, sinceUtc);

                            return CreateAcceptFriendRequestResponse(newFriend);
                        }
                        catch (SqlException ex)
                        {
                            SqlFaultMapping mapped = SqlExceptionFaultMapper.Map(
                                ex,
                                FriendServiceContext.OP_KEY_ACCEPT_FRIEND_REQUEST);

                            FriendServiceContext.Logger.Error("AcceptFriendRequest: SQL exception.", ex);

                            throw FriendServiceContext.ThrowFault(
                                FriendServiceContext.ERROR_DB,
                                mapped.MessageKey);
                        }
                    }
                });

        }

        public RejectFriendRequestResponse RejectFriendRequest(RejectFriendRequestRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);

            return FriendServiceContext.ExecuteDbOperation(
                FriendServiceContext.CONTEXT_REJECT_FRIEND_REQUEST,
                connection =>
                {
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        try
                        {
                            var db = new FriendDbContext(connection, transaction, myAccountId);

                            FriendRequestRow row = _friendRequestRepository.ReadRequestRow(
                                db,
                                FriendSql.Text.GET_REQUEST,
                                request.FriendRequestId);

                            EnsureRequestPending(
                                row.Status,
                                FriendServiceContext.ERROR_FR_NOT_PENDING,
                                FriendServiceContext.KEY_FR_REQUEST_ALREADY_PROCESSED);


                            if (row.ToAccountId == myAccountId)
                            {
                                int affectedRows = _friendRequestRepository.RejectAsReceiver(db, request.FriendRequestId);
                                EnsureAffectedRowsOrRace(affectedRows);

                                transaction.Commit();

                                FriendServiceContext.Logger.InfoFormat(
                                    "RejectFriendRequest: request rejected. RequestId={0}, Me={1}, From={2}",
                                    request.FriendRequestId,
                                    myAccountId,
                                    row.FromAccountId);

                                return CreateRejectFriendRequestResponse(FriendRequestStatus.Rejected);
                            }

                            if (row.FromAccountId == myAccountId)
                            {
                                int affectedRows = _friendRequestRepository.CancelAsSender(db, request.FriendRequestId);
                                EnsureAffectedRowsOrRace(affectedRows);

                                transaction.Commit();

                                FriendServiceContext.Logger.InfoFormat(
                                    "RejectFriendRequest: request cancelled. RequestId={0}, Me={1}, To={2}",
                                    request.FriendRequestId,
                                    myAccountId,
                                    row.ToAccountId);

                                return CreateRejectFriendRequestResponse(FriendRequestStatus.Cancelled);
                            }

                            throw FriendServiceContext.ThrowFault(
                                FriendServiceContext.ERROR_FR_FORBIDDEN,
                                FriendServiceContext.MESSAGE_FR_FORBIDDEN_NOT_INVOLVED);
                        }
                        catch (SqlException ex)
                        {
                            SqlFaultMapping mapped = SqlExceptionFaultMapper.Map(
                                ex,
                                FriendServiceContext.OP_KEY_REJECT_FRIEND_REQUEST);

                            FriendServiceContext.Logger.Error("RejectFriendRequest: SQL exception.", ex);

                            throw FriendServiceContext.ThrowFault(
                                FriendServiceContext.ERROR_DB,
                                mapped.MessageKey);
                        }
                    }
                });
        }

        public RemoveFriendResponse RemoveFriend(RemoveFriendRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);
            int otherAccountId = request.FriendAccountId;

            return FriendServiceContext.ExecuteDbOperation(
                FriendServiceContext.CONTEXT_REMOVE_FRIEND,
                connection =>
                {
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        try
                        {
                            var db = new FriendDbContext(connection, transaction, myAccountId);

                            int? friendRequestId = _friendRequestRepository.GetLatestAcceptedFriendRequestId(db, otherAccountId);
                            if (!friendRequestId.HasValue)
                            {
                                transaction.Commit();

                                FriendServiceContext.Logger.InfoFormat(
                                    "RemoveFriend: no friendship found. Me={0}, Other={1}",
                                    myAccountId,
                                    otherAccountId);

                                return CreateRemoveFriendResponse(false);
                            }

                            int affectedRows = _friendRequestRepository.MarkFriendRequestCancelled(db, friendRequestId.Value);
                            EnsureAffectedRowsOrRace(affectedRows);

                            transaction.Commit();

                            FriendServiceContext.Logger.InfoFormat(
                                "RemoveFriend: friendship marked as cancelled. Me={0}, Other={1}, RequestId={2}",
                                myAccountId,
                                otherAccountId,
                                friendRequestId.Value);

                            return CreateRemoveFriendResponse(true);
                        }
                        catch (SqlException ex)
                        {
                            SqlFaultMapping mapped = SqlExceptionFaultMapper.Map(
                                ex,
                                FriendServiceContext.OP_KEY_REMOVE_FRIEND);

                            FriendServiceContext.Logger.Error("RemoveFriend: SQL exception.", ex);

                            throw FriendServiceContext.ThrowFault(
                                FriendServiceContext.ERROR_DB,
                                mapped.MessageKey);
                        }
                    }
                });
        }

        private static void EnsureFriendshipDoesNotExist(
            IFriendRequestRepository friendRequestRepository,
            FriendDbContext db,
            int targetAccountId)
                {
                    bool exists = friendRequestRepository.FriendshipExists(db, targetAccountId);
                    if (exists)
                    {
                        throw FriendServiceContext.ThrowFault(
                            FriendServiceContext.ERROR_FR_ALREADY,
                            FriendServiceContext.MESSAGE_FR_ALREADY);
                    }
                }


        private static void EnsureRequestPending(byte status, string errorCode, string messageKey)
        {
            if (status != (byte)FriendRequestState.Pending)
            {
                throw FriendServiceContext.ThrowFault(errorCode, messageKey);
            }
        }

        private static void EnsureRequestReceiver(int toAccountId, int myAccountId)
        {
            if (toAccountId != myAccountId)
            {
                throw FriendServiceContext.ThrowFault(
                    FriendServiceContext.ERROR_FORBIDDEN,
                    FriendServiceContext.MESSAGE_FORBIDDEN_CANNOT_ACCEPT);
            }
        }

        private static void EnsureAffectedRowsOrRace(int affectedRows)
        {
            if (affectedRows == 0)
            {
                throw FriendServiceContext.ThrowFault(
                    FriendServiceContext.ERROR_RACE,
                    FriendServiceContext.MESSAGE_STATE_CHANGED);
            }
        }

        private static SendFriendRequestResponse CreateSendFriendRequestResponse(int friendRequestId, FriendRequestStatus status)
        {
            return new SendFriendRequestResponse
            {
                FriendRequestId = friendRequestId,
                Status = status
            };
        }

        private static RejectFriendRequestResponse CreateRejectFriendRequestResponse(FriendRequestStatus status)
        {
            return new RejectFriendRequestResponse
            {
                Status = status
            };
        }

        private static RemoveFriendResponse CreateRemoveFriendResponse(bool removed)
        {
            return new RemoveFriendResponse
            {
                Removed = removed
            };
        }

        private static FriendSummary CreateNewFriendSummary(int accountId, DateTime sinceUtc)
        {
            return new FriendSummary
            {
                AccountId = accountId,
                DisplayName = string.Empty,
                AvatarUrl = string.Empty,
                SinceUtc = sinceUtc,
                IsOnline = false
            };
        }

        private static AcceptFriendRequestResponse CreateAcceptFriendRequestResponse(FriendSummary newFriend)
        {
            return new AcceptFriendRequestResponse
            {
                NewFriend = newFriend
            };
        }
    }
}
