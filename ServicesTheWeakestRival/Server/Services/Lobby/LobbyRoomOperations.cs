using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using TheWeakestRival.Data;

namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public sealed class LobbyRoomOperations
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyRoomOperations));

        private readonly LobbyRepository lobbyRepository;
        private readonly LobbyCallbackHub callbackHub;
        private readonly Func<UserAvatarSql> avatarSqlFactory;

        public LobbyRoomOperations(
            LobbyRepository lobbyRepository,
            LobbyCallbackHub callbackHub,
            Func<UserAvatarSql> avatarSqlFactory)
        {
            this.lobbyRepository = lobbyRepository;
            this.callbackHub = callbackHub;
            this.avatarSqlFactory = avatarSqlFactory;
        }

        public CreateLobbyResponse CreateLobby(CreateLobbyRequest request, int ownerId, ILobbyClientCallback callback)
        {
            int maxPlayers =
                request.MaxPlayers.HasValue && request.MaxPlayers.Value > 0
                    ? (int)request.MaxPlayers.Value
                    : LobbyServiceConstants.DEFAULT_MAX_PLAYERS;

            CreateLobbyDbResult created =
                lobbyRepository.CreateLobby(ownerId, request.LobbyName, maxPlayers);

            Logger.InfoFormat(
                "CreateLobby: Lobby created. LobbyIdInt={0}, LobbyUid={1}, OwnerUserId={2}",
                created.LobbyId,
                created.LobbyUid,
                ownerId);

            List<AccountMini> accountMinis = BuildAccountMinis(created.LobbyId);

            callbackHub.AddCallback(created.LobbyUid, ownerId, callback);

            LobbyInfo info = lobbyRepository.LoadLobbyInfoByIntId(created.LobbyId);
            info.Players = accountMinis;

            if (string.IsNullOrWhiteSpace(info.AccessCode))
            {
                info.AccessCode = created.AccessCode;
            }

            TryNotifyClientLobbyUpdated(callback, info, "CreateLobby");

            return new CreateLobbyResponse { Lobby = info };
        }

        public JoinByCodeResponse JoinByCode(JoinByCodeRequest request, int userId, ILobbyClientCallback callback)
        {
            JoinLobbyDbResult joined =
                lobbyRepository.JoinByCode(userId, request.AccessCode);

            Logger.InfoFormat(
                "JoinByCode: UserId={0} joined lobby. LobbyIdInt={1}, LobbyUid={2}, AccessCode={3}",
                userId,
                joined.LobbyId,
                joined.LobbyUid,
                request.AccessCode);

            List<AccountMini> accountMinis = BuildAccountMinis(joined.LobbyId);

            callbackHub.AddCallback(joined.LobbyUid, userId, callback);

            LobbyInfo info = lobbyRepository.LoadLobbyInfoByIntId(joined.LobbyId);
            info.Players = accountMinis;

            TryNotifyClientLobbyUpdated(callback, info, "JoinByCode");
            BroadcastLobbyUpdatedExcluding(joined.LobbyUid, callback, info);

            return new JoinByCodeResponse { Lobby = info };
        }

        public void LeaveLobby(int userId, Guid lobbyUid)
        {
            int lobbyId = lobbyRepository.GetLobbyIdFromUid(lobbyUid);

            lobbyRepository.LeaveLobby(userId, lobbyId);

            Logger.InfoFormat(
                "LeaveLobby: user left lobby. UserId={0}, LobbyUid={1}, LobbyId={2}",
                userId,
                lobbyUid,
                lobbyId);

            TryBroadcastLobbyUpdated(lobbyUid, lobbyId);
        }

        public void TryBroadcastLobbyUpdated(Guid lobbyUid, int lobbyId)
        {
            try
            {
                List<AccountMini> accountMinis = BuildAccountMinis(lobbyId);

                LobbyInfo info = lobbyRepository.LoadLobbyInfoByIntId(lobbyId);
                info.Players = accountMinis;

                callbackHub.Broadcast(
                    lobbyUid,
                    cb =>
                    {
                        try
                        {
                            cb.OnLobbyUpdated(info);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Broadcast OnLobbyUpdated failed.", ex);
                        }
                    });
            }
            catch (Exception ex)
            {
                Logger.Error("TryBroadcastLobbyUpdated failed.", ex);
            }
        }

        private List<AccountMini> BuildAccountMinis(int lobbyId)
        {
            List<LobbyMembers> members = lobbyRepository.GetLobbyMembers(lobbyId);
            UserAvatarSql avatarSql = avatarSqlFactory();

            return LobbyMappers.MapToAccountMini(members, avatarSql);
        }

        private static void TryNotifyClientLobbyUpdated(ILobbyClientCallback callback, LobbyInfo info, string context)
        {
            try
            {
                callback.OnLobbyUpdated(info);
            }
            catch (Exception ex)
            {
                Logger.Warn(string.Format("{0}: error enviando OnLobbyUpdated al cliente.", context), ex);
            }
        }

        private void BroadcastLobbyUpdatedExcluding(Guid lobbyUid, ILobbyClientCallback excluded, LobbyInfo info)
        {
            callbackHub.Broadcast(
                lobbyUid,
                cb =>
                {
                    try
                    {
                        if (!ReferenceEquals(cb, excluded))
                        {
                            cb.OnLobbyUpdated(info);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Broadcast OnLobbyUpdated (excluding) failed.", ex);
                    }
                });
        }
    }
}
