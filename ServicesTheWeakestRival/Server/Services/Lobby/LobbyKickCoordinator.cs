using log4net;
using ServicesTheWeakestRival.Server.Infrastructure;
using System;
using System.Configuration;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public static class LobbyKickCoordinator
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyKickCoordinator));

        public static void ForceLogoutAndKickFromLobby(int accountId, byte sanctionType, DateTime? sanctionEndAtUtc)
        {
            if (accountId <= 0)
            {
                return;
            }

            try
            {
                string connectionString =
                    ConfigurationManager.ConnectionStrings[LobbyServiceConstants.MAIN_CONNECTION_STRING_NAME].ConnectionString;

                var repo = new LobbyRepository(connectionString);
                LobbyCallbackHub hub = LobbyCallbackHub.Shared;

                hub.TrySendForcedLogout(accountId, sanctionType, sanctionEndAtUtc);

                if (!hub.TryGetLobbyUidForAccount(accountId, out Guid lobbyUid) || lobbyUid == Guid.Empty)
                {
                    TryLeaveAll(repo, accountId);
                    LobbyCallbackRegistry.Remove(accountId);
                    hub.CleanupAccount(accountId);
                    return;
                }

                int lobbyId;
                try
                {
                    lobbyId = repo.GetLobbyIdFromUid(lobbyUid);
                }
                catch (Exception ex)
                {
                    Logger.Warn("ForceLogoutAndKickFromLobby: lobbyUid could not be resolved to lobbyId.", ex);
                    TryLeaveAll(repo, accountId);

                    hub.RemoveCallback(lobbyUid, accountId);
                    LobbyCallbackRegistry.Remove(accountId);
                    return;
                }

                try
                {
                    repo.LeaveLobby(accountId, lobbyId);
                }
                catch (SqlException ex)
                {
                    Logger.Warn("ForceLogoutAndKickFromLobby: error leaving lobby in DB.", ex);
                }
                catch (Exception ex)
                {
                    Logger.Warn("ForceLogoutAndKickFromLobby: unexpected error leaving lobby in DB.", ex);
                }

                TryBroadcastUpdated(connectionString, lobbyUid, lobbyId);

                hub.RemoveCallback(lobbyUid, accountId);
                LobbyCallbackRegistry.Remove(accountId);
            }
            catch (Exception ex)
            {
                Logger.Error("LobbyKickCoordinator.ForceLogoutAndKickFromLobby failed.", ex);
            }
        }

        private static void TryLeaveAll(LobbyRepository repo, int accountId)
        {
            try
            {
                repo.LeaveAllLobbiesByUser(accountId);
            }
            catch (Exception ex)
            {
                Logger.Warn("TryLeaveAll failed.", ex);
            }
        }

        private static void TryBroadcastUpdated(string connectionString, Guid lobbyUid, int lobbyId)
        {
            try
            {
                var repo = new LobbyRepository(connectionString);
                var ops = new LobbyRoomOperations(
                    repo,
                    LobbyCallbackHub.Shared,
                    () => new ServicesTheWeakestRival.Server.Services.Logic.UserAvatarSql(connectionString));

                ops.TryBroadcastLobbyUpdated(lobbyUid, lobbyId);
            }
            catch (Exception ex)
            {
                Logger.Warn("TryBroadcastUpdated failed.", ex);
            }
        }
    }
}
