using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using TheWeakestRival.Data;

namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public sealed class LobbyMatchOperations
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyMatchOperations));

        private readonly LobbyCallbackHub callbackHub;
        private readonly LobbyRepository lobbyRepository;
        private readonly Func<UserAvatarSql> avatarSqlFactory;
        private readonly string connectionString;

        public LobbyMatchOperations(
            string connectionString,
            LobbyCallbackHub callbackHub,
            LobbyRepository lobbyRepository,
            Func<UserAvatarSql> avatarSqlFactory)
        {
            this.connectionString = connectionString ?? string.Empty;
            this.callbackHub = callbackHub;
            this.lobbyRepository = lobbyRepository;
            this.avatarSqlFactory = avatarSqlFactory;
        }

        public StartLobbyMatchResponse StartLobbyMatch(StartLobbyMatchRequest request)
        {
            LobbyServiceContext.ValidateRequest(request);

            int hostUserId = LobbyServiceContext.Authenticate(request.Token);

            try
            {
                var manager = new ServicesTheWeakestRival.Server.Services.MatchManager(connectionString);

                int maxPlayers =
                    request.MaxPlayers > 0
                        ? request.MaxPlayers
                        : LobbyServiceConstants.DEFAULT_MAX_PLAYERS;



                MatchConfigDto config = BuildMatchConfigOrDefault(request.Config);

                var createRequest = new CreateMatchRequest
                {
                    Token = request.Token,
                    MaxPlayers = maxPlayers,
                    Config = config,
                    IsPrivate = request.IsPrivate
                };

                var createResponse = manager.CreateMatch(hostUserId, createRequest);
                var match = createResponse != null ? createResponse.Match : null;

                if (match == null)
                {
                    throw LobbyServiceContext.ThrowTechnicalFault(
                        LobbyServiceConstants.ERROR_UNEXPECTED,
                        LobbyServiceConstants.MESSAGE_UNEXPECTED_ERROR,
                        "LobbyService.StartLobbyMatch.NullMatch",
                        new InvalidOperationException("MatchManager returned null Match."));
                }

                match.Config = config;

                if (callbackHub.TryGetLobbyUidForCurrentSession(out Guid lobbyUid))
                {
                    int lobbyId = lobbyRepository.GetLobbyIdFromUid(lobbyUid);

                    List<LobbyMembers> members = lobbyRepository.GetLobbyMembers(lobbyId);
                    UserAvatarSql avatarSql = avatarSqlFactory();
                    List<AccountMini> accountMinis = LobbyMappers.MapToAccountMini(members, avatarSql);

                    match.Players = LobbyMappers.MapToPlayerSummaries(accountMinis);

                    Logger.InfoFormat(
                        "StartLobbyMatch: broadcasting OnMatchStarted. LobbyUid={0}, PlayersCount={1}",
                        lobbyUid,
                        match.Players != null ? match.Players.Count : 0);

                    callbackHub.Broadcast(
                        lobbyUid,
                        cb =>
                        {
                            try
                            {
                                cb.OnMatchStarted(match);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn("Error sending OnMatchStarted callback.", ex);
                            }
                        });
                }
                else
                {
                    Logger.Warn("StartLobbyMatch: could not resolve lobbyUid for current session.");
                }

                return new StartLobbyMatchResponse
                {
                    Match = match
                };
            }
            catch (System.ServiceModel.FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw LobbyServiceContext.ThrowTechnicalFault(
                    LobbyServiceConstants.ERROR_DB,
                    LobbyServiceConstants.MESSAGE_DB_ERROR,
                    LobbyServiceConstants.CTX_START_LOBBY_MATCH,
                    ex);
            }
            catch (Exception ex)
            {
                throw LobbyServiceContext.ThrowTechnicalFault(
                    LobbyServiceConstants.ERROR_UNEXPECTED,
                    LobbyServiceConstants.MESSAGE_UNEXPECTED_ERROR,
                    LobbyServiceConstants.CTX_START_LOBBY_MATCH,
                    ex);
            }
        }

        private static MatchConfigDto BuildMatchConfigOrDefault(MatchConfigDto config)
        {
            if (config != null)
            {
                return config;
            }

            return new MatchConfigDto
            {
                StartingScore = 0m,
                MaxScore = 100m,
                PointsPerCorrect = 1m,
                PointsPerWrong = -1m,
                PointsPerEliminationGain = 0m,
                AllowTiebreakCoinflip = true
            };
        }
    }
}
