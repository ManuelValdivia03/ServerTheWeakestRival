using System;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;

namespace ServicesTheWeakestRival.Server.Services.Friends
{
    internal sealed class FriendInviteLogic
    {
        private readonly IFriendInviteRepository inviteRepository;

        public FriendInviteLogic(IFriendInviteRepository inviteRepository)
        {
            this.inviteRepository = inviteRepository ??
                throw new ArgumentNullException(nameof(inviteRepository));
        }

        public SendLobbyInviteEmailResponse SendLobbyInviteEmail(SendLobbyInviteEmailRequest request)
        {
            FriendServiceContext.ValidateRequest(request);

            int myAccountId = FriendServiceContext.Authenticate(request.Token);

            if (request.TargetAccountId <= 0 || request.TargetAccountId == myAccountId)
            {
                throw FriendServiceContext.ThrowFault(
                    FriendServiceContext.ERROR_INVITE_INVALID_TARGET,
                    FriendServiceContext.MESSAGE_INVITE_INVALID_TARGET);
            }

            string lobbyCode = NormalizeLobbyCode(request.LobbyCode);
            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                throw FriendServiceContext.ThrowFault(
                    FriendServiceContext.ERROR_INVITE_INVALID_CODE,
                    FriendServiceContext.MESSAGE_INVITE_INVALID_CODE);
            }

            InviteEmailData data = FriendServiceContext.ExecuteDbOperation(
                FriendServiceContext.CONTEXT_SEND_LOBBY_INVITE_EMAIL,
                connection =>
                {
                    bool isFriend = inviteRepository.ExistsAcceptedFriendship(connection, myAccountId, request.TargetAccountId);
                    if (!isFriend)
                    {
                        throw FriendServiceContext.ThrowFault(
                            FriendServiceContext.ERROR_INVITE_NOT_FRIEND,
                            FriendServiceContext.MESSAGE_INVITE_NOT_FRIEND);
                    }

                    AccountContactLookup target = inviteRepository.GetAccountContact(connection, request.TargetAccountId);
                    if (!target.IsFound || string.IsNullOrWhiteSpace(target.Email))
                    {
                        throw FriendServiceContext.ThrowFault(
                            FriendServiceContext.ERROR_INVITE_ACCOUNT_NOT_FOUND,
                            FriendServiceContext.MESSAGE_INVITE_ACCOUNT_NOT_FOUND);
                    }

                    AccountContactLookup inviter = inviteRepository.GetAccountContact(connection, myAccountId);
                    string inviterName = !inviter.IsFound || string.IsNullOrWhiteSpace(inviter.DisplayName)
                        ? myAccountId.ToString()
                        : inviter.DisplayName;

                    return new InviteEmailData(target.Email, inviterName);
                });

            try
            {
                EmailSender.SendLobbyInvite(data.TargetEmail, data.InviterName, lobbyCode);

                FriendServiceContext.Logger.InfoFormat(
                    "Lobby invite email sent. From={0}, To={1}",
                    myAccountId,
                    request.TargetAccountId);

                return new SendLobbyInviteEmailResponse
                {
                    Sent = true
                };
            }
            catch (Exception ex)
            {
                throw FriendServiceContext.ThrowTechnicalFault(
                    FriendServiceContext.ERROR_INVITE_EMAIL_FAILED,
                    FriendServiceContext.MESSAGE_INVITE_EMAIL_FAILED,
                    FriendServiceContext.CONTEXT_SEND_LOBBY_INVITE_EMAIL,
                    ex);
            }
        }

        private static string NormalizeLobbyCode(string lobbyCode)
        {
            string safe = (lobbyCode ?? string.Empty).Trim();

            if (safe.Length == 0)
            {
                return string.Empty;
            }

            if (safe.Length > FriendServiceContext.MAX_LOBBY_CODE_LENGTH)
            {
                return string.Empty;
            }

            return safe;
        }

        private sealed class InviteEmailData
        {
            public InviteEmailData(string targetEmail, string inviterName)
            {
                TargetEmail = targetEmail ?? string.Empty;
                InviterName = inviterName ?? string.Empty;
            }

            public string TargetEmail { get; }

            public string InviterName { get; }
        }
    }
}
