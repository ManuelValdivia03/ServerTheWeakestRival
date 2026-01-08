using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Lobby;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(
        InstanceContextMode = InstanceContextMode.Single,
        ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class LobbyService : ILobbyService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LobbyService));

        private readonly LobbyCallbackHub callbackHub;

        private string connectionString;
        private LobbyRepository lobbyRepository;

        private LobbyRoomOperations lobbyRoomOperations;
        private LobbyAccountOperations lobbyAccountOperations;
        private LobbyMatchOperations lobbyMatchOperations;

        public LobbyService()
        {
            // Importante: NO leer config aquí (como antes).
            callbackHub = LobbyCallbackHub.Shared;
        }

        public JoinLobbyResponse JoinLobby(JoinLobbyRequest request)
        {
            LobbyServiceContext.ValidateRequest(request);

            try
            {
                int userId = LobbyServiceContext.Authenticate(request.Token);

                ILobbyClientCallback callback = LobbyServiceContext.GetCallbackChannel();
                LobbyCallbackRegistry.Upsert(userId, callback);

                var lobbyInfo = new LobbyInfo
                {
                    LobbyId = Guid.NewGuid(),
                    LobbyName = string.IsNullOrWhiteSpace(request.LobbyName)
                        ? LobbyServiceConstants.DEFAULT_LOBBY_NAME
                        : request.LobbyName,
                    MaxPlayers = LobbyServiceConstants.DEFAULT_MAX_PLAYERS,
                    Players = new List<AccountMini>(),
                    AccessCode = null
                };

                Logger.InfoFormat(
                    "JoinLobby: New in-memory lobby created. LobbyId={0}, LobbyName={1}",
                    lobbyInfo.LobbyId,
                    lobbyInfo.LobbyName);

                callbackHub.AddCallback(lobbyInfo.LobbyId, userId, callback);

                try
                {
                    callback.OnLobbyUpdated(lobbyInfo);
                }
                catch (Exception ex)
                {
                    Logger.Warn("JoinLobby: error enviando OnLobbyUpdated al cliente.", ex);
                }

                return new JoinLobbyResponse { Lobby = lobbyInfo };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw LobbyServiceContext.ThrowTechnicalFault(
                    LobbyServiceConstants.ERROR_UNEXPECTED,
                    LobbyServiceConstants.MESSAGE_UNEXPECTED_ERROR,
                    LobbyServiceConstants.CTX_JOIN_LOBBY,
                    ex);
            }
        }

        public void LeaveLobby(LeaveLobbyRequest request)
        {
            int userId = 0;

            try
            {
                if (request == null
                    || string.IsNullOrWhiteSpace(request.Token)
                    || request.LobbyId == Guid.Empty)
                {
                    Logger.Debug("LeaveLobby: invalid request (null, empty token or empty LobbyId).");
                    return;
                }

                if (!TokenStore.TryGetUserId(request.Token, out userId))
                {
                    Logger.WarnFormat("LeaveLobby: invalid token. LobbyId={0}", request.LobbyId);
                    return;
                }

                EnsureInitialized();

                lobbyRoomOperations.LeaveLobby(userId, request.LobbyId);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error at LeaveLobby.", ex);
                return;
            }
            finally
            {
                if (request != null && request.LobbyId != Guid.Empty && userId > 0)
                {
                    callbackHub.RemoveCallback(request.LobbyId, userId);
                }

                if (userId > 0)
                {
                    LobbyCallbackRegistry.Remove(userId);
                }
            }
        }

        public ListLobbiesResponse ListLobbies(ListLobbiesRequest request)
        {
            Logger.Debug("ListLobbies called (currently returns empty list).");

            return new ListLobbiesResponse
            {
                Lobbies = new List<LobbyInfo>()
            };
        }

        public void SendChatMessage(SendLobbyMessageRequest request)
        {
            if (request == null
                || request.LobbyId == Guid.Empty
                || string.IsNullOrWhiteSpace(request.Token))
            {
                Logger.Debug("SendChatMessage: invalid request (null, empty LobbyId or token).");
                return;
            }

            if (!TokenStore.TryGetUserId(request.Token, out int userId))
            {
                Logger.WarnFormat("SendChatMessage: invalid token. LobbyId={0}", request.LobbyId);
                return;
            }

            EnsureInitialized();

            callbackHub.TryRefreshLobbyCallbackRegistry(userId);

            string senderName = lobbyRepository.GetUserDisplayName(userId);

            var chatMessage = new ChatMessage
            {
                FromPlayerId = Guid.Empty,
                FromPlayerName = senderName,
                Message = request.Message ?? string.Empty,
                SentAtUtc = DateTime.UtcNow
            };

            Logger.InfoFormat(
                "SendChatMessage: LobbyId={0}, UserId={1}, SenderName={2}",
                request.LobbyId,
                userId,
                senderName);

            callbackHub.Broadcast(request.LobbyId, cb => cb.OnChatMessageReceived(chatMessage));
        }

        public UpdateAccountResponse GetMyProfile(string token)
        {
            EnsureInitialized();
            return lobbyAccountOperations.GetMyProfile(token);
        }

        public UpdateAccountResponse UpdateAccount(UpdateAccountRequest request)
        {
            EnsureInitialized();
            return lobbyAccountOperations.UpdateAccount(request);
        }

        public void UpdateAvatar(UpdateAvatarRequest request)
        {
            EnsureInitialized();
            lobbyAccountOperations.UpdateAvatar(request);
        }

        public CreateLobbyResponse CreateLobby(CreateLobbyRequest request)
        {
            LobbyServiceContext.ValidateRequest(request);

            int ownerId = LobbyServiceContext.Authenticate(request.Token);

            ILobbyClientCallback callback = LobbyServiceContext.GetCallbackChannel();
            LobbyCallbackRegistry.Upsert(ownerId, callback);

            EnsureInitialized();
            return lobbyRoomOperations.CreateLobby(request, ownerId, callback);
        }

        public JoinByCodeResponse JoinByCode(JoinByCodeRequest request)
        {
            LobbyServiceContext.ValidateRequest(request);

            if (string.IsNullOrWhiteSpace(request.AccessCode))
            {
                throw LobbyServiceContext.ThrowFault(LobbyServiceConstants.ERROR_INVALID_REQUEST, "AccessCode requerido.");
            }

            int userId = LobbyServiceContext.Authenticate(request.Token);

            ILobbyClientCallback callback = LobbyServiceContext.GetCallbackChannel();
            LobbyCallbackRegistry.Upsert(userId, callback);

            EnsureInitialized();
            return lobbyRoomOperations.JoinByCode(request, userId, callback);
        }

        public StartLobbyMatchResponse StartLobbyMatch(StartLobbyMatchRequest request)
        {
            EnsureInitialized();
            return lobbyMatchOperations.StartLobbyMatch(request);
        }

        public StartLobbyMatchResponse StartLobbyMatch(StartLobbyMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request nulo.");
            }

            var hostUserId = EnsureAuthorizedAndGetUserId(request.Token);

            try
            {
                var manager = new MatchManager(Connection);

                var maxPlayers =
                    request.MaxPlayers > 0
                        ? (int)request.MaxPlayers
                        : DEFAULT_MAX_PLAYERS;

                var config = request.Config ?? new MatchConfigDto
                {
                    StartingScore = 0m,
                    MaxScore = 100m,
                    PointsPerCorrect = 1m,
                    PointsPerWrong = -1m,
                    PointsPerEliminationGain = 0m,
                    AllowTiebreakCoinflip = true
                };

                var createRequest = new CreateMatchRequest
                {
                    Token = request.Token,
                    MaxPlayers = maxPlayers,
                    Config = config,
                    IsPrivate = request.IsPrivate
                };

                var createResponse = manager.CreateMatch(hostUserId, createRequest);
                var match = createResponse.Match;

                if (match == null)
                {
                    throw ThrowTechnicalFault(
                        ERROR_UNEXPECTED,
                        MESSAGE_UNEXPECTED_ERROR,
                        "LobbyService.StartLobbyMatch.NullMatch",
                        new InvalidOperationException("MatchManager returned null Match."));
                }

                match.Config = config;

                if (TryGetLobbyUidForCurrentSession(out var lobbyUid))
                {
                    var lobbyId = GetLobbyIdFromUid(lobbyUid);
                    var members = GetLobbyMembers(lobbyId);
                    var avatarSql = new UserAvatarSql(Connection);
                    var accountMinis = MapToAccountMini(members, avatarSql);

                    match.Players = MapToPlayerSummaries(accountMinis);

                    Logger.InfoFormat(
                        "StartLobbyMatch: broadcasting OnMatchStarted. LobbyUid={0}, PlayersCount={1}",
                        lobbyUid,
                        match.Players != null ? match.Players.Count : 0);

                    var lobbyUidCopy = lobbyUid;
                    var matchCopy = match;

                    Task.Run(() => BroadcastToLobby(lobbyUidCopy, cb => cb.OnMatchStarted(matchCopy)));
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
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "LobbyService.StartLobbyMatch",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "LobbyService.StartLobbyMatch",
                    ex);
            }
        }

        public void UpdateAvatar(UpdateAvatarRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request nulo.");
            }

            var userId = EnsureAuthorizedAndGetUserId(request.Token);

            try
            {
                var avatarEntity = new UserAvatarEntity
                {
                    UserId = userId,
                    BodyColor = request.BodyColor,
                    PantsColor = request.PantsColor,
                    HatType = request.HatType,
                    HatColor = request.HatColor,
                    FaceType = request.FaceType,
                    UseProfilePhoto = request.UseProfilePhotoAsFace
                };

                var avatarSql = new UserAvatarSql(Connection);
                avatarSql.Save(avatarEntity);

                Logger.InfoFormat(
                    "UpdateAvatar: avatar updated. UserId={0}, BodyColor={1}, PantsColor={2}, HatType={3}, HatColor={4}, FaceType={5}, UsePhoto={6}",
                    userId,
                    avatarEntity.BodyColor,
                    avatarEntity.PantsColor,
                    avatarEntity.HatType,
                    avatarEntity.HatColor,
                    avatarEntity.FaceType,
                    avatarEntity.UseProfilePhoto);
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "LobbyService.UpdateAvatar",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "LobbyService.UpdateAvatar",
                    ex);
            }
        }

        internal static void ForceLogoutAndKickFromLobby(int accountId, byte sanctionType, DateTime? sanctionEndAtUtc)
        {
            LobbyKickCoordinator.ForceLogoutAndKickFromLobby(accountId, sanctionType, sanctionEndAtUtc);
        }

        private void EnsureInitialized()
        {
            if (lobbyRepository != null && lobbyRoomOperations != null && lobbyAccountOperations != null && lobbyMatchOperations != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = ResolveConnectionString(LobbyServiceConstants.MAIN_CONNECTION_STRING_NAME);
            }

            if (lobbyRepository == null)
            {
                lobbyRepository = new LobbyRepository(connectionString);
            }

            if (lobbyRoomOperations == null)
            {
                lobbyRoomOperations = new LobbyRoomOperations(lobbyRepository, callbackHub, CreateAvatarSql);
            }

            if (lobbyAccountOperations == null)
            {
                lobbyAccountOperations = new LobbyAccountOperations(lobbyRepository, CreateAvatarSql);
            }

            if (lobbyMatchOperations == null)
            {
                lobbyMatchOperations = new LobbyMatchOperations(connectionString, callbackHub, lobbyRepository, CreateAvatarSql);
            }
        }

        private UserAvatarSql CreateAvatarSql()
        {
            return new UserAvatarSql(connectionString);
        }

        private static string ResolveConnectionString(string name)
        {
            var setting = ConfigurationManager.ConnectionStrings[name];

            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                throw new InvalidOperationException(
                    string.Format("Falta connectionString '{0}' en App.config.", name));
            }

        public StartLobbyMatchResponse StartLobbyMatch(StartLobbyMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request nulo.");
            }

            var hostUserId = EnsureAuthorizedAndGetUserId(request.Token);

            try
            {
                var manager = new MatchManager(Connection);

                var maxPlayers =
                    request.MaxPlayers > 0
                        ? (int)request.MaxPlayers
                        : DEFAULT_MAX_PLAYERS;

                var config = request.Config ?? new MatchConfigDto
                {
                    StartingScore = 0m,
                    MaxScore = 100m,
                    PointsPerCorrect = 1m,
                    PointsPerWrong = -1m,
                    PointsPerEliminationGain = 0m,
                    AllowTiebreakCoinflip = true
                };

                var createRequest = new CreateMatchRequest
                {
                    Token = request.Token,
                    MaxPlayers = maxPlayers,
                    Config = config,
                    IsPrivate = request.IsPrivate
                };

                var createResponse = manager.CreateMatch(hostUserId, createRequest);
                var match = createResponse.Match;

                if (match == null)
                {
                    throw ThrowTechnicalFault(
                        ERROR_UNEXPECTED,
                        MESSAGE_UNEXPECTED_ERROR,
                        "LobbyService.StartLobbyMatch.NullMatch",
                        new InvalidOperationException("MatchManager returned null Match."));
                }

                match.Config = config;

                if (TryGetLobbyUidForCurrentSession(out var lobbyUid))
                {
                    var lobbyId = GetLobbyIdFromUid(lobbyUid);
                    var members = GetLobbyMembers(lobbyId);
                    var avatarSql = new UserAvatarSql(Connection);
                    var accountMinis = MapToAccountMini(members, avatarSql);

                    match.Players = MapToPlayerSummaries(accountMinis);

                    Logger.InfoFormat(
                        "StartLobbyMatch: broadcasting OnMatchStarted. LobbyUid={0}, PlayersCount={1}",
                        lobbyUid,
                        match.Players != null ? match.Players.Count : 0);

                    BroadcastToLobby(
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
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "LobbyService.StartLobbyMatch",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "LobbyService.StartLobbyMatch",
                    ex);
            }
        }


        private static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            var fault = new ServiceFault
            {
                Code = code,
                Message = userMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }

        private static bool TryGetLobbyUidForCurrentSession(out Guid lobbyUid)
        {
            foreach (var kv in CallbackBuckets)
            {
                if (kv.Value.ContainsKey(CurrentSessionId))
                {
                    lobbyUid = kv.Key;
                    return true;
                }
            }

            lobbyUid = Guid.Empty;
            return false;
        }

        public void UpdateAvatar(UpdateAvatarRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request nulo.");
            }

            var userId = EnsureAuthorizedAndGetUserId(request.Token);

            try
            {
                var avatarEntity = new UserAvatarEntity
                {
                    UserId = userId,
                    BodyColor = request.BodyColor,
                    PantsColor = request.PantsColor,
                    HatType = request.HatType,
                    HatColor = request.HatColor,
                    FaceType = request.FaceType,
                    UseProfilePhoto = request.UseProfilePhotoAsFace
                };

                var avatarSql = new UserAvatarSql(Connection);
                avatarSql.Save(avatarEntity);

                Logger.InfoFormat(
                    "UpdateAvatar: avatar updated. UserId={0}, BodyColor={1}, PantsColor={2}, HatType={3}, HatColor={4}, FaceType={5}, UsePhoto={6}",
                    userId,
                    avatarEntity.BodyColor,
                    avatarEntity.PantsColor,
                    avatarEntity.HatType,
                    avatarEntity.HatColor,
                    avatarEntity.FaceType,
                    avatarEntity.UseProfilePhoto);
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "LobbyService.UpdateAvatar",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "LobbyService.UpdateAvatar",
                    ex);
            }
        }

        private static List<AccountMini> MapToAccountMini(
            List<LobbyMembers> members,
            UserAvatarSql avatarSql)
        {
            var accountMinis = new List<AccountMini>();

            foreach (var member in members)
            {
                if (member.Users == null)
                {
                    Logger.WarnFormat(
                        "MapToAccountMini: Usuario nulo para member.user_id={0}",
                        member.user_id);
                    continue;
                }

                var avatarEntity = avatarSql.GetByUserId(member.user_id);

                accountMinis.Add(
                    new AccountMini
                    {
                        AccountId = member.user_id,
                        DisplayName = member.Users.display_name ?? (DEFAULT_PLAYER_NAME_PREFIX + member.user_id),
                        AvatarUrl = member.Users.profile_image_url,
                        Avatar = MapAvatar(avatarEntity)
                    });
            }

            return accountMinis;
        }

        private static List<PlayerSummary> MapToPlayerSummaries(List<AccountMini> accounts)
        {
            var players = new List<PlayerSummary>();

            if (accounts == null)
            {
                return players;
            }

            foreach (var account in accounts)
            {
                players.Add(
                    new PlayerSummary
                    {
                        UserId = account.AccountId,
                        DisplayName = account.DisplayName,
                        IsOnline = true,
                        Avatar = account.Avatar
                    });
            }

            return players;
        }


        private static List<LobbyMembers> GetLobbyMembers(int lobbyId)
        {
            var members = new List<LobbyMembers>();

            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_LOBBY_MEMBERS_WITH_USERS, sqlConnection))
            {
                sqlCommand.Parameters.Add(PARAM_LOBBY_ID, SqlDbType.Int).Value = lobbyId;

                sqlConnection.Open();

                using (var reader = sqlCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        members.Add(
                            new LobbyMembers
                            {
                                lobby_id = reader.GetInt32(0),
                                user_id = reader.GetInt32(1),
                                role = reader.GetByte(2),
                                joined_at_utc = reader.GetDateTime(3),
                                left_at_utc = reader.IsDBNull(4)
                                    ? (DateTime?)null
                                    : reader.GetDateTime(4),
                                is_active = reader.GetBoolean(5),
                                Users = new Users
                                {
                                    user_id = reader.GetInt32(6),
                                    display_name = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    profile_image_url = reader.IsDBNull(8) ? null : reader.GetString(8)
                                }
                            });
                    }
                }
            }

            return members;
        }

        private static void TryBroadcastLobbyUpdated(Guid lobbyUid, int lobbyId)
        {
            try
            {
                var members = GetLobbyMembers(lobbyId);
                var avatarSql = new UserAvatarSql(Connection);
                var accountMinis = MapToAccountMini(members, avatarSql);

                var info = LoadLobbyInfoByIntId(lobbyId);
                info.Players = accountMinis;

                BroadcastToLobby(
                    lobbyUid,
                    cb =>
                    {
                        try
                        {
                            cb.OnLobbyUpdated(info);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Error broadcasting lobby update.", ex);
                        }
                    });
            }
            catch (Exception ex)
            {
                Logger.Error("Error rebuilding lobby info for broadcast.", ex);
            }
        }


        public void UpdateAvatar(UpdateAvatarRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request nulo.");
            }

            var userId = EnsureAuthorizedAndGetUserId(request.Token);

            try
            {
                var avatarEntity = new UserAvatarEntity
                {
                    UserId = userId,
                    BodyColor = request.BodyColor,
                    PantsColor = request.PantsColor,
                    HatType = request.HatType,
                    HatColor = request.HatColor,
                    FaceType = request.FaceType,
                    UseProfilePhoto = request.UseProfilePhotoAsFace
                };

                var avatarSql = new UserAvatarSql(Connection);
                avatarSql.Save(avatarEntity);

                Logger.InfoFormat(
                    "UpdateAvatar: avatar updated. UserId={0}, BodyColor={1}, PantsColor={2}, HatType={3}, HatColor={4}, FaceType={5}, UsePhoto={6}",
                    userId,
                    avatarEntity.BodyColor,
                    avatarEntity.PantsColor,
                    avatarEntity.HatType,
                    avatarEntity.HatColor,
                    avatarEntity.FaceType,
                    avatarEntity.UseProfilePhoto);
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "LobbyService.UpdateAvatar",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "LobbyService.UpdateAvatar",
                    ex);
            }
        }

        private static List<AccountMini> MapToAccountMini(
            List<LobbyMembers> members,
            UserAvatarSql avatarSql)
        {
            var accountMinis = new List<AccountMini>();

            foreach (var member in members)
            {
                if (member.Users == null)
                {
                    Logger.WarnFormat(
                        "MapToAccountMini: Usuario nulo para member.user_id={0}",
                        member.user_id);
                    continue;
                }

                var avatarEntity = avatarSql.GetByUserId(member.user_id);

                accountMinis.Add(
                    new AccountMini
                    {
                        AccountId = member.user_id,
                        DisplayName = member.Users.display_name ?? (DEFAULT_PLAYER_NAME_PREFIX + member.user_id),
                        AvatarUrl = member.Users.profile_image_url,
                        Avatar = MapAvatar(avatarEntity)
                    });
            }

            return accountMinis;
        }

        private static List<PlayerSummary> MapToPlayerSummaries(List<AccountMini> accounts)
        {
            var players = new List<PlayerSummary>();

            if (accounts == null)
            {
                return players;
            }

            foreach (var account in accounts)
            {
                players.Add(
                    new PlayerSummary
                    {
                        UserId = account.AccountId,
                        DisplayName = account.DisplayName,
                        IsOnline = true,
                        Avatar = account.Avatar
                    });
            }

            return players;
        }


        private static List<LobbyMembers> GetLobbyMembers(int lobbyId)
        {
            var members = new List<LobbyMembers>();

            using (var sqlConnection = new SqlConnection(Connection))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_LOBBY_MEMBERS_WITH_USERS, sqlConnection))
            {
                sqlCommand.Parameters.Add(PARAM_LOBBY_ID, SqlDbType.Int).Value = lobbyId;

                sqlConnection.Open();

                using (var reader = sqlCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        members.Add(
                            new LobbyMembers
                            {
                                lobby_id = reader.GetInt32(0),
                                user_id = reader.GetInt32(1),
                                role = reader.GetByte(2),
                                joined_at_utc = reader.GetDateTime(3),
                                left_at_utc = reader.IsDBNull(4)
                                    ? (DateTime?)null
                                    : reader.GetDateTime(4),
                                is_active = reader.GetBoolean(5),
                                Users = new Users
                                {
                                    user_id = reader.GetInt32(6),
                                    display_name = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    profile_image_url = reader.IsDBNull(8) ? null : reader.GetString(8)
                                }
                            });
                    }
                }
            }

            return members;
        }

        private static void TryBroadcastLobbyUpdated(Guid lobbyUid, int lobbyId)
        {
            try
            {
                var members = GetLobbyMembers(lobbyId);
                var avatarSql = new UserAvatarSql(Connection);
                var accountMinis = MapToAccountMini(members, avatarSql);

                var info = LoadLobbyInfoByIntId(lobbyId);
                info.Players = accountMinis;

                BroadcastToLobby(
                    lobbyUid,
                    cb =>
                    {
                        try
                        {
                            cb.OnLobbyUpdated(info);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Error broadcasting lobby update.", ex);
                        }
                    });
            }
            catch (Exception ex)
            {
                Logger.Error("Error rebuilding lobby info for broadcast.", ex);
            }
        }

    }
}
