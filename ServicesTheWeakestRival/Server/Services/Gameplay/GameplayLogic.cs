using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using TheWeakestRival.Contracts.Enums;
using TheWeakestRival.Data;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class GameplayLogic : IGameplayService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayLogic));

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_DB = "DB_ERROR";
        private const string ERROR_UNEXPECTED = "UNEXPECTED_ERROR";
        private const string ERROR_MATCH_NOT_FOUND = "MATCH_NOT_FOUND";
        private const string ERROR_NOT_PLAYER_TURN = "NOT_PLAYER_TURN";
        private const string ERROR_DUEL_NOT_ACTIVE = "DUEL_NOT_ACTIVE";
        private const string ERROR_NOT_WEAKEST_RIVAL = "NOT_WEAKEST_RIVAL";
        private const string ERROR_INVALID_DUEL_TARGET = "INVALID_DUEL_TARGET";
        private const string ERROR_MATCH_ALREADY_STARTED = "MATCH_ALREADY_STARTED";
        private const string ERROR_NO_QUESTIONS = "NO_QUESTIONS";
        private const string ERROR_NO_QUESTIONS_MESSAGE = "No se encontraron preguntas para la dificultad/idioma solicitados.";
        private const string FALLBACK_LOCALE_EN_US = "en-US";

        private const string ERROR_INVALID_REQUEST_MESSAGE = "Request is null.";
        private const string ERROR_MATCH_NOT_FOUND_MESSAGE = "Match not found.";
        private const string ERROR_NOT_PLAYER_TURN_MESSAGE = "It is not the player turn.";
        private const string ERROR_DUEL_NOT_ACTIVE_MESSAGE = "Duel is not active.";
        private const string ERROR_NOT_WEAKEST_RIVAL_MESSAGE = "Only weakest rival can choose duel opponent.";
        private const string ERROR_INVALID_DUEL_TARGET_MESSAGE = "Invalid duel opponent.";
        private const string ERROR_MATCH_ALREADY_STARTED_MESSAGE = "Match already started. Joining is not allowed.";

        private const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";

        private const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        private const string SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE =
            "LIGHTNING_WILDCARD_AWARDED";

        private const string SPECIAL_EVENT_LIGHTNING_WILDCARD_DESCRIPTION_TEMPLATE =
            "El jugador {0} ha ganado un comodín relámpago.";

        private const string SPECIAL_EVENT_EXTRA_WILDCARD_CODE =
            "EXTRA_WILDCARD_AWARDED";

        private const string SPECIAL_EVENT_EXTRA_WILDCARD_DESCRIPTION_TEMPLATE =
            "El jugador {0} ha recibido un comodín extra.";

        private const string SPECIAL_EVENT_BOMB_QUESTION_CODE =
            "BOMB_QUESTION";

        private const string SPECIAL_EVENT_BOMB_QUESTION_DESCRIPTION_TEMPLATE =
            "Pregunta bomba para {0}. Acierto: +{1} a la banca. Fallo: -{1} de lo bancado.";

        private const string SPECIAL_EVENT_BOMB_QUESTION_APPLIED_CODE =
            "BOMB_QUESTION_APPLIED";

        private const string SPECIAL_EVENT_BOMB_QUESTION_APPLIED_DESCRIPTION_TEMPLATE =
            "Pregunta bomba resuelta por {0}. Cambio en banca: {1}. Banca actual: {2}.";

        private const int DEFAULT_MAX_QUESTIONS = 40;
        private const int QUESTIONS_PER_PLAYER_PER_ROUND = 2;
        private const int VOTE_PHASE_TIME_LIMIT_SECONDS = 30;
        private const int MIN_PLAYERS_TO_CONTINUE = 2;

        private const int COIN_FLIP_RANDOM_MIN_VALUE = 0;
        private const int COIN_FLIP_RANDOM_MAX_VALUE = 100;
        private const int COIN_FLIP_THRESHOLD_VALUE = 50;

        private const int LIGHTNING_PROBABILITY_PERCENT = 0;
        private const int LIGHTNING_TOTAL_QUESTIONS = 3;
        private const int LIGHTNING_TOTAL_TIME_SECONDS = 30;
        private const int LIGHTNING_RANDOM_MIN_VALUE = 0;
        private const int LIGHTNING_RANDOM_MAX_VALUE = 100;

        private const int EXTRA_WILDCARD_RANDOM_MIN_VALUE = 0;
        private const int EXTRA_WILDCARD_RANDOM_MAX_VALUE = 100;
        private const int EXTRA_WILDCARD_PROBABILITY_PERCENT = 0;

        private const int BOMB_QUESTION_RANDOM_MIN_VALUE = 0;
        private const int BOMB_QUESTION_RANDOM_MAX_VALUE = 100;
        private const int BOMB_QUESTION_PROBABILITY_PERCENT = 20;

        private const decimal BOMB_BANK_DELTA = 0.50m;
        private const decimal MIN_BANKED_POINTS = 0.00m;

        private const decimal INITIAL_BANKED_POINTS = 5.00m;

        private const int TURN_USER_ID_NONE = 0;

        private static readonly decimal[] CHAIN_STEPS =
        {
            0.10m,
            0.20m,
            0.30m,
            0.40m,
            0.50m
        };

        private static readonly ConcurrentDictionary<Guid, MatchRuntimeState> Matches =
            new ConcurrentDictionary<Guid, MatchRuntimeState>();

        private static readonly ConcurrentDictionary<int, Guid> PlayerMatchByUserId =
            new ConcurrentDictionary<int, Guid>();

        // FIX: jugadores permitidos por match (los del lobby). Evita tocar MatchRuntimeState.
        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>> ExpectedPlayersByMatchId =
            new ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>>();

        private static readonly Random randomGenerator = new Random();
        private static readonly object randomSyncRoot = new object();

        private const string LOG_CTX_SUBMIT_ANSWER = "GameplayService.SubmitAnswer";
        private const string LOG_CTX_BANK = "GameplayService.Bank";
        private const string LOG_CTX_CAST_VOTE = "GameplayService.CastVote";
        private const string LOG_CTX_JOIN_MATCH = "GameplayService.JoinMatch";
        private const string LOG_CTX_START_MATCH = "GameplayService.StartMatch";
        private const string LOG_CTX_CHOOSE_DUEL = "GameplayService.ChooseDuelOpponent";
        private const string LOG_CTX_SEND_NEXT_QUESTION = "GameplayService.SendNextQuestion";
        private const string LOG_CTX_VOTE_PHASE = "GameplayService.VotePhase";
        private const string LOG_CTX_BOMB = "GameplayService.BombQuestion";
        private const string LOG_CTX_TURN_ORDER = "GameplayService.TurnOrder";

        public SubmitAnswerResponse SubmitAnswer(SubmitAnswerRequest request)
        {
            try
            {
                ValidateNotNullRequest(request);
                ValidateMatchId(request.MatchId);

                int userId = Authenticate(request.Token);
                MatchRuntimeState state = GetMatchOrThrow(request.MatchId);

                lock (state.SyncRoot)
                {
                    EnsureNotInVotePhase(state, "Round is in vote phase. No questions available.");

                    MatchPlayerRuntime currentPlayer = GetCurrentPlayerOrThrow(state, userId);

                    if (IsLightningActive(state))
                    {
                        return HandleLightningSubmitAnswer(request, state, currentPlayer);
                    }

                    QuestionWithAnswersDto question = GetCurrentQuestionOrThrow(state);
                    bool isCorrect = EvaluateAnswerOrThrow(question, request.AnswerText);

                    UpdateChainState(state, isCorrect);
                    ApplyBombQuestionEffectIfNeeded(state, currentPlayer, isCorrect);

                    AnswerResult result = BuildAnswerResult(request.QuestionId, state, isCorrect);
                    PlayerSummary playerSummary = BuildPlayerSummary(currentPlayer, isOnline: true);

                    Broadcast(
                        state,
                        cb => cb.OnAnswerEvaluated(state.MatchId, playerSummary, result),
                        LOG_CTX_SUBMIT_ANSWER);

                    SubmitAnswerResponse response = new SubmitAnswerResponse
                    {
                        Result = result
                    };

                    if (ShouldHandleDuelTurn(state, currentPlayer))
                    {
                        HandleDuelTurn(state, currentPlayer, isCorrect);
                        return response;
                    }

                    state.QuestionsAskedThisRound++;

                    int alivePlayersCount = CountAlivePlayersOrFallbackToTotal(state);
                    int maxQuestionsThisRound = alivePlayersCount * QUESTIONS_PER_PLAYER_PER_ROUND;
                    bool hasNoMoreQuestions = state.Questions.Count == 0;

                    if (state.QuestionsAskedThisRound >= maxQuestionsThisRound || hasNoMoreQuestions)
                    {
                        Logger.InfoFormat(
                            "End of round reached. MatchId={0}, Round={1}, QuestionsAskedThisRound={2}, AlivePlayers={3}, NoMoreQuestions={4}",
                            state.MatchId,
                            state.RoundNumber,
                            state.QuestionsAskedThisRound,
                            alivePlayersCount,
                            hasNoMoreQuestions);

                        StartVotePhase(state);
                        return response;
                    }

                    state.AdvanceTurn();
                    SendNextQuestion(state);

                    return response;
                }
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(ERROR_DB, MESSAGE_DB_ERROR, LOG_CTX_SUBMIT_ANSWER, ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(ERROR_UNEXPECTED, MESSAGE_UNEXPECTED_ERROR, LOG_CTX_SUBMIT_ANSWER, ex);
            }
        }

        public BankResponse Bank(BankRequest request)
        {
            ValidateNotNullRequest(request);
            ValidateMatchId(request.MatchId);

            int userId = Authenticate(request.Token);
            MatchRuntimeState state = GetMatchOrThrow(request.MatchId);

            lock (state.SyncRoot)
            {
                EnsureNotInVotePhase(state, "Round is in vote phase. Banking is not allowed.");

                GetCurrentPlayerOrThrow(state, userId);

                state.BankedPoints += state.CurrentChain;
                state.CurrentChain = 0m;
                state.CurrentStreak = 0;

                BankState bankState = new BankState
                {
                    MatchId = state.MatchId,
                    CurrentChain = state.CurrentChain,
                    BankedPoints = state.BankedPoints
                };

                Broadcast(
                    state,
                    cb => cb.OnBankUpdated(state.MatchId, bankState),
                    LOG_CTX_BANK);

                return new BankResponse
                {
                    Bank = bankState
                };
            }
        }

        public UseLifelineResponse UseLifeline(UseLifelineRequest request)
        {
            return new UseLifelineResponse
            {
                Outcome = "OK"
            };
        }

        public CastVoteResponse CastVote(CastVoteRequest request)
        {
            ValidateNotNullRequest(request);
            ValidateMatchId(request.MatchId);

            int userId = Authenticate(request.Token);
            MatchRuntimeState state = GetMatchOrThrow(request.MatchId);

            lock (state.SyncRoot)
            {
                if (!state.IsInVotePhase)
                {
                    throw ThrowFault(ERROR_INVALID_REQUEST, "Not in vote phase.");
                }

                HashSet<int> alivePlayers = state.Players
                    .Where(p => !p.IsEliminated)
                    .Select(p => p.UserId)
                    .ToHashSet();

                int? targetUserId = request.TargetUserId;

                if (targetUserId.HasValue && !alivePlayers.Contains(targetUserId.Value))
                {
                    throw ThrowFault(ERROR_INVALID_REQUEST, "Target player is not in match or already eliminated.");
                }

                state.VotesThisRound[userId] = targetUserId;
                state.VotersThisRound.Add(userId);

                Logger.InfoFormat(
                    "CastVote registered. MatchId={0}, VoterUserId={1}, TargetUserId={2}",
                    request.MatchId,
                    userId,
                    targetUserId.HasValue ? targetUserId.Value : 0);

                int alivePlayersCount = alivePlayers.Count;
                if (alivePlayersCount <= 0)
                {
                    alivePlayersCount = state.Players.Count;
                }

                if (state.VotersThisRound.Count >= alivePlayersCount)
                {
                    Logger.InfoFormat(
                        "All alive players voted. MatchId={0}, Round={1}, AlivePlayers={2}",
                        state.MatchId,
                        state.RoundNumber,
                        alivePlayersCount);

                    ResolveEliminationOrStartDuel(state);
                }

                return new CastVoteResponse
                {
                    Accepted = true
                };
            }
        }

        public AckEventSeenResponse AckEventSeen(AckEventSeenRequest request)
        {
            return new AckEventSeenResponse
            {
                Acknowledged = true
            };
        }

        public GetQuestionsResponse GetQuestions(GetQuestionsRequest request)
        {
            ValidateGetQuestionsRequest(request);
            Authenticate(request.Token);

            int maxQuestions = GetMaxQuestionsOrDefault(request.MaxQuestions);

            try
            {
                List<QuestionWithAnswersDto> questions = LoadQuestions(request.Difficulty, request.LocaleCode, maxQuestions);

                Logger.InfoFormat(
                    "GetQuestions: Difficulty={0}, Locale={1}, RequestedMax={2}, Returned={3}",
                    request.Difficulty,
                    request.LocaleCode,
                    maxQuestions,
                    questions.Count);

                return new GetQuestionsResponse
                {
                    Questions = questions
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
                    "GameplayService.GetQuestions",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "GameplayService.GetQuestions",
                    ex);
            }
        }

        public GameplayJoinMatchResponse JoinMatch(GameplayJoinMatchRequest request)
        {
            ValidateNotNullRequest(request);
            ValidateMatchId(request.MatchId);

            int userId = Authenticate(request.Token);

            IGameplayServiceCallback callback =
                OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

            MatchRuntimeState state = Matches.GetOrAdd(request.MatchId, id => new MatchRuntimeState(id));

            bool hasStarted;

            lock (state.SyncRoot)
            {
                hasStarted = state.HasStarted || state.IsInitialized;

                if (hasStarted)
                {
                    if (ExpectedPlayersByMatchId.TryGetValue(request.MatchId, out ConcurrentDictionary<int, byte> expectedPlayers) &&
                        expectedPlayers != null &&
                        expectedPlayers.Count > 0)
                    {
                        if (!expectedPlayers.ContainsKey(userId))
                        {
                            throw ThrowFault(ERROR_MATCH_ALREADY_STARTED, ERROR_MATCH_ALREADY_STARTED_MESSAGE);
                        }
                    }
                    else
                    {
                        // Legacy fallback: permite reconectar solo si ya existía en runtime
                        MatchPlayerRuntime existing = state.Players.Find(p => p.UserId == userId);
                        if (existing == null)
                        {
                            throw ThrowFault(ERROR_MATCH_ALREADY_STARTED, ERROR_MATCH_ALREADY_STARTED_MESSAGE);
                        }
                    }
                }

                MatchPlayerRuntime existingPlayer = state.Players.Find(p => p.UserId == userId);
                if (existingPlayer != null)
                {
                    existingPlayer.Callback = callback;
                }
                else
                {
                    string displayName = string.Format("Jugador {0}", userId);

                    UserAvatarSql avatarSql = new UserAvatarSql(GetConnectionString());
                    UserAvatarEntity avatarEntity = avatarSql.GetByUserId(userId);
                    AvatarAppearanceDto avatar = MapAvatar(avatarEntity);

                    MatchPlayerRuntime player = new MatchPlayerRuntime(userId, displayName, callback)
                    {
                        Avatar = avatar
                    };

                    state.Players.Add(player);
                }

                Logger.InfoFormat(
                    "JoinMatch: MatchId={0}, UserId={1}, PlayersInState={2}, HasStarted={3}",
                    request.MatchId,
                    userId,
                    state.Players.Count,
                    hasStarted);

                if (hasStarted && state.IsInitialized)
                {
                    TrySendSnapshotToJoiningPlayer(state, userId);
                }
            }

            PlayerMatchByUserId[userId] = request.MatchId;

            return new GameplayJoinMatchResponse
            {
                Accepted = true
            };
        }

        public GameplayStartMatchResponse StartMatch(GameplayStartMatchRequest request)
        {
            ValidateNotNullRequest(request);
            ValidateMatchId(request.MatchId);

            if (request.Difficulty <= 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Difficulty must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.LocaleCode))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "LocaleCode is required.");
            }

            int userId = Authenticate(request.Token);

            int maxQuestions = GetMaxQuestionsOrDefault(request.MaxQuestions);
            MatchRuntimeState state = Matches.GetOrAdd(request.MatchId, id => new MatchRuntimeState(id));

            try
            {
                lock (state.SyncRoot)
                {
                    // FIX: si ya inició, NO falla; solo asegura que exista el set de esperados.
                    if (state.HasStarted || state.IsInitialized)
                    {
                        StoreOrMergeExpectedPlayers(request.MatchId, request.ExpectedPlayerUserIds, userId);

                        return new GameplayStartMatchResponse
                        {
                            Started = true
                        };
                    }

                    state.HasStarted = true;

                    try
                    {
                        StoreOrMergeExpectedPlayers(request.MatchId, request.ExpectedPlayerUserIds, userId);

                        List<QuestionWithAnswersDto> questions = LoadQuestionsWithLocaleFallback(
                            request.Difficulty,
                            request.LocaleCode,
                            maxQuestions);

                        if (questions == null || questions.Count == 0)
                        {
                            throw ThrowFault(ERROR_NO_QUESTIONS, ERROR_NO_QUESTIONS_MESSAGE);
                        }

                        state.Initialize(
                            request.Difficulty,
                            request.LocaleCode,
                            questions,
                            INITIAL_BANKED_POINTS);

                        state.WildcardMatchId = request.MatchDbId;

                        EnsureHostPlayerRegistered(state, userId);
                        ResetRoundStateForStart(state);

                        ShufflePlayersForStart(state);

                        BroadcastTurnOrderInitialized(state);

                        TryStartExtraWildcardEvent(state);

                        bool hasLightningStarted = TryStartLightningChallenge(state);
                        if (!hasLightningStarted)
                        {
                            SendNextQuestion(state);
                        }

                        return new GameplayStartMatchResponse
                        {
                            Started = true
                        };
                    }
                    catch
                    {
                        state.HasStarted = false;
                        throw;
                    }
                }
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
                    "GameplayService.StartMatch",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "GameplayService.StartMatch",
                    ex);
            }
        }

        public ChooseDuelOpponentResponse ChooseDuelOpponent(ChooseDuelOpponentRequest request)
        {
            ValidateNotNullRequest(request);

            if (request.TargetUserId <= 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "TargetUserId is required.");
            }

            int userId = Authenticate(request.Token);

            if (!PlayerMatchByUserId.TryGetValue(userId, out Guid matchId))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            MatchRuntimeState state = GetMatchOrThrow(matchId);

            lock (state.SyncRoot)
            {
                if (!state.IsInDuelPhase || !state.WeakestRivalUserId.HasValue)
                {
                    throw ThrowFault(ERROR_DUEL_NOT_ACTIVE, ERROR_DUEL_NOT_ACTIVE_MESSAGE);
                }

                if (state.WeakestRivalUserId.Value != userId)
                {
                    throw ThrowFault(ERROR_NOT_WEAKEST_RIVAL, ERROR_NOT_WEAKEST_RIVAL_MESSAGE);
                }

                HashSet<int> alivePlayers = state.Players
                    .Where(p => !p.IsEliminated)
                    .Select(p => p.UserId)
                    .ToHashSet();

                if (!alivePlayers.Contains(request.TargetUserId))
                {
                    throw ThrowFault(ERROR_INVALID_DUEL_TARGET, ERROR_INVALID_DUEL_TARGET_MESSAGE);
                }

                HashSet<int> votersAgainstWeakest = state.VotesThisRound
                    .Where(kvp => kvp.Value.HasValue && kvp.Value.Value == state.WeakestRivalUserId.Value)
                    .Select(kvp => kvp.Key)
                    .ToHashSet();

                if (!votersAgainstWeakest.Contains(request.TargetUserId))
                {
                    throw ThrowFault(ERROR_INVALID_DUEL_TARGET, ERROR_INVALID_DUEL_TARGET_MESSAGE);
                }

                state.DuelTargetUserId = request.TargetUserId;

                int weakestIndex = state.Players.FindIndex(
                    p => p.UserId == state.WeakestRivalUserId.Value && !p.IsEliminated);

                if (weakestIndex >= 0)
                {
                    state.CurrentPlayerIndex = weakestIndex;
                    SendNextQuestion(state);
                }

                Logger.InfoFormat(
                    "ChooseDuelOpponent accepted. MatchId={0}, WeakestRivalUserId={1}, DuelTargetUserId={2}",
                    state.MatchId,
                    state.WeakestRivalUserId.Value,
                    state.DuelTargetUserId.Value);

                return new ChooseDuelOpponentResponse
                {
                    Accepted = true
                };
            }
        }

        // ---------------------------------------------------------------
        // TURNOS: Shuffle inicial + broadcast
        // ---------------------------------------------------------------
        private static void ShufflePlayersForStart(MatchRuntimeState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.Players.Count <= 1)
            {
                state.CurrentPlayerIndex = 0;
                return;
            }

            for (int i = state.Players.Count - 1; i > 0; i--)
            {
                int j = NextRandom(0, i + 1);

                MatchPlayerRuntime temp = state.Players[i];
                state.Players[i] = state.Players[j];
                state.Players[j] = temp;
            }

            state.CurrentPlayerIndex = 0;

            MatchPlayerRuntime current = state.GetCurrentPlayer();
            Logger.InfoFormat(
                "{0}: players shuffled for start. MatchId={1}, Players={2}, CurrentUserId={3}",
                LOG_CTX_TURN_ORDER,
                state.MatchId,
                state.Players.Count,
                current != null ? current.UserId : TURN_USER_ID_NONE);
        }

        private static void BroadcastTurnOrderInitialized(MatchRuntimeState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            int[] orderedAliveUserIds = state.Players
                .Where(p => p != null && !p.IsEliminated)
                .Select(p => p.UserId)
                .ToArray();

            int currentUserId = TURN_USER_ID_NONE;
            MatchPlayerRuntime current = state.GetCurrentPlayer();
            if (current != null)
            {
                currentUserId = current.UserId;
            }

            TurnOrderDto dto = new TurnOrderDto
            {
                OrderedAliveUserIds = orderedAliveUserIds,
                CurrentTurnUserId = currentUserId,
                ServerUtcTicks = DateTime.UtcNow.Ticks
            };

            Broadcast(
                state,
                cb => cb.OnTurnOrderInitialized(state.MatchId, dto),
                LOG_CTX_TURN_ORDER);
        }

        // ---------------------------------------------------------------
        // FIX: Snapshot al que entra tarde (pero permitido)
        // ---------------------------------------------------------------
        private static void TrySendSnapshotToJoiningPlayer(MatchRuntimeState state, int userId)
        {
            if (state == null)
            {
                return;
            }

            MatchPlayerRuntime joiningPlayer = state.Players.FirstOrDefault(p => p.UserId == userId);
            if (joiningPlayer == null || joiningPlayer.Callback == null)
            {
                return;
            }

            try
            {
                int[] orderedAliveUserIds = state.Players
                    .Where(p => p != null && !p.IsEliminated)
                    .Select(p => p.UserId)
                    .ToArray();

                MatchPlayerRuntime current = state.GetCurrentPlayer();

                TurnOrderDto turnOrder = new TurnOrderDto
                {
                    OrderedAliveUserIds = orderedAliveUserIds,
                    CurrentTurnUserId = current != null ? current.UserId : TURN_USER_ID_NONE,
                    ServerUtcTicks = DateTime.UtcNow.Ticks
                };

                joiningPlayer.Callback.OnTurnOrderInitialized(state.MatchId, turnOrder);

                if (state.IsInVotePhase)
                {
                    joiningPlayer.Callback.OnVotePhaseStarted(
                        state.MatchId,
                        TimeSpan.FromSeconds(VOTE_PHASE_TIME_LIMIT_SECONDS));
                    return;
                }

                if (current != null &&
                    state.CurrentQuestionId > 0 &&
                    state.QuestionsById.TryGetValue(state.CurrentQuestionId, out QuestionWithAnswersDto question))
                {
                    PlayerSummary currentSummary = BuildPlayerSummary(current, isOnline: true);

                    joiningPlayer.Callback.OnNextQuestion(
                        state.MatchId,
                        currentSummary,
                        question,
                        state.CurrentChain,
                        state.BankedPoints);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("TrySendSnapshotToJoiningPlayer failed.", ex);
            }
        }

        // ---------------------------------------------------------------
        // FIX: store/merge expected players
        // ---------------------------------------------------------------
        private static void StoreOrMergeExpectedPlayers(Guid matchId, int[] expectedPlayerUserIds, int callerUserId)
        {
            if (matchId == Guid.Empty)
            {
                return;
            }

            ConcurrentDictionary<int, byte> set = ExpectedPlayersByMatchId.GetOrAdd(
                matchId,
                _ => new ConcurrentDictionary<int, byte>());

            if (expectedPlayerUserIds != null && expectedPlayerUserIds.Length > 0)
            {
                foreach (int id in expectedPlayerUserIds)
                {
                    if (id > 0)
                    {
                        set.TryAdd(id, 0);
                    }
                }
            }

            if (callerUserId > 0)
            {
                set.TryAdd(callerUserId, 0);
            }
        }

        // ---------------------------------------------------------------
        // VALIDACIONES / HELPERS EXISTENTES
        // ---------------------------------------------------------------
        private static void ValidateNotNullRequest(object request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }
        }

        private static void ValidateMatchId(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }
        }

        private static MatchRuntimeState GetMatchOrThrow(Guid matchId)
        {
            if (!Matches.TryGetValue(matchId, out MatchRuntimeState state))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            return state;
        }

        private static void EnsureNotInVotePhase(MatchRuntimeState state, string message)
        {
            if (state.IsInVotePhase)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, message);
            }
        }

        private static MatchPlayerRuntime GetCurrentPlayerOrThrow(MatchRuntimeState state, int userId)
        {
            MatchPlayerRuntime currentPlayer = state.GetCurrentPlayer();

            if (currentPlayer == null || currentPlayer.UserId != userId)
            {
                throw ThrowFault(ERROR_NOT_PLAYER_TURN, ERROR_NOT_PLAYER_TURN_MESSAGE);
            }

            return currentPlayer;
        }

        private static QuestionWithAnswersDto GetCurrentQuestionOrThrow(MatchRuntimeState state)
        {
            if (!state.QuestionsById.TryGetValue(state.CurrentQuestionId, out QuestionWithAnswersDto question))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Current question not found for this match.");
            }

            return question;
        }

        private static bool EvaluateAnswerOrThrow(QuestionWithAnswersDto question, string answerText)
        {
            bool isTimeout = string.IsNullOrWhiteSpace(answerText);
            if (isTimeout)
            {
                return false;
            }

            AnswerDto selectedAnswer = question.Answers.Find(
                a => string.Equals(a.Text, answerText, StringComparison.Ordinal));

            if (selectedAnswer == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Answer not found for current question.");
            }

            return selectedAnswer.IsCorrect;
        }

        private static void UpdateChainState(MatchRuntimeState state, bool isCorrect)
        {
            if (isCorrect)
            {
                if (state.CurrentStreak < CHAIN_STEPS.Length)
                {
                    decimal increment = CHAIN_STEPS[state.CurrentStreak];
                    state.CurrentChain += increment;
                    state.CurrentStreak++;
                }

                return;
            }

            state.CurrentChain = 0m;
            state.CurrentStreak = 0;
        }

        private static AnswerResult BuildAnswerResult(int questionId, MatchRuntimeState state, bool isCorrect)
        {
            return new AnswerResult
            {
                QuestionId = questionId,
                IsCorrect = isCorrect,
                ChainIncrement = state.CurrentStreak > 0 && isCorrect
                    ? CHAIN_STEPS[state.CurrentStreak - 1]
                    : 0m,
                CurrentChain = state.CurrentChain,
                BankedPoints = state.BankedPoints
            };
        }

        private static PlayerSummary BuildPlayerSummary(MatchPlayerRuntime player, bool isOnline)
        {
            return new PlayerSummary
            {
                UserId = player.UserId,
                DisplayName = player.DisplayName,
                IsOnline = isOnline,
                Avatar = player.Avatar
            };
        }

        private static void Broadcast(MatchRuntimeState state, Action<IGameplayServiceCallback> action, string logContext)
        {
            foreach (MatchPlayerRuntime player in state.Players)
            {
                if (player == null || player.Callback == null)
                {
                    continue;
                }

                try
                {
                    action(player.Callback);
                }
                catch (Exception ex)
                {
                    Logger.WarnFormat(
                        "{0}: callback notification failed. PlayerUserId={1}",
                        logContext,
                        player.UserId);

                    Logger.Warn(logContext, ex);
                }
            }
        }

        private static int NextRandom(int minInclusive, int maxExclusive)
        {
            lock (randomSyncRoot)
            {
                return randomGenerator.Next(minInclusive, maxExclusive);
            }
        }

        private static int GetMaxQuestionsOrDefault(int? requested)
        {
            if (requested.HasValue && requested.Value > 0)
            {
                return requested.Value;
            }

            return DEFAULT_MAX_QUESTIONS;
        }

        private static void EnsureHostPlayerRegistered(MatchRuntimeState state, int userId)
        {
            if (state.Players.Count != 0)
            {
                return;
            }

            IGameplayServiceCallback callback =
                OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

            string displayName = string.Format("Jugador {0}", userId);

            UserAvatarSql avatarSql = new UserAvatarSql(GetConnectionString());
            UserAvatarEntity avatarEntity = avatarSql.GetByUserId(userId);
            AvatarAppearanceDto avatar = MapAvatar(avatarEntity);

            MatchPlayerRuntime player = new MatchPlayerRuntime(userId, displayName, callback)
            {
                Avatar = avatar
            };

            state.Players.Add(player);
            PlayerMatchByUserId[userId] = state.MatchId;
        }

        private static void ResetRoundStateForStart(MatchRuntimeState state)
        {
            state.CurrentPlayerIndex = 0;
            state.QuestionsAskedThisRound = 0;
            state.RoundNumber = 1;
            state.IsInVotePhase = false;
            state.IsInDuelPhase = false;
            state.WeakestRivalUserId = null;
            state.DuelTargetUserId = null;
            state.VotersThisRound.Clear();
            state.VotesThisRound.Clear();
            state.BombQuestionId = 0;
            state.HasSpecialEventThisRound = false;

            state.RestoreTurnAfterLightning();
            state.ResetLightningChallenge();
        }

        private static bool ShouldHandleDuelTurn(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            if (!state.IsInDuelPhase ||
                !state.WeakestRivalUserId.HasValue ||
                !state.DuelTargetUserId.HasValue)
            {
                return false;
            }

            return currentPlayer.UserId == state.WeakestRivalUserId.Value ||
                   currentPlayer.UserId == state.DuelTargetUserId.Value;
        }

        private static void HandleDuelTurn(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, bool isCorrect)
        {
            if (!isCorrect)
            {
                currentPlayer.IsEliminated = true;

                PlayerSummary eliminatedSummary = BuildPlayerSummary(currentPlayer, isOnline: true);

                Broadcast(
                    state,
                    cb => cb.OnElimination(state.MatchId, eliminatedSummary),
                    "GameplayService.DuelElimination");

                Logger.InfoFormat(
                    "Player eliminated by duel. MatchId={0}, Round={1}, EliminatedUserId={2}",
                    state.MatchId,
                    state.RoundNumber,
                    currentPlayer.UserId);

                state.IsInDuelPhase = false;
                state.WeakestRivalUserId = null;
                state.DuelTargetUserId = null;
                state.BombQuestionId = 0;

                FinishMatchWithWinnerIfApplicable(state);
                if (state.IsFinished)
                {
                    return;
                }

                StartNextRound(state);
                return;
            }

            int nextUserId = currentPlayer.UserId == state.WeakestRivalUserId.Value
                ? state.DuelTargetUserId.Value
                : state.WeakestRivalUserId.Value;

            int nextIndex = state.Players.FindIndex(
                p => p.UserId == nextUserId && !p.IsEliminated);

            if (nextIndex >= 0)
            {
                state.CurrentPlayerIndex = nextIndex;
                SendNextQuestion(state);
            }
        }

        private static int CountAlivePlayersOrFallbackToTotal(MatchRuntimeState state)
        {
            int alivePlayersCount = state.Players.Count(p => !p.IsEliminated);
            if (alivePlayersCount <= 0)
            {
                alivePlayersCount = state.Players.Count;
            }

            return alivePlayersCount;
        }

        private static void SendNextQuestion(MatchRuntimeState state)
        {
            if (state.Questions.Count == 0)
            {
                Logger.InfoFormat("No more questions in match. MatchId={0}", state.MatchId);
                return;
            }

            QuestionWithAnswersDto question = state.Questions.Dequeue();
            state.CurrentQuestionId = question.QuestionId;

            MatchPlayerRuntime targetPlayer = state.GetCurrentPlayer();
            if (targetPlayer == null)
            {
                Logger.WarnFormat("SendNextQuestion: no current player. MatchId={0}", state.MatchId);
                return;
            }

            state.BombQuestionId = 0;
            TryStartBombQuestionEvent(state, targetPlayer, question.QuestionId);

            PlayerSummary targetSummary = BuildPlayerSummary(targetPlayer, isOnline: true);

            Broadcast(
                state,
                cb => cb.OnNextQuestion(
                    state.MatchId,
                    targetSummary,
                    question,
                    state.CurrentChain,
                    state.BankedPoints),
                LOG_CTX_SEND_NEXT_QUESTION);
        }

        private static void StartVotePhase(MatchRuntimeState state)
        {
            state.IsInVotePhase = true;
            state.IsInDuelPhase = false;
            state.WeakestRivalUserId = null;
            state.DuelTargetUserId = null;
            state.VotersThisRound.Clear();
            state.VotesThisRound.Clear();
            state.BombQuestionId = 0;

            TimeSpan timeLimit = TimeSpan.FromSeconds(VOTE_PHASE_TIME_LIMIT_SECONDS);

            Broadcast(
                state,
                cb => cb.OnVotePhaseStarted(state.MatchId, timeLimit),
                LOG_CTX_VOTE_PHASE);
        }

        private static void ResolveEliminationOrStartDuel(MatchRuntimeState state)
        {
            state.IsInVotePhase = false;
            state.BombQuestionId = 0;

            List<MatchPlayerRuntime> alivePlayers = state.Players
                .Where(p => !p.IsEliminated)
                .ToList();

            if (alivePlayers.Count < MIN_PLAYERS_TO_CONTINUE)
            {
                FinishMatchWithWinnerIfApplicable(state);

                Logger.InfoFormat(
                    "Match finished. Not enough players to continue. MatchId={0}, AlivePlayers={1}",
                    state.MatchId,
                    alivePlayers.Count);

                return;
            }

            Dictionary<int, int> voteCounts = CountVotesForAlivePlayers(state, alivePlayers);

            if (voteCounts.Count == 0)
            {
                Logger.InfoFormat(
                    "No valid votes registered. No elimination this round. MatchId={0}, Round={1}",
                    state.MatchId,
                    state.RoundNumber);

                StartNextRound(state);
                return;
            }

            int weakestRivalUserId = ResolveWeakestRivalUserId(voteCounts, state);

            MatchPlayerRuntime weakestRivalPlayer = state.Players.FirstOrDefault(p => p.UserId == weakestRivalUserId);
            if (weakestRivalPlayer == null)
            {
                Logger.WarnFormat(
                    "ResolveEliminationOrStartDuel: weakest rival not found in players. MatchId={0}, WeakestRivalUserId={1}",
                    state.MatchId,
                    weakestRivalUserId);

                StartNextRound(state);
                return;
            }

            CoinFlipResolvedDto coinFlip = PerformCoinFlip(state, weakestRivalUserId);

            Broadcast(
                state,
                cb => cb.OnCoinFlipResolved(state.MatchId, coinFlip),
                "GameplayService.CoinFlip");

            if (!coinFlip.ShouldEnableDuel)
            {
                EliminatePlayerByVoteNoDuel(state, weakestRivalPlayer);
                return;
            }

            List<DuelCandidateDto> duelCandidates = BuildDuelCandidates(state, weakestRivalUserId);

            if (duelCandidates.Count == 0)
            {
                EliminatePlayerByVoteNoDuel(state, weakestRivalPlayer);
                return;
            }

            StartDuel(state, weakestRivalPlayer, weakestRivalUserId, duelCandidates);
        }

        private static Dictionary<int, int> CountVotesForAlivePlayers(MatchRuntimeState state, List<MatchPlayerRuntime> alivePlayers)
        {
            Dictionary<int, int> voteCounts = new Dictionary<int, int>();

            foreach (KeyValuePair<int, int?> kvp in state.VotesThisRound)
            {
                int? targetUserId = kvp.Value;
                if (!targetUserId.HasValue)
                {
                    continue;
                }

                if (!alivePlayers.Any(p => p.UserId == targetUserId.Value))
                {
                    continue;
                }

                if (!voteCounts.TryGetValue(targetUserId.Value, out int count))
                {
                    count = 0;
                }

                voteCounts[targetUserId.Value] = count + 1;
            }

            return voteCounts;
        }

        private static int ResolveWeakestRivalUserId(Dictionary<int, int> voteCounts, MatchRuntimeState state)
        {
            int maxVotes = voteCounts.Values.Max();

            List<int> candidates = voteCounts
                .Where(kvp => kvp.Value == maxVotes)
                .Select(kvp => kvp.Key)
                .ToList();

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            int randomIndex = NextRandom(0, candidates.Count);
            int weakestRivalUserId = candidates[randomIndex];

            Logger.InfoFormat(
                "Tie in votes. Random weakest rival selected. MatchId={0}, CandidatesCount={1}, WeakestRivalUserId={2}",
                state.MatchId,
                candidates.Count,
                weakestRivalUserId);

            return weakestRivalUserId;
        }

        private static List<QuestionWithAnswersDto> LoadQuestionsWithLocaleFallback(byte difficulty, string localeCode, int maxQuestions)
        {
            List<QuestionWithAnswersDto> questions = LoadQuestions(difficulty, localeCode, maxQuestions);
            if (questions.Count > 0)
            {
                return questions;
            }

            string languageOnly = ExtractLanguageCode(localeCode);
            if (!string.IsNullOrWhiteSpace(languageOnly) &&
                !string.Equals(languageOnly, localeCode, StringComparison.OrdinalIgnoreCase))
            {
                questions = LoadQuestions(difficulty, languageOnly, maxQuestions);
                if (questions.Count > 0)
                {
                    return questions;
                }
            }

            questions = LoadQuestions(difficulty, FALLBACK_LOCALE_EN_US, maxQuestions);
            return questions;
        }

        private static string ExtractLanguageCode(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return string.Empty;
            }

            string trimmed = localeCode.Trim();
            int dashIndex = trimmed.IndexOf('-');

            if (dashIndex <= 0)
            {
                return trimmed;
            }

            return trimmed.Substring(0, dashIndex);
        }

        private static void EliminatePlayerByVoteNoDuel(MatchRuntimeState state, MatchPlayerRuntime weakestRivalPlayer)
        {
            weakestRivalPlayer.IsEliminated = true;

            PlayerSummary eliminatedSummary = BuildPlayerSummary(weakestRivalPlayer, isOnline: true);

            Broadcast(
                state,
                cb => cb.OnElimination(state.MatchId, eliminatedSummary),
                "GameplayService.Elimination");

            Logger.InfoFormat(
                "Player eliminated by vote (no duel). MatchId={0}, Round={1}, EliminatedUserId={2}",
                state.MatchId,
                state.RoundNumber,
                weakestRivalPlayer.UserId);

            FinishMatchWithWinnerIfApplicable(state);
            if (state.IsFinished)
            {
                return;
            }

            StartNextRound(state);
        }

        private static List<DuelCandidateDto> BuildDuelCandidates(MatchRuntimeState state, int weakestRivalUserId)
        {
            List<int> votersAgainstWeakest = state.VotesThisRound
                .Where(kvp => kvp.Value.HasValue && kvp.Value.Value == weakestRivalUserId)
                .Select(kvp => kvp.Key)
                .ToList();

            List<DuelCandidateDto> duelCandidates = new List<DuelCandidateDto>();

            foreach (int voterUserId in votersAgainstWeakest)
            {
                MatchPlayerRuntime player = state.Players.FirstOrDefault(p => p.UserId == voterUserId);
                if (player == null || player.IsEliminated)
                {
                    continue;
                }

                duelCandidates.Add(
                    new DuelCandidateDto
                    {
                        UserId = player.UserId,
                        DisplayName = player.DisplayName,
                        Avatar = player.Avatar
                    });
            }

            return duelCandidates;
        }

        private static void StartDuel(
            MatchRuntimeState state,
            MatchPlayerRuntime weakestRivalPlayer,
            int weakestRivalUserId,
            List<DuelCandidateDto> duelCandidates)
        {
            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = weakestRivalUserId;
            state.DuelTargetUserId = null;
            state.BombQuestionId = 0;

            DuelCandidatesDto duelDto = new DuelCandidatesDto
            {
                WeakestRivalUserId = weakestRivalUserId,
                Candidates = duelCandidates.ToArray()
            };

            try
            {
                weakestRivalPlayer.Callback.OnDuelCandidates(state.MatchId, duelDto);
            }
            catch (Exception ex)
            {
                Logger.Warn("Error al notificar OnDuelCandidates al rival más débil.", ex);
            }

            Logger.InfoFormat(
                "Duel started. MatchId={0}, Round={1}, WeakestRivalUserId={2}, CandidatesCount={3}",
                state.MatchId,
                state.RoundNumber,
                weakestRivalUserId,
                duelCandidates.Count);
        }

        private static void StartNextRound(MatchRuntimeState state)
        {
            List<MatchPlayerRuntime> alivePlayers = state.Players
                .Where(p => !p.IsEliminated)
                .ToList();

            if (alivePlayers.Count < MIN_PLAYERS_TO_CONTINUE)
            {
                FinishMatchWithWinnerIfApplicable(state);

                Logger.InfoFormat(
                    "Match finished. Not enough players to continue in StartNextRound. MatchId={0}, AlivePlayers={1}",
                    state.MatchId,
                    alivePlayers.Count);

                return;
            }

            state.RoundNumber++;
            state.QuestionsAskedThisRound = 0;
            state.CurrentChain = 0m;
            state.CurrentStreak = 0;
            state.IsInVotePhase = false;
            state.IsInDuelPhase = false;
            state.WeakestRivalUserId = null;
            state.DuelTargetUserId = null;
            state.VotersThisRound.Clear();
            state.VotesThisRound.Clear();
            state.HasSpecialEventThisRound = false;
            state.BombQuestionId = 0;

            state.RestoreTurnAfterLightning();
            state.ResetLightningChallenge();

            if (state.CurrentPlayerIndex < 0 ||
                state.CurrentPlayerIndex >= state.Players.Count ||
                state.Players[state.CurrentPlayerIndex].IsEliminated)
            {
                int firstAlive = state.Players.FindIndex(p => !p.IsEliminated);
                if (firstAlive < 0)
                {
                    Logger.WarnFormat("StartNextRound: no alive players found. MatchId={0}", state.MatchId);
                    return;
                }

                state.CurrentPlayerIndex = firstAlive;
            }

            Logger.InfoFormat(
                "Starting next round. MatchId={0}, NewRound={1}, CurrentPlayerUserId={2}",
                state.MatchId,
                state.RoundNumber,
                state.Players[state.CurrentPlayerIndex].UserId);

            TryStartExtraWildcardEvent(state);

            bool hasLightningStarted = TryStartLightningChallenge(state);
            if (!hasLightningStarted)
            {
                SendNextQuestion(state);
            }
        }

        private static List<QuestionWithAnswersDto> LoadQuestions(byte difficulty, string localeCode, int maxQuestions)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                using (SqlCommand command = new SqlCommand(QuestionsSql.Text.LIST_QUESTIONS_WITH_ANSWERS, connection))
                {
                    command.CommandType = CommandType.Text;

                    command.Parameters.Add("@MaxQuestions", SqlDbType.Int).Value = maxQuestions;
                    command.Parameters.Add("@Difficulty", SqlDbType.TinyInt).Value = difficulty;
                    command.Parameters.Add("@LocaleCode", SqlDbType.NVarChar, 10).Value = localeCode;

                    connection.Open();

                    Dictionary<int, QuestionWithAnswersDto> questionsById =
                        new Dictionary<int, QuestionWithAnswersDto>();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int questionId = reader.GetInt32(0);

                            if (!questionsById.TryGetValue(questionId, out QuestionWithAnswersDto question))
                            {
                                question = new QuestionWithAnswersDto
                                {
                                    QuestionId = questionId,
                                    CategoryId = reader.GetInt32(1),
                                    Difficulty = reader.GetByte(2),
                                    LocaleCode = reader.GetString(3),
                                    Body = reader.GetString(4),
                                    Answers = new List<AnswerDto>()
                                };

                                questionsById.Add(questionId, question);
                            }

                            AnswerDto answer = new AnswerDto
                            {
                                AnswerId = reader.GetInt32(5),
                                Text = reader.GetString(6),
                                IsCorrect = reader.GetBoolean(7),
                                DisplayOrder = reader.GetByte(8)
                            };

                            question.Answers.Add(answer);
                        }
                    }

                    return new List<QuestionWithAnswersDto>(questionsById.Values);
                }
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "GameplayService.LoadQuestions",
                    ex);
            }
        }

        private static void ValidateGetQuestionsRequest(GetQuestionsRequest request)
        {
            ValidateNotNullRequest(request);

            if (request.Difficulty <= 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Difficulty must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.LocaleCode))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "LocaleCode is required.");
            }
        }

        private static string GetConnectionString()
        {
            ConnectionStringSettings configurationString =
                ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (configurationString == null || string.IsNullOrWhiteSpace(configurationString.ConnectionString))
            {
                Logger.ErrorFormat("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME);

                throw ThrowTechnicalFault(
                    "CONFIG_ERROR",
                    "Configuration error. Please contact support.",
                    "GameplayService.GetConnectionString",
                    new ConfigurationErrorsException(
                        string.Format("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME)));
            }

            return configurationString.ConnectionString;
        }

        private static CoinFlipResolvedDto PerformCoinFlip(MatchRuntimeState state, int weakestRivalUserId)
        {
            int randomValue = NextRandom(COIN_FLIP_RANDOM_MIN_VALUE, COIN_FLIP_RANDOM_MAX_VALUE);

            bool shouldEnableDuel = randomValue >= COIN_FLIP_THRESHOLD_VALUE;

            CoinFlipResultType result = shouldEnableDuel
                ? CoinFlipResultType.Heads
                : CoinFlipResultType.Tails;

            CoinFlipResolvedDto dto = new CoinFlipResolvedDto
            {
                RoundId = state.RoundNumber,
                WeakestRivalPlayerId = weakestRivalUserId,
                Result = result,
                ShouldEnableDuel = shouldEnableDuel
            };

            Logger.InfoFormat(
                "Coin flip resolved. MatchId={0}, Round={1}, WeakestRivalUserId={2}, Result={3}, ShouldEnableDuel={4}, RandomValue={5}",
                state.MatchId,
                state.RoundNumber,
                weakestRivalUserId,
                result,
                shouldEnableDuel,
                randomValue);

            return dto;
        }

        private static void FinishMatchWithWinnerIfApplicable(MatchRuntimeState state)
        {
            if (state == null || state.IsFinished)
            {
                return;
            }

            List<MatchPlayerRuntime> alivePlayers = state.Players
                .Where(p => !p.IsEliminated)
                .ToList();

            if (alivePlayers.Count != 1)
            {
                return;
            }

            MatchPlayerRuntime winner = alivePlayers[0];

            state.IsFinished = true;
            state.WinnerUserId = winner.UserId;
            winner.IsWinner = true;

            PlayerSummary winnerSummary = BuildPlayerSummary(winner, isOnline: true);

            Broadcast(
                state,
                cb => cb.OnMatchFinished(state.MatchId, winnerSummary),
                "GameplayService.MatchFinished");

            Logger.InfoFormat(
                "Match finished. MatchId={0}, WinnerUserId={1}",
                state.MatchId,
                winner.UserId);

            Matches.TryRemove(state.MatchId, out _);
            ExpectedPlayersByMatchId.TryRemove(state.MatchId, out _);

            foreach (MatchPlayerRuntime player in state.Players)
            {
                PlayerMatchByUserId.TryRemove(player.UserId, out _);
            }
        }

        private static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat("Service fault. Code='{0}', Message='{1}'", code, message);

            ServiceFault fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        private static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            ServiceFault fault = new ServiceFault
            {
                Code = code,
                Message = userMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }

        private static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ThrowFault("AUTH_REQUIRED", "Missing token.");
            }

            if (!TokenCache.TryGetValue(token, out AuthToken authToken))
            {
                throw ThrowFault("AUTH_INVALID", "Invalid token.");
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault("AUTH_EXPIRED", "Token expired.");
            }

            return authToken.UserId;
        }

        private static AvatarAppearanceDto MapAvatar(UserAvatarEntity entity)
        {
            if (entity == null)
            {
                return new AvatarAppearanceDto
                {
                    BodyColor = AvatarBodyColor.Blue,
                    PantsColor = AvatarPantsColor.Black,
                    HatType = AvatarHatType.None,
                    HatColor = AvatarHatColor.Default,
                    FaceType = AvatarFaceType.Default,
                    UseProfilePhotoAsFace = false
                };
            }

            return new AvatarAppearanceDto
            {
                BodyColor = (AvatarBodyColor)entity.BodyColor,
                PantsColor = (AvatarPantsColor)entity.PantsColor,
                HatType = (AvatarHatType)entity.HatType,
                HatColor = (AvatarHatColor)entity.HatColor,
                FaceType = (AvatarFaceType)entity.FaceType,
                UseProfilePhotoAsFace = entity.UseProfilePhoto
            };
        }

        private static bool IsLightningActive(MatchRuntimeState state)
        {
            return state != null &&
                   state.ActiveSpecialEvent == SpecialEventType.LightningChallenge &&
                   state.LightningChallenge != null;
        }

        private static bool TryStartBombQuestionEvent(MatchRuntimeState state, MatchPlayerRuntime targetPlayer, int questionId)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (targetPlayer == null)
            {
                throw new ArgumentNullException(nameof(targetPlayer));
            }

            if (questionId <= 0)
            {
                return false;
            }

            if (state.HasSpecialEventThisRound)
            {
                return false;
            }

            if (IsLightningActive(state))
            {
                return false;
            }

            int randomValue = NextRandom(BOMB_QUESTION_RANDOM_MIN_VALUE, BOMB_QUESTION_RANDOM_MAX_VALUE);
            if (randomValue >= BOMB_QUESTION_PROBABILITY_PERCENT)
            {
                return false;
            }

            state.BombQuestionId = questionId;
            state.HasSpecialEventThisRound = true;

            string deltaDisplay = BOMB_BANK_DELTA.ToString("0.00");
            string description = string.Format(
                SPECIAL_EVENT_BOMB_QUESTION_DESCRIPTION_TEMPLATE,
                targetPlayer.DisplayName,
                deltaDisplay);

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, SPECIAL_EVENT_BOMB_QUESTION_CODE, description),
                "GameplayService.OnSpecialEvent.BombQuestion");

            Logger.InfoFormat(
                "{0}: BombQuestion started. MatchId={1}, Round={2}, QuestionId={3}, TargetUserId={4}, RandomValue={5}",
                LOG_CTX_BOMB,
                state.MatchId,
                state.RoundNumber,
                questionId,
                targetPlayer.UserId,
                randomValue);

            return true;
        }

        private static void ApplyBombQuestionEffectIfNeeded(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, bool isCorrect)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (currentPlayer == null)
            {
                throw new ArgumentNullException(nameof(currentPlayer));
            }

            if (state.BombQuestionId <= 0 || state.BombQuestionId != state.CurrentQuestionId)
            {
                return;
            }

            decimal previousBank = state.BankedPoints;
            if (previousBank < MIN_BANKED_POINTS)
            {
                previousBank = MIN_BANKED_POINTS;
            }

            decimal delta = isCorrect ? BOMB_BANK_DELTA : -BOMB_BANK_DELTA;

            decimal updatedBank = previousBank + delta;
            if (updatedBank < MIN_BANKED_POINTS)
            {
                updatedBank = MIN_BANKED_POINTS;
            }

            state.BankedPoints = updatedBank;
            state.BombQuestionId = 0;

            string deltaDisplay = delta.ToString("+0.00;-0.00;0.00");
            string bankDisplay = state.BankedPoints.ToString("0.00");

            string description = string.Format(
                SPECIAL_EVENT_BOMB_QUESTION_APPLIED_DESCRIPTION_TEMPLATE,
                currentPlayer.DisplayName,
                deltaDisplay,
                bankDisplay);

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, SPECIAL_EVENT_BOMB_QUESTION_APPLIED_CODE, description),
                "GameplayService.OnSpecialEvent.BombQuestion.Applied");

            Logger.InfoFormat(
                "{0}: BombQuestion applied. MatchId={1}, UserId={2}, IsCorrect={3}, Bank {4} -> {5}, Delta={6}",
                LOG_CTX_BOMB,
                state.MatchId,
                currentPlayer.UserId,
                isCorrect,
                previousBank,
                state.BankedPoints,
                delta);
        }

        // ---------------------------------------------------------------
        // LIGHTNING
        // ---------------------------------------------------------------
        private static bool TryStartLightningChallenge(MatchRuntimeState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.HasSpecialEventThisRound)
            {
                Logger.InfoFormat(
                    "Skipping lightning challenge: special event already triggered this round. MatchId={0}, Round={1}",
                    state.MatchId,
                    state.RoundNumber);
                return false;
            }

            if (IsLightningActive(state))
            {
                return false;
            }

            if (state.Players.Count == 0 || state.Questions.Count < LIGHTNING_TOTAL_QUESTIONS)
            {
                return false;
            }

            int randomValue = NextRandom(LIGHTNING_RANDOM_MIN_VALUE, LIGHTNING_RANDOM_MAX_VALUE);
            if (randomValue >= LIGHTNING_PROBABILITY_PERCENT)
            {
                return false;
            }

            List<MatchPlayerRuntime> candidates = state.Players
                .Where(p => !p.IsEliminated)
                .ToList();

            if (candidates.Count == 0)
            {
                return false;
            }

            int candidateIndex = NextRandom(0, candidates.Count);
            MatchPlayerRuntime targetPlayer = candidates[candidateIndex];

            int targetPlayerIndex = state.Players.FindIndex(p => p.UserId == targetPlayer.UserId);
            if (targetPlayerIndex < 0)
            {
                return false;
            }

            List<QuestionWithAnswersDto> lightningQuestions = new List<QuestionWithAnswersDto>();

            if (state.Questions.Count < LIGHTNING_TOTAL_QUESTIONS)
            {
                return false;
            }

            for (int index = 0; index < LIGHTNING_TOTAL_QUESTIONS; index++)
            {
                QuestionWithAnswersDto question = state.Questions.Dequeue();
                lightningQuestions.Add(question);
            }

            state.OverrideTurnForLightning(targetPlayerIndex);

            state.ActiveSpecialEvent = SpecialEventType.LightningChallenge;
            state.HasSpecialEventThisRound = true;

            state.LightningChallenge = new LightningChallengeState(
                state.MatchId,
                Guid.NewGuid(),
                targetPlayer.UserId,
                LIGHTNING_TOTAL_QUESTIONS,
                TimeSpan.FromSeconds(LIGHTNING_TOTAL_TIME_SECONDS));

            state.SetLightningQuestions(lightningQuestions);

            PlayerSummary targetSummary = BuildPlayerSummary(targetPlayer, isOnline: true);

            Broadcast(
                state,
                cb => cb.OnLightningChallengeStarted(
                    state.MatchId,
                    state.LightningChallenge.RoundId,
                    targetSummary,
                    LIGHTNING_TOTAL_QUESTIONS,
                    LIGHTNING_TOTAL_TIME_SECONDS),
                "GameplayService.OnLightningChallengeStarted");

            QuestionWithAnswersDto firstQuestion = state.GetCurrentLightningQuestion();

            Broadcast(
                state,
                cb => cb.OnLightningChallengeQuestion(
                    state.MatchId,
                    state.LightningChallenge.RoundId,
                    1,
                    firstQuestion),
                "GameplayService.OnLightningChallengeQuestion");

            return true;
        }

        private static SubmitAnswerResponse HandleLightningSubmitAnswer(
            SubmitAnswerRequest request,
            MatchRuntimeState state,
            MatchPlayerRuntime currentPlayer)
        {
            LightningChallengeState challenge = state.LightningChallenge;

            AnswerResult fallbackResult = new AnswerResult
            {
                QuestionId = request.QuestionId,
                IsCorrect = false,
                ChainIncrement = 0m,
                CurrentChain = state.CurrentChain,
                BankedPoints = state.BankedPoints
            };

            if (challenge == null ||
                challenge.PlayerId != currentPlayer.UserId ||
                challenge.IsCompleted)
            {
                return new SubmitAnswerResponse
                {
                    Result = fallbackResult
                };
            }

            QuestionWithAnswersDto question = state.GetCurrentLightningQuestion();
            if (question == null)
            {
                return new SubmitAnswerResponse
                {
                    Result = fallbackResult
                };
            }

            bool isTimeout = string.IsNullOrWhiteSpace(request.AnswerText);
            bool isCorrect = false;

            if (!isTimeout)
            {
                AnswerDto selectedAnswer = question.Answers.Find(
                    a => string.Equals(a.Text, request.AnswerText, StringComparison.Ordinal));

                if (selectedAnswer != null)
                {
                    isCorrect = selectedAnswer.IsCorrect;
                }
            }

            if (isCorrect)
            {
                challenge.CorrectAnswers++;
            }

            challenge.RemainingQuestions--;

            AnswerResult result = new AnswerResult
            {
                QuestionId = question.QuestionId,
                IsCorrect = isCorrect,
                ChainIncrement = 0m,
                CurrentChain = state.CurrentChain,
                BankedPoints = state.BankedPoints
            };

            PlayerSummary playerSummary = BuildPlayerSummary(currentPlayer, isOnline: false);

            Broadcast(
                state,
                cb => cb.OnAnswerEvaluated(state.MatchId, playerSummary, result),
                "GameplayService.OnAnswerEvaluated.Lightning");

            if (challenge.RemainingQuestions <= 0)
            {
                bool isSuccess = challenge.CorrectAnswers == LIGHTNING_TOTAL_QUESTIONS;
                CompleteLightningChallenge(state, isSuccess);
            }
            else
            {
                state.MoveToNextLightningQuestion();

                QuestionWithAnswersDto nextQuestion = state.GetCurrentLightningQuestion();
                int questionIndex = LIGHTNING_TOTAL_QUESTIONS - challenge.RemainingQuestions;

                Broadcast(
                    state,
                    cb => cb.OnLightningChallengeQuestion(
                        state.MatchId,
                        challenge.RoundId,
                        questionIndex,
                        nextQuestion),
                    "GameplayService.OnLightningChallengeQuestion.Next");
            }

            return new SubmitAnswerResponse
            {
                Result = result
            };
        }

        private static void CompleteLightningChallenge(MatchRuntimeState state, bool isSuccess)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LightningChallengeState challenge = state.LightningChallenge;
            if (challenge == null)
            {
                return;
            }

            challenge.IsCompleted = true;
            challenge.IsSuccess = isSuccess;

            Broadcast(
                state,
                cb => cb.OnLightningChallengeFinished(
                    state.MatchId,
                    challenge.RoundId,
                    challenge.CorrectAnswers,
                    isSuccess),
                "GameplayService.OnLightningChallengeFinished");

            if (isSuccess)
            {
                TryAwardLightningWildcard(state, challenge.PlayerId);
            }

            state.RestoreTurnAfterLightning();
            state.ResetLightningChallenge();

            SendNextQuestion(state);
        }

        private static void AwardWildcard(
            MatchRuntimeState state,
            int playerUserId,
            string logContext,
            string specialEventCode,
            string descriptionTemplate)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            MatchPlayerRuntime targetPlayer = state.Players.FirstOrDefault(p => p.UserId == playerUserId);
            if (targetPlayer == null)
            {
                Logger.WarnFormat("{0}: player not found. MatchId={1}, UserId={2}", logContext, state.MatchId, playerUserId);
                return;
            }

            try
            {
                if (state.WildcardMatchId > 0)
                {
                    WildcardService.GrantLightningWildcard(state.WildcardMatchId, playerUserId);

                    Logger.InfoFormat(
                        "{0}: wildcard GRANTED. MatchDbId={1}, UserId={2}",
                        logContext,
                        state.WildcardMatchId,
                        playerUserId);
                }
                else
                {
                    Logger.ErrorFormat(
                        "{0}: wildcard NOT GRANTED: WildcardMatchId no asignado. MatchGuid={1}",
                        logContext,
                        state.MatchId);
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat("{0}: error otorgando comodín en BD", logContext);
                Logger.Error(logContext, ex);
            }

            string description = string.Format(descriptionTemplate, targetPlayer.DisplayName);

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, specialEventCode, description),
                "GameplayService.OnSpecialEvent");
        }

        private static void TryAwardLightningWildcard(MatchRuntimeState state, int playerUserId)
        {
            AwardWildcard(
                state,
                playerUserId,
                "LightningWildcard",
                SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE,
                SPECIAL_EVENT_LIGHTNING_WILDCARD_DESCRIPTION_TEMPLATE);
        }

        private static void TryAwardExtraWildcard(MatchRuntimeState state, int playerUserId)
        {
            AwardWildcard(
                state,
                playerUserId,
                "ExtraWildcard",
                SPECIAL_EVENT_EXTRA_WILDCARD_CODE,
                SPECIAL_EVENT_EXTRA_WILDCARD_DESCRIPTION_TEMPLATE);
        }

        private static bool TryStartExtraWildcardEvent(MatchRuntimeState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.HasSpecialEventThisRound)
            {
                return false;
            }

            if (IsLightningActive(state))
            {
                return false;
            }

            List<MatchPlayerRuntime> candidates = state.Players
                .Where(p => !p.IsEliminated)
                .ToList();

            if (candidates.Count == 0)
            {
                return false;
            }

            int probabilityValue = NextRandom(EXTRA_WILDCARD_RANDOM_MIN_VALUE, EXTRA_WILDCARD_RANDOM_MAX_VALUE);
            if (probabilityValue >= EXTRA_WILDCARD_PROBABILITY_PERCENT)
            {
                return false;
            }

            int candidateIndex = NextRandom(0, candidates.Count);
            MatchPlayerRuntime targetPlayer = candidates[candidateIndex];

            Logger.InfoFormat(
                "Extra wildcard event started. MatchId={0}, Round={1}, TargetUserId={2}",
                state.MatchId,
                state.RoundNumber,
                targetPlayer.UserId);

            TryAwardExtraWildcard(state, targetPlayer.UserId);
            state.HasSpecialEventThisRound = true;

            return true;
        }
    }
}
