using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using TheWeakestRival.Contracts.Enums;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class GameplayEngine
    {
        internal static readonly GameplayEngine Shared = new GameplayEngine();

        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayEngine));

        internal const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        internal const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        internal const string ERROR_DB = "DB_ERROR";
        internal const string ERROR_UNEXPECTED = "UNEXPECTED_ERROR";
        internal const string ERROR_MATCH_NOT_FOUND = "MATCH_NOT_FOUND";
        internal const string ERROR_NOT_PLAYER_TURN = "NOT_PLAYER_TURN";
        internal const string ERROR_DUEL_NOT_ACTIVE = "DUEL_NOT_ACTIVE";
        internal const string ERROR_NOT_WEAKEST_RIVAL = "NOT_WEAKEST_RIVAL";
        internal const string ERROR_INVALID_DUEL_TARGET = "INVALID_DUEL_TARGET";
        internal const string ERROR_MATCH_ALREADY_STARTED = "MATCH_ALREADY_STARTED";
        internal const string ERROR_NO_QUESTIONS = "NO_QUESTIONS";

        internal const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";

        internal const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        private const string FALLBACK_LOCALE_EN_US = "en-US";

        private const string ERROR_MATCH_ALREADY_STARTED_MESSAGE =
            "Match already started. Joining is not allowed.";

        private const string ERROR_MATCH_NOT_FOUND_MESSAGE = "Match not found.";
        private const string ERROR_NOT_PLAYER_TURN_MESSAGE = "It is not the player turn.";
        private const string ERROR_DUEL_NOT_ACTIVE_MESSAGE = "Duel is not active.";
        private const string ERROR_NOT_WEAKEST_RIVAL_MESSAGE = "Only weakest rival can choose duel opponent.";
        private const string ERROR_INVALID_DUEL_TARGET_MESSAGE = "Invalid duel opponent.";
        private const string ERROR_NO_QUESTIONS_MESSAGE =
            "No se encontraron preguntas para la dificultad/idioma solicitados.";

        private const int DEFAULT_MAX_QUESTIONS = 40;
        private const int QUESTIONS_PER_PLAYER_PER_ROUND = 2;
        private const int VOTE_PHASE_TIME_LIMIT_SECONDS = 30;
        private const int MIN_PLAYERS_TO_CONTINUE = 2;

        private const int COIN_FLIP_RANDOM_MIN_VALUE = 0;
        private const int COIN_FLIP_RANDOM_MAX_VALUE = 100;
        private const int COIN_FLIP_THRESHOLD_VALUE = 50;

        private const int LIGHTNING_PROBABILITY_PERCENT = 20;
        private const int LIGHTNING_TOTAL_QUESTIONS = 3;
        private const int LIGHTNING_TOTAL_TIME_SECONDS = 30;
        private const int LIGHTNING_RANDOM_MIN_VALUE = 0;
        private const int LIGHTNING_RANDOM_MAX_VALUE = 100;

        private const int EXTRA_WILDCARD_RANDOM_MIN_VALUE = 0;
        private const int EXTRA_WILDCARD_RANDOM_MAX_VALUE = 100;
        private const int EXTRA_WILDCARD_PROBABILITY_PERCENT = 20;

        private const int BOMB_QUESTION_RANDOM_MIN_VALUE = 0;
        private const int BOMB_QUESTION_RANDOM_MAX_VALUE = 100;
        private const int BOMB_QUESTION_PROBABILITY_PERCENT = 20;

        private const int SURPRISE_EXAM_RANDOM_MIN_VALUE = 0;
        private const int SURPRISE_EXAM_RANDOM_MAX_VALUE = 100;
        private const int SURPRISE_EXAM_PROBABILITY_PERCENT = 20;

        private const int SURPRISE_EXAM_TIME_LIMIT_SECONDS = 20;

        private const decimal SURPRISE_EXAM_SUCCESS_BONUS = 2.00m;
        private const decimal SURPRISE_EXAM_FAILURE_PENALTY = 3.00m;

        private const string SURPRISE_EXAM_RESOLVE_REASON_TIMEOUT = "TIMEOUT";
        private const string SURPRISE_EXAM_RESOLVE_REASON_ALL_ANSWERED = "ALL_ANSWERED";

        private const string SURPRISE_EXAM_BANKING_NOT_ALLOWED_MESSAGE =
            "Special event in progress. Banking is not allowed.";

        private const decimal BOMB_BANK_DELTA = 0.50m;
        private const decimal MIN_BANKED_POINTS = 0.00m;
        private const decimal INITIAL_BANKED_POINTS = 5.00m;

        private const int TURN_USER_ID_NONE = 0;

        private const string SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE = "LIGHTNING_WILDCARD_AWARDED";
        private const string SPECIAL_EVENT_LIGHTNING_WILDCARD_DESCRIPTION_TEMPLATE =
            "El jugador {0} ha ganado un comodín relámpago.";

        private const string SPECIAL_EVENT_EXTRA_WILDCARD_CODE = "EXTRA_WILDCARD_AWARDED";
        private const string SPECIAL_EVENT_EXTRA_WILDCARD_DESCRIPTION_TEMPLATE =
            "El jugador {0} ha recibido un comodín extra.";

        private const string SPECIAL_EVENT_BOMB_QUESTION_CODE = "BOMB_QUESTION";
        private const string SPECIAL_EVENT_BOMB_QUESTION_DESCRIPTION_TEMPLATE =
            "Pregunta bomba para {0}. Acierto: +{1} a la banca. Fallo: -{1} de lo bancado.";

        private const string SPECIAL_EVENT_BOMB_QUESTION_APPLIED_CODE = "BOMB_QUESTION_APPLIED";
        private const string SPECIAL_EVENT_BOMB_QUESTION_APPLIED_DESCRIPTION_TEMPLATE =
            "Pregunta bomba resuelta por {0}. Cambio en banca: {1}. Banca actual: {2}.";

        private const string SPECIAL_EVENT_SURPRISE_EXAM_STARTED_CODE = "SURPRISE_EXAM_STARTED";
        private const string SPECIAL_EVENT_SURPRISE_EXAM_STARTED_DESCRIPTION =
            "¡Examen sorpresa! Cada jugador debe responder su pregunta.";

        private const string SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_CODE = "SURPRISE_EXAM_RESOLVED";
        private const string SPECIAL_EVENT_SURPRISE_EXAM_OUTCOME_ALL_CORRECT = "Todos acertaron";
        private const string SPECIAL_EVENT_SURPRISE_EXAM_OUTCOME_SOME_FAILED = "Al menos uno falló";

        private const string SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_DESCRIPTION_TEMPLATE =
            "Examen sorpresa resuelto. {0} ({1}/{2}). Cambio en banca: {3}. Banca actual: {4}.";

        private const int DARK_MODE_RANDOM_MIN_VALUE = 0;
        private const int DARK_MODE_RANDOM_MAX_VALUE = 100;
        private const int DARK_MODE_PROBABILITY_PERCENT = 20;

        private const string SPECIAL_EVENT_DARK_MODE_STARTED_CODE = "DARK_MODE_STARTED";
        private const string SPECIAL_EVENT_DARK_MODE_STARTED_DESCRIPTION =
            "¡A oscuras! Los lugares se han revuelto y la identidad de los jugadores está oculta.";

        private const string SPECIAL_EVENT_DARK_MODE_ENDED_CODE = "DARK_MODE_ENDED";
        private const string SPECIAL_EVENT_DARK_MODE_ENDED_DESCRIPTION =
            "Las luces vuelven. Ahora puedes ver por quién votaste.";

        private const string SPECIAL_EVENT_DARK_MODE_VOTE_REVEAL_CODE = "DARK_MODE_VOTE_REVEAL";
        private const string SPECIAL_EVENT_DARK_MODE_VOTE_REVEAL_DESCRIPTION_TEMPLATE =
            "Votaste por {0}.";

        private const string DARK_MODE_NO_VOTE_DISPLAY_NAME = "Nadie";
        private const string DARK_MODE_FALLBACK_PLAYER_NAME_TEMPLATE = "Jugador {0}";

        private const string WILDCARD_CHANGE_Q = "CHANGE_Q";
        private const string WILDCARD_PASS_Q = "PASS_Q";
        private const string WILDCARD_SHIELD = "SHIELD";
        private const string WILDCARD_FORCED_BANK = "FORCED_BANK";
        private const string WILDCARD_DOUBLE = "DOUBLE";
        private const string WILDCARD_BLOCK = "BLOCK";
        private const string WILDCARD_SWAP = "SWAP";
        private const string WILDCARD_SABOTAGE = "SABOTAGE";
        private const string WILDCARD_EXTRA_TIME = "EXTRA_TIME";

        private const string TURN_REASON_TIME_DELTA_PREFIX = "TIME_DELTA:";
        private const string TURN_REASON_WILDCARD_SWAP = "WILDCARD_SWAP";
        private const string TURN_REASON_WILDCARD_PASS_Q = "WILDCARD_PASS_Q";

        private const int WILDCARD_TIME_BONUS_SECONDS = 5;
        private const int WILDCARD_TIME_PENALTY_SECONDS = 5;

        private const string SPECIAL_EVENT_WILDCARD_USED_CODE_TEMPLATE = "WILDCARD_USED_{0}";
        private const string SPECIAL_EVENT_WILDCARD_USED_DESCRIPTION_TEMPLATE = "{0} usó el comodín {1}.";

        private const string SPECIAL_EVENT_SHIELD_TRIGGERED_CODE = "WILDCARD_SHIELD_TRIGGERED";
        private const string SPECIAL_EVENT_SHIELD_TRIGGERED_DESCRIPTION_TEMPLATE =
            "El escudo de {0} evitó su eliminación.";

        private const string SPECIAL_EVENT_TIME_BONUS_CODE = "WILDCARD_EXTRA_TIME";
        private const string SPECIAL_EVENT_TIME_BONUS_DESCRIPTION_TEMPLATE =
            "{0} obtuvo +{1} segundos.";

        private const string SPECIAL_EVENT_TIME_PENALTY_CODE = "WILDCARD_SABOTAGE";
        private const string SPECIAL_EVENT_TIME_PENALTY_DESCRIPTION_TEMPLATE =
            "{0} tendrá -{1} segundos.";

        private const string ERROR_WILDCARD_INVALID_TIMING = "WILDCARD_INVALID_TIMING";
        private const string ERROR_WILDCARD_INVALID_TIMING_MESSAGE =
            "No puedes usar comodines en este momento.";

        private const string ERROR_WILDCARDS_BLOCKED = "WILDCARDS_BLOCKED";
        private const string ERROR_WILDCARDS_BLOCKED_MESSAGE =
            "Tus comodines están bloqueados por esta ronda.";

        private const string ERROR_INVALID_ROUND = "INVALID_ROUND";
        private const string ERROR_INVALID_ROUND_MESSAGE =
            "La ronda del cliente no coincide con la del servidor.";

        private const int LOCALE_CODE_MAX_LENGTH = 10;

        private static readonly decimal[] CHAIN_STEPS =
        {
            0.10m,
            0.20m,
            0.30m,
            0.40m,
            0.50m
        };

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private static readonly ConcurrentDictionary<Guid, MatchRuntimeState> Matches =
            new ConcurrentDictionary<Guid, MatchRuntimeState>();

        private static readonly ConcurrentDictionary<int, Guid> RuntimeMatchByWildcardMatchId =
            new ConcurrentDictionary<int, Guid>();

        private static readonly ConcurrentDictionary<int, Guid> PlayerMatchByUserId =
            new ConcurrentDictionary<int, Guid>();

        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>> ExpectedPlayersByMatchId =
            new ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>>();

        private static readonly object RandomSyncRoot = new object();
        private static readonly Random RandomGenerator = new Random();

        private GameplayEngine()
        {
        }

        internal GetQuestionsResponse GetQuestions(GetQuestionsRequest request)
        {
            ValidateGetQuestionsRequest(request);

            Authenticate(request.Token);

            int maxQuestions = GetMaxQuestionsOrDefault(request.MaxQuestions);

            try
            {
                List<QuestionWithAnswersDto> questions =
                    LoadQuestions(request.Difficulty, request.LocaleCode, maxQuestions);

                return new GetQuestionsResponse
                {
                    Questions = questions ?? new List<QuestionWithAnswersDto>()
                };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(ERROR_DB, MESSAGE_DB_ERROR, "GameplayEngine.GetQuestions", ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(ERROR_UNEXPECTED, MESSAGE_UNEXPECTED_ERROR, "GameplayEngine.GetQuestions", ex);
            }
        }

        internal MatchRuntimeState GetOrCreateMatch(Guid matchId)
        {
            return Matches.GetOrAdd(matchId, id => new MatchRuntimeState(id));
        }

        internal MatchRuntimeState GetMatchOrThrow(Guid matchId)
        {
            if (!Matches.TryGetValue(matchId, out MatchRuntimeState state))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            return state;
        }

        internal MatchRuntimeState GetMatchByWildcardDbIdOrThrow(int wildcardMatchId)
        {
            if (wildcardMatchId <= 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "WildcardMatchId inválido.");
            }

            if (RuntimeMatchByWildcardMatchId.TryGetValue(wildcardMatchId, out Guid runtimeMatchId))
            {
                return GetMatchOrThrow(runtimeMatchId);
            }

            MatchRuntimeState fallback = Matches.Values.FirstOrDefault(
                s => s != null && s.WildcardMatchId == wildcardMatchId);

            if (fallback == null)
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            RuntimeMatchByWildcardMatchId.TryAdd(wildcardMatchId, fallback.MatchId);
            return fallback;
        }

        internal Guid ResolveMatchIdForUserOrThrow(int userId)
        {
            if (!PlayerMatchByUserId.TryGetValue(userId, out Guid matchId))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            return matchId;
        }

        internal void JoinMatchInternal(
            MatchRuntimeState state,
            Guid matchId,
            int userId,
            IGameplayServiceCallback callback)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (callback == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Missing callback channel.");
            }

            lock (state.SyncRoot)
            {
                bool hasStarted = state.HasStarted || state.IsInitialized;

                if (hasStarted)
                {
                    if (ExpectedPlayersByMatchId.TryGetValue(matchId, out ConcurrentDictionary<int, byte> expectedPlayers) &&
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
                        MatchPlayerRuntime existingRuntime = state.Players.Find(p => p.UserId == userId);
                        if (existingRuntime == null)
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
                    string displayName = string.Format(DARK_MODE_FALLBACK_PLAYER_NAME_TEMPLATE, userId);

                    UserAvatarEntity avatarEntity =
                        new UserAvatarSql(GetConnectionString()).GetByUserId(userId);

                    MatchPlayerRuntime player = new MatchPlayerRuntime(userId, displayName, callback)
                    {
                        Avatar = MapAvatar(avatarEntity)
                    };

                    state.Players.Add(player);
                }

                if (hasStarted && state.IsInitialized)
                {
                    TrySendSnapshotToJoiningPlayer(state, userId);
                }
            }

            PlayerMatchByUserId[userId] = matchId;
        }

        internal void StartMatchInternal(MatchRuntimeState state, GameplayStartMatchRequest request, int hostUserId)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            int maxQuestions = GetMaxQuestionsOrDefault(request.MaxQuestions);

            try
            {
                lock (state.SyncRoot)
                {
                    if (state.HasStarted || state.IsInitialized)
                    {
                        StoreOrMergeExpectedPlayers(request.MatchId, request.ExpectedPlayerUserIds, hostUserId);
                        return;
                    }

                    state.HasStarted = true;

                    try
                    {
                        StoreOrMergeExpectedPlayers(request.MatchId, request.ExpectedPlayerUserIds, hostUserId);

                        List<QuestionWithAnswersDto> questions = LoadQuestionsWithLocaleFallback(
                            request.Difficulty,
                            request.LocaleCode,
                            maxQuestions);

                        if (questions == null || questions.Count == 0)
                        {
                            throw ThrowFault(ERROR_NO_QUESTIONS, ERROR_NO_QUESTIONS_MESSAGE);
                        }

                        InitializeMatchState(state, request, hostUserId, questions);

                        TryStartDarkModeEvent(state);

                        TryStartExtraWildcardEvent(state);

                        if (TryStartSurpriseExamEvent(state))
                        {
                            return;
                        }

                        bool hasLightningStarted = TryStartLightningChallenge(state);
                        if (!hasLightningStarted)
                        {
                            SendNextQuestion(state);
                        }
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
                throw ThrowTechnicalFault(ERROR_DB, MESSAGE_DB_ERROR, "GameplayEngine.StartMatchInternal", ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(ERROR_UNEXPECTED, MESSAGE_UNEXPECTED_ERROR, "GameplayEngine.StartMatchInternal", ex);
            }
        }

        internal void InitializeMatchState(
            MatchRuntimeState state,
            GameplayStartMatchRequest request,
            int hostUserId,
            List<QuestionWithAnswersDto> questions)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (questions == null)
            {
                throw new ArgumentNullException(nameof(questions));
            }

            state.Initialize(
                request.Difficulty,
                request.LocaleCode,
                questions,
                INITIAL_BANKED_POINTS);

            state.WildcardMatchId = request.MatchDbId;

            if (state.WildcardMatchId > 0)
            {
                RuntimeMatchByWildcardMatchId[state.WildcardMatchId] = state.MatchId;
            }

            EnsureHostPlayerRegistered(state, hostUserId);

            ResetRoundStateForStart(state);

            ShufflePlayersForStart(state);

            BroadcastTurnOrderInitialized(state);
        }

        internal void ChooseDuelOpponentInternal(MatchRuntimeState state, int userId, int targetUserId)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

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

                if (!alivePlayers.Contains(targetUserId))
                {
                    throw ThrowFault(ERROR_INVALID_DUEL_TARGET, ERROR_INVALID_DUEL_TARGET_MESSAGE);
                }

                HashSet<int> votersAgainstWeakest = state.VotesThisRound
                    .Where(kvp => kvp.Value.HasValue && kvp.Value.Value == state.WeakestRivalUserId.Value)
                    .Select(kvp => kvp.Key)
                    .ToHashSet();

                if (!votersAgainstWeakest.Contains(targetUserId))
                {
                    throw ThrowFault(ERROR_INVALID_DUEL_TARGET, ERROR_INVALID_DUEL_TARGET_MESSAGE);
                }

                state.DuelTargetUserId = targetUserId;

                int weakestIndex = state.Players.FindIndex(
                    p => p.UserId == state.WeakestRivalUserId.Value && !p.IsEliminated);

                if (weakestIndex >= 0)
                {
                    state.CurrentPlayerIndex = weakestIndex;
                    SendNextQuestion(state);
                }
            }
        }

        internal AnswerResult SubmitAnswerInternal(MatchRuntimeState state, int userId, SubmitAnswerRequest request)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            lock (state.SyncRoot)
            {
                EnsureNotInVotePhase(state, "Round is in vote phase. No questions available.");

                if (state.IsSurpriseExamActive)
                {
                    return HandleSurpriseExamSubmitAnswer(state, userId, request);
                }

                MatchPlayerRuntime currentPlayer = GetCurrentPlayerOrThrow(state, userId);

                if (IsLightningActive(state))
                {
                    return HandleLightningSubmitAnswer(state, currentPlayer, request);
                }

                QuestionWithAnswersDto question = GetCurrentQuestionOrThrow(state);
                bool isCorrect = EvaluateAnswerOrThrow(question, request.AnswerText);

                decimal chainIncrement = UpdateChainState(state, currentPlayer, isCorrect);
                ApplyBombQuestionEffectIfNeeded(state, currentPlayer, isCorrect);

                AnswerResult result = BuildAnswerResult(question.QuestionId, state, isCorrect, chainIncrement);

                Broadcast(
                    state,
                    cb => cb.OnAnswerEvaluated(
                        state.MatchId,
                        BuildPlayerSummary(currentPlayer, isOnline: true),
                        result),
                    "GameplayEngine.SubmitAnswerInternal");

                if (ShouldHandleDuelTurn(state, currentPlayer))
                {
                    HandleDuelTurn(state, currentPlayer, isCorrect);
                    return result;
                }

                state.QuestionsAskedThisRound++;

                int alivePlayersCount = CountAlivePlayersOrFallbackToTotal(state);
                int maxQuestionsThisRound = alivePlayersCount * QUESTIONS_PER_PLAYER_PER_ROUND;
                bool hasNoMoreQuestions = state.Questions.Count == 0;

                if (state.QuestionsAskedThisRound >= maxQuestionsThisRound || hasNoMoreQuestions)
                {
                    StartVotePhase(state);
                    return result;
                }

                state.AdvanceTurn();
                SendNextQuestion(state);

                return result;
            }
        }

        internal BankState BankInternal(MatchRuntimeState state, int userId)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            lock (state.SyncRoot)
            {
                EnsureNotInVotePhase(state, "Round is in vote phase. Banking is not allowed.");

                if (state.IsSurpriseExamActive)
                {
                    throw ThrowFault(ERROR_INVALID_REQUEST, SURPRISE_EXAM_BANKING_NOT_ALLOWED_MESSAGE);
                }

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
                    "GameplayEngine.BankInternal");

                return bankState;
            }
        }

        internal bool CastVoteInternal(MatchRuntimeState state, int userId, int? targetUserId)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

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

                if (targetUserId.HasValue && !alivePlayers.Contains(targetUserId.Value))
                {
                    throw ThrowFault(ERROR_INVALID_REQUEST, "Target player is not in match or already eliminated.");
                }

                state.VotesThisRound[userId] = targetUserId;
                state.VotersThisRound.Add(userId);

                int alivePlayersCount = alivePlayers.Count;
                if (alivePlayersCount <= 0)
                {
                    alivePlayersCount = state.Players.Count;
                }

                if (state.VotersThisRound.Count >= alivePlayersCount)
                {
                    ResolveEliminationOrStartDuel(state);
                }

                return true;
            }
        }

        internal int ApplyWildcardFromDbOrThrow(int wildcardMatchId, int userId, string wildcardCode, int clientRoundNumber)
        {
            MatchRuntimeState state = GetMatchByWildcardDbIdOrThrow(wildcardMatchId);

            lock (state.SyncRoot)
            {
                int serverRoundNumber = state.RoundNumber;

                if (clientRoundNumber != serverRoundNumber)
                {
                    Logger.WarnFormat(
                        "ApplyWildcardFromDbOrThrow: round mismatch. MatchId={0}, UserId={1}, Code={2}, ClientRound={3}, ServerRound={4}. Using server round.",
                        wildcardMatchId,
                        userId,
                        wildcardCode ?? string.Empty,
                        clientRoundNumber,
                        serverRoundNumber);

                    clientRoundNumber = serverRoundNumber;
                }

                if (state.IsInVotePhase || state.IsInDuelPhase || state.IsSurpriseExamActive || IsLightningActive(state))
                {
                    throw ThrowFault(ERROR_WILDCARD_INVALID_TIMING, ERROR_WILDCARD_INVALID_TIMING_MESSAGE);
                }

                MatchPlayerRuntime actor = state.Players.FirstOrDefault(p => p != null && p.UserId == userId);
                if (actor == null || actor.IsEliminated)
                {
                    throw ThrowFault(ERROR_INVALID_REQUEST, "Player not in match or eliminated.");
                }

                if (actor.BlockWildcardsRoundNumber == state.RoundNumber)
                {
                    throw ThrowFault(ERROR_WILDCARDS_BLOCKED, ERROR_WILDCARDS_BLOCKED_MESSAGE);
                }

                MatchPlayerRuntime current = GetCurrentPlayerOrThrow(state, userId);

                string normalizedCode = (wildcardCode ?? string.Empty).Trim().ToUpperInvariant();
                ApplyWildcardLocked(state, current, normalizedCode);

                BroadcastWildcardUsed(state, current, normalizedCode);

                return state.RoundNumber;
            }
        }


        private static void ApplyWildcardLocked(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, string wildcardCode)
        {
            switch (wildcardCode)
            {
                case WILDCARD_CHANGE_Q:
                    ApplyWildcardChangeQuestion(state, currentPlayer);
                    return;

                case WILDCARD_PASS_Q:
                    ApplyWildcardPassQuestion(state, currentPlayer);
                    return;

                case WILDCARD_SHIELD:
                    ApplyWildcardShield(currentPlayer);
                    return;

                case WILDCARD_FORCED_BANK:
                    ApplyWildcardForcedBank(state);
                    return;

                case WILDCARD_DOUBLE:
                    ApplyWildcardDouble(currentPlayer);
                    return;

                case WILDCARD_BLOCK:
                    ApplyWildcardBlock(state, currentPlayer);
                    return;

                case WILDCARD_SWAP:
                    ApplyWildcardSwap(state, currentPlayer);
                    return;

                case WILDCARD_SABOTAGE:
                    ApplyWildcardSabotage(state, currentPlayer);
                    return;

                case WILDCARD_EXTRA_TIME:
                    ApplyWildcardExtraTime(state, currentPlayer);
                    return;

                default:
                    throw ThrowFault(ERROR_INVALID_REQUEST, "Código de comodín inválido.");
            }
        }

        private static void ApplyWildcardChangeQuestion(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            if (state.Questions.Count <= 0)
            {
                throw ThrowFault(ERROR_NO_QUESTIONS, ERROR_NO_QUESTIONS_MESSAGE);
            }

            QuestionWithAnswersDto nextQuestion = state.Questions.Dequeue();
            state.CurrentQuestionId = nextQuestion.QuestionId;

            Broadcast(
                state,
                cb => cb.OnNextQuestion(
                    state.MatchId,
                    BuildPlayerSummary(currentPlayer, isOnline: true),
                    nextQuestion,
                    state.CurrentChain,
                    state.BankedPoints),
                "GameplayEngine.Wildcard.ChangeQuestion");
        }

        private static void ApplyWildcardPassQuestion(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            MatchPlayerRuntime target = ResolveNextAlivePlayerOrThrow(state, currentPlayer.UserId);

            int targetIndex = state.Players.FindIndex(p => p != null && p.UserId == target.UserId);
            if (targetIndex < 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Target inválido para PASS_Q.");
            }

            state.CurrentPlayerIndex = targetIndex;

            BroadcastTurnOrderInitialized(state);

            if (!state.QuestionsById.TryGetValue(state.CurrentQuestionId, out QuestionWithAnswersDto currentQuestion))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Current question not found for PASS_Q.");
            }

            Broadcast(
                state,
                cb => cb.OnNextQuestion(
                    state.MatchId,
                    BuildPlayerSummary(target, isOnline: true),
                    currentQuestion,
                    state.CurrentChain,
                    state.BankedPoints),
                "GameplayEngine.Wildcard.PassQuestion");
        }

        private static void ApplyWildcardShield(MatchPlayerRuntime currentPlayer)
        {
            currentPlayer.IsShieldActive = true;
        }

        private static void ApplyWildcardForcedBank(MatchRuntimeState state)
        {
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
                "GameplayEngine.Wildcard.ForcedBank");
        }

        private static void ApplyWildcardDouble(MatchPlayerRuntime currentPlayer)
        {
            currentPlayer.IsDoublePointsActive = true;
        }

        private static void ApplyWildcardBlock(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            MatchPlayerRuntime target = ResolveNextAlivePlayerOrThrow(state, currentPlayer.UserId);
            target.BlockWildcardsRoundNumber = state.RoundNumber;
        }

        private static void ApplyWildcardSwap(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            MatchPlayerRuntime target = ResolveNextAlivePlayerOrThrow(state, currentPlayer.UserId);

            int currentIndex = state.Players.FindIndex(p => p.UserId == currentPlayer.UserId);
            int targetIndex = state.Players.FindIndex(p => p.UserId == target.UserId);

            if (currentIndex < 0 || targetIndex < 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "No se pudo aplicar SWAP.");
            }

            MatchPlayerRuntime tmp = state.Players[currentIndex];
            state.Players[currentIndex] = state.Players[targetIndex];
            state.Players[targetIndex] = tmp;

            state.CurrentPlayerIndex = targetIndex;

            BroadcastTurnOrderInitialized(state);
        }

        private static void ApplyWildcardSabotage(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            MatchPlayerRuntime target = ResolveNextAlivePlayerOrThrow(state, currentPlayer.UserId);

            target.PendingTimeDeltaSeconds -= WILDCARD_TIME_PENALTY_SECONDS;

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(
                    state.MatchId,
                    SPECIAL_EVENT_TIME_PENALTY_CODE,
                    string.Format(
                        SPECIAL_EVENT_TIME_PENALTY_DESCRIPTION_TEMPLATE,
                        target.DisplayName,
                        WILDCARD_TIME_PENALTY_SECONDS)),
                "GameplayEngine.Wildcard.Sabotage");
        }

        private static void ApplyWildcardExtraTime(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            currentPlayer.PendingTimeDeltaSeconds += WILDCARD_TIME_BONUS_SECONDS;

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(
                    state.MatchId,
                    SPECIAL_EVENT_TIME_BONUS_CODE,
                    string.Format(
                        SPECIAL_EVENT_TIME_BONUS_DESCRIPTION_TEMPLATE,
                        currentPlayer.DisplayName,
                        WILDCARD_TIME_BONUS_SECONDS)),
                "GameplayEngine.Wildcard.ExtraTime");
        }

        private static void BroadcastWildcardUsed(MatchRuntimeState state, MatchPlayerRuntime actor, string wildcardCode)
        {
            string code = string.Format(SPECIAL_EVENT_WILDCARD_USED_CODE_TEMPLATE, wildcardCode);
            string description = string.Format(SPECIAL_EVENT_WILDCARD_USED_DESCRIPTION_TEMPLATE, actor.DisplayName, wildcardCode);

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, code, description),
                "GameplayEngine.Wildcard.Used");
        }

        private static MatchPlayerRuntime ResolveNextAlivePlayerOrThrow(MatchRuntimeState state, int currentUserId)
        {
            if (state == null || state.Players.Count <= 1)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "No hay jugador objetivo.");
            }

            int startIndex = state.Players.FindIndex(p => p != null && p.UserId == currentUserId);
            if (startIndex < 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Jugador actual inválido.");
            }

            int idx = startIndex;

            for (int i = 0; i < state.Players.Count; i++)
            {
                idx++;

                if (idx >= state.Players.Count)
                {
                    idx = 0;
                }

                MatchPlayerRuntime candidate = state.Players[idx];
                if (candidate != null && !candidate.IsEliminated && candidate.UserId != currentUserId)
                {
                    return candidate;
                }
            }

            throw ThrowFault(ERROR_INVALID_REQUEST, "No hay jugador vivo objetivo.");
        }

        internal static void TrySendSnapshotToJoiningPlayer(MatchRuntimeState state, int userId)
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
                if (state.IsDarkModeActive)
                {
                    joiningPlayer.Callback.OnSpecialEvent(
                        state.MatchId,
                        SPECIAL_EVENT_DARK_MODE_STARTED_CODE,
                        SPECIAL_EVENT_DARK_MODE_STARTED_DESCRIPTION);
                }

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

                if (state.IsSurpriseExamActive)
                {
                    SurpriseExamState exam = state.SurpriseExam;

                    if (exam != null &&
                        exam.QuestionIdByUserId.TryGetValue(userId, out int examQuestionId) &&
                        state.QuestionsById.TryGetValue(examQuestionId, out QuestionWithAnswersDto examQuestion) &&
                        !exam.IsCorrectByUserId.ContainsKey(userId))
                    {
                        joiningPlayer.Callback.OnSpecialEvent(
                            state.MatchId,
                            SPECIAL_EVENT_SURPRISE_EXAM_STARTED_CODE,
                            SPECIAL_EVENT_SURPRISE_EXAM_STARTED_DESCRIPTION);

                        joiningPlayer.Callback.OnNextQuestion(
                            state.MatchId,
                            BuildPlayerSummary(joiningPlayer, isOnline: true),
                            examQuestion,
                            state.CurrentChain,
                            state.BankedPoints);

                        return;
                    }
                }

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
                    joiningPlayer.Callback.OnNextQuestion(
                        state.MatchId,
                        BuildPlayerSummary(current, isOnline: true),
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

        internal void ValidateNotNullRequest(object request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request is null.");
            }
        }

        internal void ValidateMatchId(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }
        }

        internal int Authenticate(string token)
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

        private static void StartVotePhase(MatchRuntimeState state)
        {
            state.ResetSurpriseExam();

            state.IsInVotePhase = true;
            state.IsInDuelPhase = false;
            state.WeakestRivalUserId = null;
            state.DuelTargetUserId = null;
            state.VotersThisRound.Clear();
            state.VotesThisRound.Clear();
            state.BombQuestionId = 0;

            state.ActiveSpecialEvent = SpecialEventType.None;

            Broadcast(
                state,
                cb => cb.OnVotePhaseStarted(state.MatchId, TimeSpan.FromSeconds(VOTE_PHASE_TIME_LIMIT_SECONDS)),
                "GameplayEngine.StartVotePhase");
        }

        private static void ResolveEliminationOrStartDuel(MatchRuntimeState state)
        {
            state.IsInVotePhase = false;
            state.BombQuestionId = 0;

            EndDarkModeIfActive(state);

            List<MatchPlayerRuntime> alivePlayers = state.Players
                .Where(p => !p.IsEliminated)
                .ToList();

            if (alivePlayers.Count < MIN_PLAYERS_TO_CONTINUE)
            {
                FinishMatchWithWinnerIfApplicable(state);
                return;
            }

            Dictionary<int, int> voteCounts = CountVotesForAlivePlayers(state, alivePlayers);
            if (voteCounts.Count == 0)
            {
                StartNextRound(state);
                return;
            }

            if (!TryResolveWeakestRivalUserIdConsideringShield(state, voteCounts, out int weakestRivalUserId))
            {
                StartNextRound(state);
                return;
            }

            MatchPlayerRuntime weakestRivalPlayer = state.Players.FirstOrDefault(p => p.UserId == weakestRivalUserId);
            if (weakestRivalPlayer == null)
            {
                StartNextRound(state);
                return;
            }

            CoinFlipResolvedDto coinFlip = PerformCoinFlip(state, weakestRivalUserId);

            Broadcast(
                state,
                cb => cb.OnCoinFlipResolved(state.MatchId, coinFlip),
                "GameplayEngine.CoinFlip");

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

        private static bool TryResolveWeakestRivalUserIdConsideringShield(
            MatchRuntimeState state,
            Dictionary<int, int> voteCounts,
            out int weakestRivalUserId)
        {
            weakestRivalUserId = 0;

            while (voteCounts.Count > 0)
            {
                int maxVotes = voteCounts.Values.Max();

                List<int> candidates = voteCounts
                    .Where(kvp => kvp.Value == maxVotes)
                    .Select(kvp => kvp.Key)
                    .ToList();

                int selected = candidates.Count == 1
                    ? candidates[0]
                    : candidates[NextRandom(0, candidates.Count)];

                MatchPlayerRuntime selectedPlayer = state.Players.FirstOrDefault(p => p != null && p.UserId == selected);
                if (selectedPlayer == null || selectedPlayer.IsEliminated)
                {
                    voteCounts.Remove(selected);
                    continue;
                }

                if (!selectedPlayer.IsShieldActive)
                {
                    weakestRivalUserId = selected;
                    return true;
                }

                selectedPlayer.IsShieldActive = false;

                Broadcast(
                    state,
                    cb => cb.OnSpecialEvent(
                        state.MatchId,
                        SPECIAL_EVENT_SHIELD_TRIGGERED_CODE,
                        string.Format(SPECIAL_EVENT_SHIELD_TRIGGERED_DESCRIPTION_TEMPLATE, selectedPlayer.DisplayName)),
                    "GameplayEngine.Shield.Triggered");

                voteCounts.Remove(selected);
            }

            return false;
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

        private static void EliminatePlayerByVoteNoDuel(MatchRuntimeState state, MatchPlayerRuntime weakestRivalPlayer)
        {
            weakestRivalPlayer.IsEliminated = true;

            Broadcast(
                state,
                cb => cb.OnElimination(state.MatchId, BuildPlayerSummary(weakestRivalPlayer, isOnline: true)),
                "GameplayEngine.Elimination");

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

                duelCandidates.Add(new DuelCandidateDto
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
                Logger.Warn("Error OnDuelCandidates.", ex);
            }
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

                Broadcast(
                    state,
                    cb => cb.OnElimination(state.MatchId, BuildPlayerSummary(currentPlayer, isOnline: true)),
                    "GameplayEngine.DuelElimination");

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

            int nextIndex = state.Players.FindIndex(p => p.UserId == nextUserId && !p.IsEliminated);
            if (nextIndex >= 0)
            {
                state.CurrentPlayerIndex = nextIndex;
                SendNextQuestion(state);
            }
        }

        private static void StartNextRound(MatchRuntimeState state)
        {
            List<MatchPlayerRuntime> alivePlayers = state.Players
                .Where(p => !p.IsEliminated)
                .ToList();

            if (alivePlayers.Count < MIN_PLAYERS_TO_CONTINUE)
            {
                FinishMatchWithWinnerIfApplicable(state);
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

            state.IsDarkModeActive = false;
            state.DarkModeRoundNumber = 0;

            state.ResetSurpriseExam();

            state.RestoreTurnAfterLightning();
            state.ResetLightningChallenge();

            if (state.CurrentPlayerIndex < 0 ||
                state.CurrentPlayerIndex >= state.Players.Count ||
                state.Players[state.CurrentPlayerIndex].IsEliminated)
            {
                int firstAlive = state.Players.FindIndex(p => !p.IsEliminated);
                if (firstAlive < 0)
                {
                    return;
                }

                state.CurrentPlayerIndex = firstAlive;
            }

            TryStartDarkModeEvent(state);

            if (TryStartSurpriseExamEvent(state))
            {
                return;
            }

            bool hasLightningStarted = TryStartLightningChallenge(state);
            if (!hasLightningStarted)
            {
                SendNextQuestion(state);
            }
        }

        private static void ShufflePlayersForStart(MatchRuntimeState state)
        {
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
        }

        private static void BroadcastTurnOrderInitialized(MatchRuntimeState state)
        {
            int[] orderedAliveUserIds = state.Players
                .Where(p => p != null && !p.IsEliminated)
                .Select(p => p.UserId)
                .ToArray();

            MatchPlayerRuntime current = state.GetCurrentPlayer();

            TurnOrderDto dto = new TurnOrderDto
            {
                OrderedAliveUserIds = orderedAliveUserIds,
                CurrentTurnUserId = current != null ? current.UserId : TURN_USER_ID_NONE,
                ServerUtcTicks = DateTime.UtcNow.Ticks
            };

            Broadcast(
                state,
                cb => cb.OnTurnOrderInitialized(state.MatchId, dto),
                "GameplayEngine.TurnOrder");
        }

        private static TurnOrderDto BuildTurnOrderDto(MatchRuntimeState state)
        {
            int[] orderedAliveUserIds = state.Players
                .Where(p => p != null && !p.IsEliminated)
                .Select(p => p.UserId)
                .ToArray();

            MatchPlayerRuntime current = state.GetCurrentPlayer();

            return new TurnOrderDto
            {
                OrderedAliveUserIds = orderedAliveUserIds,
                CurrentTurnUserId = current != null ? current.UserId : TURN_USER_ID_NONE,
                ServerUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        private static void BroadcastTurnOrderChanged(MatchRuntimeState state, string reasonCode)
        {
            TurnOrderDto dto = BuildTurnOrderDto(state);

            Broadcast(
                state,
                cb => cb.OnTurnOrderChanged(state.MatchId, dto, reasonCode ?? string.Empty),
                "GameplayEngine.TurnOrderChanged");
        }

        internal MatchPlayerRuntime ValidateWildcardUseUnderLockOrThrow(MatchRuntimeState state, int userId, int clientRoundNumber)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.RoundNumber != clientRoundNumber)
            {
                throw ThrowFault(ERROR_INVALID_ROUND, ERROR_INVALID_ROUND_MESSAGE);
            }

            if (state.IsInVotePhase || state.IsInDuelPhase || state.IsSurpriseExamActive || IsLightningActive(state))
            {
                throw ThrowFault(ERROR_WILDCARD_INVALID_TIMING, ERROR_WILDCARD_INVALID_TIMING_MESSAGE);
            }

            MatchPlayerRuntime actor = state.Players.FirstOrDefault(p => p != null && p.UserId == userId);
            if (actor == null || actor.IsEliminated)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Player not in match or eliminated.");
            }

            if (actor.BlockWildcardsRoundNumber == state.RoundNumber)
            {
                throw ThrowFault(ERROR_WILDCARDS_BLOCKED, ERROR_WILDCARDS_BLOCKED_MESSAGE);
            }

            return GetCurrentPlayerOrThrow(state, userId);
        }

        internal void ApplyWildcardUnderLockFromServiceOrThrow(
            MatchRuntimeState state,
            MatchPlayerRuntime currentPlayer,
            string wildcardCode)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (currentPlayer == null)
            {
                throw new ArgumentNullException(nameof(currentPlayer));
            }

            string normalizedCode = (wildcardCode ?? string.Empty).Trim().ToUpperInvariant();

            ApplyWildcardLocked(state, currentPlayer, normalizedCode);

            BroadcastWildcardUsed(state, currentPlayer, normalizedCode);
        }

        private static void NotifyAndClearPendingTimeDeltaIfAny(MatchRuntimeState state, MatchPlayerRuntime targetPlayer)
        {
            if (state == null || targetPlayer == null || targetPlayer.Callback == null)
            {
                return;
            }

            int deltaSeconds = targetPlayer.PendingTimeDeltaSeconds;
            if (deltaSeconds == 0)
            {
                return;
            }

            targetPlayer.PendingTimeDeltaSeconds = 0;

            string reasonCode = TURN_REASON_TIME_DELTA_PREFIX + deltaSeconds.ToString();

            try
            {
                targetPlayer.Callback.OnTurnOrderChanged(state.MatchId, BuildTurnOrderDto(state), reasonCode);
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayEngine.TimeDelta callback failed.", ex);
            }
        }

        private static void SendNextQuestion(MatchRuntimeState state)
        {
            if (state.Questions.Count == 0)
            {
                return;
            }

            QuestionWithAnswersDto question = state.Questions.Dequeue();
            state.CurrentQuestionId = question.QuestionId;

            MatchPlayerRuntime targetPlayer = state.GetCurrentPlayer();
            if (targetPlayer == null)
            {
                return;
            }

            state.BombQuestionId = 0;
            TryStartBombQuestionEvent(state, targetPlayer, question.QuestionId);
            NotifyAndClearPendingTimeDeltaIfAny(state, targetPlayer);

            Broadcast(
                state,
                cb => cb.OnNextQuestion(
                    state.MatchId,
                    BuildPlayerSummary(targetPlayer, isOnline: true),
                    question,
                    state.CurrentChain,
                    state.BankedPoints),
                "GameplayEngine.SendNextQuestion");
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
            if (string.IsNullOrWhiteSpace(answerText))
            {
                return false;
            }

            string safeAnswer = answerText.Trim();

            AnswerDto selectedAnswer = question.Answers.Find(a =>
                string.Equals(a.Text, safeAnswer, StringComparison.Ordinal));

            if (selectedAnswer == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Answer not found for current question.");
            }

            return selectedAnswer.IsCorrect;
        }

        private static decimal UpdateChainState(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, bool isCorrect)
        {
            if (!isCorrect)
            {
                state.CurrentChain = 0m;
                state.CurrentStreak = 0;

                currentPlayer.IsDoublePointsActive = false;

                return 0m;
            }

            if (state.CurrentStreak >= CHAIN_STEPS.Length)
            {
                currentPlayer.IsDoublePointsActive = false;
                return 0m;
            }

            decimal baseIncrement = CHAIN_STEPS[state.CurrentStreak];
            decimal increment = currentPlayer.IsDoublePointsActive
                ? baseIncrement + baseIncrement
                : baseIncrement;

            state.CurrentChain += increment;
            state.CurrentStreak++;

            currentPlayer.IsDoublePointsActive = false;

            return increment;
        }

        private static AnswerResult BuildAnswerResult(
            int questionId,
            MatchRuntimeState state,
            bool isCorrect,
            decimal chainIncrement)
        {
            return new AnswerResult
            {
                QuestionId = questionId,
                IsCorrect = isCorrect,
                ChainIncrement = chainIncrement,
                CurrentChain = state.CurrentChain,
                BankedPoints = state.BankedPoints
            };
        }

        private static int CountAlivePlayersOrFallbackToTotal(MatchRuntimeState state)
        {
            int alivePlayersCount = state.Players.Count(p => !p.IsEliminated);
            return alivePlayersCount > 0 ? alivePlayersCount : state.Players.Count;
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
                        "{0}: callback failed. PlayerUserId={1}",
                        logContext,
                        player.UserId);

                    Logger.Warn(logContext, ex);
                }
            }
        }

        private static int NextRandom(int minInclusive, int maxExclusive)
        {
            lock (RandomSyncRoot)
            {
                return RandomGenerator.Next(minInclusive, maxExclusive);
            }
        }

        private static int GetMaxQuestionsOrDefault(int? requested)
        {
            return requested.HasValue && requested.Value > 0
                ? requested.Value
                : DEFAULT_MAX_QUESTIONS;
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

            return LoadQuestions(difficulty, FALLBACK_LOCALE_EN_US, maxQuestions);
        }

        private static string ExtractLanguageCode(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return string.Empty;
            }

            string trimmed = localeCode.Trim();
            int dashIndex = trimmed.IndexOf('-');
            return dashIndex > 0 ? trimmed.Substring(0, dashIndex) : trimmed;
        }

        private static List<QuestionWithAnswersDto> LoadQuestions(byte difficulty, string localeCode, int maxQuestions)
        {
            List<QuestionWithAnswersDto> result = new List<QuestionWithAnswersDto>();

            using (SqlConnection connection = new SqlConnection(GetConnectionString()))
            using (SqlCommand command = new SqlCommand(QuestionsSql.Text.LIST_QUESTIONS_WITH_ANSWERS, connection))
            {
                command.CommandType = CommandType.Text;

                command.Parameters.Add("@MaxQuestions", SqlDbType.Int).Value = maxQuestions;
                command.Parameters.Add("@Difficulty", SqlDbType.TinyInt).Value = difficulty;

                string safeLocale = (localeCode ?? string.Empty).Trim();
                if (safeLocale.Length > LOCALE_CODE_MAX_LENGTH)
                {
                    safeLocale = safeLocale.Substring(0, LOCALE_CODE_MAX_LENGTH);
                }

                command.Parameters.Add("@LocaleCode", SqlDbType.NVarChar, LOCALE_CODE_MAX_LENGTH).Value = safeLocale;

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

                        question.Answers.Add(new AnswerDto
                        {
                            AnswerId = reader.GetInt32(5),
                            Text = reader.GetString(6),
                            IsCorrect = reader.GetBoolean(7),
                            DisplayOrder = reader.GetByte(8)
                        });
                    }
                }

                result = new List<QuestionWithAnswersDto>(questionsById.Values);
            }

            return result;
        }

        private static void ValidateGetQuestionsRequest(GetQuestionsRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Request is null.");
            }

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
                    "GameplayEngine.GetConnectionString",
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

            return new CoinFlipResolvedDto
            {
                RoundId = state.RoundNumber,
                WeakestRivalPlayerId = weakestRivalUserId,
                Result = result,
                ShouldEnableDuel = shouldEnableDuel
            };
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

            state.ResetSurpriseExam();

            Broadcast(
                state,
                cb => cb.OnMatchFinished(state.MatchId, BuildPlayerSummary(winner, isOnline: true)),
                "GameplayEngine.MatchFinished");

            if (state.WildcardMatchId > 0)
            {
                RuntimeMatchByWildcardMatchId.TryRemove(state.WildcardMatchId, out _);
            }

            Matches.TryRemove(state.MatchId, out _);
            ExpectedPlayersByMatchId.TryRemove(state.MatchId, out _);

            foreach (MatchPlayerRuntime player in state.Players)
            {
                if (player != null)
                {
                    PlayerMatchByUserId.TryRemove(player.UserId, out _);
                }
            }
        }

        private static void StoreOrMergeExpectedPlayers(Guid matchId, int[] expectedPlayerUserIds, int callerUserId)
        {
            if (matchId == Guid.Empty)
            {
                return;
            }

            ConcurrentDictionary<int, byte> set = ExpectedPlayersByMatchId.GetOrAdd(
                matchId,
                _ => new ConcurrentDictionary<int, byte>());

            if (expectedPlayerUserIds != null)
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

        private static void EnsureHostPlayerRegistered(MatchRuntimeState state, int userId)
        {
            if (state.Players.Count != 0)
            {
                return;
            }

            IGameplayServiceCallback callback =
                OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

            string displayName = string.Format(DARK_MODE_FALLBACK_PLAYER_NAME_TEMPLATE, userId);

            UserAvatarEntity avatarEntity =
                new UserAvatarSql(GetConnectionString()).GetByUserId(userId);

            state.Players.Add(new MatchPlayerRuntime(userId, displayName, callback)
            {
                Avatar = MapAvatar(avatarEntity)
            });

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

            state.IsDarkModeActive = false;
            state.DarkModeRoundNumber = 0;

            state.ResetSurpriseExam();
            state.RestoreTurnAfterLightning();
            state.ResetLightningChallenge();
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

        private static bool TryStartBombQuestionEvent(MatchRuntimeState state, MatchPlayerRuntime targetPlayer, int questionId)
        {
            if (questionId <= 0)
            {
                return false;
            }

            if (state.HasSpecialEventThisRound || IsLightningActive(state))
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
            string description = string.Format(SPECIAL_EVENT_BOMB_QUESTION_DESCRIPTION_TEMPLATE, targetPlayer.DisplayName, deltaDisplay);

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, SPECIAL_EVENT_BOMB_QUESTION_CODE, description),
                "GameplayEngine.BombQuestion");

            return true;
        }

        private static void ApplyBombQuestionEffectIfNeeded(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, bool isCorrect)
        {
            if (state.BombQuestionId <= 0 || state.BombQuestionId != state.CurrentQuestionId)
            {
                return;
            }

            decimal previousBank = state.BankedPoints < MIN_BANKED_POINTS ? MIN_BANKED_POINTS : state.BankedPoints;
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
                "GameplayEngine.BombQuestion.Applied");
        }

        private static bool TryStartSurpriseExamEvent(MatchRuntimeState state)
        {
            if (state == null)
            {
                return false;
            }

            if (state.HasSpecialEventThisRound || IsLightningActive(state) || state.IsInVotePhase || state.IsInDuelPhase)
            {
                return false;
            }

            List<MatchPlayerRuntime> alivePlayers = state.Players
                .Where(p => p != null && !p.IsEliminated)
                .ToList();

            if (alivePlayers.Count <= 0)
            {
                return false;
            }

            if (state.Questions.Count < alivePlayers.Count)
            {
                return false;
            }

            int randomValue = NextRandom(SURPRISE_EXAM_RANDOM_MIN_VALUE, SURPRISE_EXAM_RANDOM_MAX_VALUE);
            if (randomValue >= SURPRISE_EXAM_PROBABILITY_PERCENT)
            {
                return false;
            }

            DateTime deadlineUtc = DateTime.UtcNow.AddSeconds(SURPRISE_EXAM_TIME_LIMIT_SECONDS);

            state.ActiveSpecialEvent = SpecialEventType.SurpriseExam;
            state.HasSpecialEventThisRound = true;

            SurpriseExamState exam = new SurpriseExamState(deadlineUtc);

            foreach (MatchPlayerRuntime player in alivePlayers)
            {
                QuestionWithAnswersDto question = state.Questions.Dequeue();

                exam.QuestionIdByUserId[player.UserId] = question.QuestionId;
                exam.PendingUserIds.Add(player.UserId);

                TrySendSurpriseExamQuestionToPlayer(state, player, question);
            }

            state.SurpriseExam = exam;

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, SPECIAL_EVENT_SURPRISE_EXAM_STARTED_CODE, SPECIAL_EVENT_SURPRISE_EXAM_STARTED_DESCRIPTION),
                "GameplayEngine.SurpriseExam.Started");

            Timer timer = new Timer(
                SurpriseExamTimeoutCallback,
                state.MatchId,
                TimeSpan.FromSeconds(SURPRISE_EXAM_TIME_LIMIT_SECONDS),
                Timeout.InfiniteTimeSpan);

            exam.AttachTimer(timer);

            return true;
        }

        private static void TrySendSurpriseExamQuestionToPlayer(MatchRuntimeState state, MatchPlayerRuntime player, QuestionWithAnswersDto question)
        {
            if (state == null || player == null || player.Callback == null || question == null)
            {
                return;
            }

            try
            {
                player.Callback.OnNextQuestion(
                    state.MatchId,
                    BuildPlayerSummary(player, isOnline: true),
                    question,
                    state.CurrentChain,
                    state.BankedPoints);
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayEngine.SurpriseExam.Question callback failed.", ex);
            }
        }

        private static void SurpriseExamTimeoutCallback(object stateObj)
        {
            if (!(stateObj is Guid matchId) || matchId == Guid.Empty)
            {
                return;
            }

            if (!Matches.TryGetValue(matchId, out MatchRuntimeState state) || state == null)
            {
                return;
            }

            lock (state.SyncRoot)
            {
                if (!state.IsSurpriseExamActive)
                {
                    return;
                }

                ResolveSurpriseExam(state, SURPRISE_EXAM_RESOLVE_REASON_TIMEOUT);
            }
        }

        private static AnswerResult HandleSurpriseExamSubmitAnswer(MatchRuntimeState state, int userId, SubmitAnswerRequest request)
        {
            SurpriseExamState exam = state.SurpriseExam;

            AnswerResult fallback = new AnswerResult
            {
                QuestionId = request.QuestionId,
                IsCorrect = false,
                ChainIncrement = 0m,
                CurrentChain = state.CurrentChain,
                BankedPoints = state.BankedPoints
            };

            if (exam == null || exam.IsResolved)
            {
                return fallback;
            }

            if (!exam.QuestionIdByUserId.TryGetValue(userId, out int expectedQuestionId))
            {
                return fallback;
            }

            if (expectedQuestionId != request.QuestionId)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Invalid question for SurpriseExam.");
            }

            if (exam.IsCorrectByUserId.ContainsKey(userId))
            {
                return fallback;
            }

            if (!state.QuestionsById.TryGetValue(request.QuestionId, out QuestionWithAnswersDto question))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Question not found for SurpriseExam.");
            }

            bool isCorrect = EvaluateAnswerOrThrow(question, request.AnswerText);

            exam.IsCorrectByUserId[userId] = isCorrect;
            exam.PendingUserIds.Remove(userId);

            MatchPlayerRuntime answeringPlayer = state.Players.FirstOrDefault(p => p.UserId == userId);

            if (answeringPlayer != null && answeringPlayer.Callback != null)
            {
                try
                {
                    answeringPlayer.Callback.OnAnswerEvaluated(
                        state.MatchId,
                        BuildPlayerSummary(answeringPlayer, isOnline: true),
                        new AnswerResult
                        {
                            QuestionId = request.QuestionId,
                            IsCorrect = isCorrect,
                            ChainIncrement = 0m,
                            CurrentChain = state.CurrentChain,
                            BankedPoints = state.BankedPoints
                        });
                }
                catch (Exception ex)
                {
                    Logger.Warn("GameplayEngine.SurpriseExam.Answer callback failed.", ex);
                }
            }

            if (exam.PendingUserIds.Count <= 0)
            {
                ResolveSurpriseExam(state, SURPRISE_EXAM_RESOLVE_REASON_ALL_ANSWERED);
            }

            return new AnswerResult
            {
                QuestionId = request.QuestionId,
                IsCorrect = isCorrect,
                ChainIncrement = 0m,
                CurrentChain = state.CurrentChain,
                BankedPoints = state.BankedPoints
            };
        }

        private static void ResolveSurpriseExam(MatchRuntimeState state, string reasonCode)
        {
            SurpriseExamState exam = state.SurpriseExam;

            if (exam == null || exam.IsResolved)
            {
                return;
            }

            exam.IsResolved = true;

            foreach (int pendingUserId in exam.PendingUserIds.ToList())
            {
                exam.IsCorrectByUserId[pendingUserId] = false;
            }

            exam.PendingUserIds.Clear();

            int total = exam.QuestionIdByUserId.Count;
            int correct = exam.IsCorrectByUserId.Values.Count(v => v);

            bool allCorrect = total > 0 && correct == total;

            decimal previousBank = state.BankedPoints < MIN_BANKED_POINTS ? MIN_BANKED_POINTS : state.BankedPoints;
            decimal delta = allCorrect ? SURPRISE_EXAM_SUCCESS_BONUS : -SURPRISE_EXAM_FAILURE_PENALTY;

            decimal updatedBank = previousBank + delta;
            if (updatedBank < MIN_BANKED_POINTS)
            {
                updatedBank = MIN_BANKED_POINTS;
            }

            state.BankedPoints = updatedBank;

            string outcome = allCorrect
                ? SPECIAL_EVENT_SURPRISE_EXAM_OUTCOME_ALL_CORRECT
                : SPECIAL_EVENT_SURPRISE_EXAM_OUTCOME_SOME_FAILED;

            string deltaDisplay = delta.ToString("+0.00;-0.00;0.00");
            string bankDisplay = state.BankedPoints.ToString("0.00");

            string description = string.Format(
                SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_DESCRIPTION_TEMPLATE,
                outcome,
                correct,
                total,
                deltaDisplay,
                bankDisplay);

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_CODE, description),
                "GameplayEngine.SurpriseExam.Resolved");

            BankState bankState = new BankState
            {
                MatchId = state.MatchId,
                CurrentChain = state.CurrentChain,
                BankedPoints = state.BankedPoints
            };

            Broadcast(
                state,
                cb => cb.OnBankUpdated(state.MatchId, bankState),
                "GameplayEngine.SurpriseExam.BankUpdated");

            exam.DisposeTimerSafely();

            state.ActiveSpecialEvent = SpecialEventType.None;
            state.SurpriseExam = null;

            if (state.IsFinished)
            {
                return;
            }

            if (state.Questions.Count == 0)
            {
                StartVotePhase(state);
                return;
            }

            SendNextQuestion(state);
        }

        private static bool IsLightningActive(MatchRuntimeState state)
        {
            return state != null &&
                   state.ActiveSpecialEvent == SpecialEventType.LightningChallenge &&
                   state.LightningChallenge != null;
        }

        private static bool TryStartLightningChallenge(MatchRuntimeState state)
        {
            if (state.HasSpecialEventThisRound || IsLightningActive(state))
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

            List<MatchPlayerRuntime> candidates = state.Players.Where(p => !p.IsEliminated).ToList();
            if (candidates.Count == 0)
            {
                return false;
            }

            MatchPlayerRuntime targetPlayer = candidates[NextRandom(0, candidates.Count)];
            int targetPlayerIndex = state.Players.FindIndex(p => p.UserId == targetPlayer.UserId);
            if (targetPlayerIndex < 0)
            {
                return false;
            }

            List<QuestionWithAnswersDto> lightningQuestions = new List<QuestionWithAnswersDto>();
            for (int i = 0; i < LIGHTNING_TOTAL_QUESTIONS; i++)
            {
                lightningQuestions.Add(state.Questions.Dequeue());
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

            Broadcast(
                state,
                cb => cb.OnLightningChallengeStarted(
                    state.MatchId,
                    state.LightningChallenge.RoundId,
                    BuildPlayerSummary(targetPlayer, isOnline: true),
                    LIGHTNING_TOTAL_QUESTIONS,
                    LIGHTNING_TOTAL_TIME_SECONDS),
                "GameplayEngine.Lightning.Started");

            QuestionWithAnswersDto firstQuestion = state.GetCurrentLightningQuestion();

            Broadcast(
                state,
                cb => cb.OnLightningChallengeQuestion(
                    state.MatchId,
                    state.LightningChallenge.RoundId,
                    1,
                    firstQuestion),
                "GameplayEngine.Lightning.Question");

            return true;
        }

        private static AnswerResult HandleLightningSubmitAnswer(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, SubmitAnswerRequest request)
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

            if (challenge == null || challenge.PlayerId != currentPlayer.UserId || challenge.IsCompleted)
            {
                return fallbackResult;
            }

            QuestionWithAnswersDto question = state.GetCurrentLightningQuestion();
            if (question == null)
            {
                return fallbackResult;
            }

            bool isCorrect = false;

            if (!string.IsNullOrWhiteSpace(request.AnswerText))
            {
                AnswerDto selected = question.Answers.Find(a =>
                    string.Equals(a.Text, request.AnswerText.Trim(), StringComparison.Ordinal));

                isCorrect = selected != null && selected.IsCorrect;
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

            Broadcast(
                state,
                cb => cb.OnAnswerEvaluated(state.MatchId, BuildPlayerSummary(currentPlayer, isOnline: true), result),
                "GameplayEngine.Lightning.Answer");

            if (challenge.RemainingQuestions <= 0)
            {
                bool isSuccess = challenge.CorrectAnswers == LIGHTNING_TOTAL_QUESTIONS;
                CompleteLightningChallenge(state, isSuccess);
                return result;
            }

            state.MoveToNextLightningQuestion();

            QuestionWithAnswersDto nextQuestion = state.GetCurrentLightningQuestion();
            int questionIndex = LIGHTNING_TOTAL_QUESTIONS - challenge.RemainingQuestions;

            Broadcast(
                state,
                cb => cb.OnLightningChallengeQuestion(state.MatchId, challenge.RoundId, questionIndex, nextQuestion),
                "GameplayEngine.Lightning.NextQuestion");

            return result;
        }

        private static void CompleteLightningChallenge(MatchRuntimeState state, bool isSuccess)
        {
            LightningChallengeState challenge = state.LightningChallenge;
            if (challenge == null)
            {
                return;
            }

            challenge.IsCompleted = true;
            challenge.IsSuccess = isSuccess;

            Broadcast(
                state,
                cb => cb.OnLightningChallengeFinished(state.MatchId, challenge.RoundId, challenge.CorrectAnswers, isSuccess),
                "GameplayEngine.Lightning.Finished");

            if (isSuccess)
            {
                TryAwardLightningWildcard(state, challenge.PlayerId);
            }

            state.RestoreTurnAfterLightning();
            state.ResetLightningChallenge();

            SendNextQuestion(state);
        }

        private static bool TryStartExtraWildcardEvent(MatchRuntimeState state)
        {
            if (state.HasSpecialEventThisRound || IsLightningActive(state))
            {
                return false;
            }

            List<MatchPlayerRuntime> candidates = state.Players.Where(p => !p.IsEliminated).ToList();
            if (candidates.Count == 0)
            {
                return false;
            }

            int probabilityValue = NextRandom(EXTRA_WILDCARD_RANDOM_MIN_VALUE, EXTRA_WILDCARD_RANDOM_MAX_VALUE);
            if (probabilityValue >= EXTRA_WILDCARD_PROBABILITY_PERCENT)
            {
                return false;
            }

            MatchPlayerRuntime targetPlayer = candidates[NextRandom(0, candidates.Count)];

            TryAwardExtraWildcard(state, targetPlayer.UserId);

            state.HasSpecialEventThisRound = true;

            return true;
        }

        private static void TryAwardLightningWildcard(MatchRuntimeState state, int playerUserId)
        {
            MatchPlayerRuntime targetPlayer = state.Players.FirstOrDefault(p => p.UserId == playerUserId);
            if (targetPlayer == null)
            {
                return;
            }

            try
            {
                if (state.WildcardMatchId > 0)
                {
                    WildcardService.GrantLightningWildcard(state.WildcardMatchId, playerUserId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GameplayEngine.TryAwardLightningWildcard", ex);
            }

            string description = string.Format(SPECIAL_EVENT_LIGHTNING_WILDCARD_DESCRIPTION_TEMPLATE, targetPlayer.DisplayName);

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE, description),
                "GameplayEngine.SpecialEvent.LightningWildcard");
        }

        private static void TryAwardExtraWildcard(MatchRuntimeState state, int playerUserId)
        {
            MatchPlayerRuntime targetPlayer = state.Players.FirstOrDefault(p => p.UserId == playerUserId);
            if (targetPlayer == null)
            {
                return;
            }

            try
            {
                if (state.WildcardMatchId > 0)
                {
                    WildcardService.GrantRandomWildcardForMatch(state.WildcardMatchId, playerUserId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GameplayEngine.TryAwardExtraWildcard", ex);
            }

            string description = string.Format(SPECIAL_EVENT_EXTRA_WILDCARD_DESCRIPTION_TEMPLATE, targetPlayer.DisplayName);

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, SPECIAL_EVENT_EXTRA_WILDCARD_CODE, description),
                "GameplayEngine.SpecialEvent.ExtraWildcard");
        }

        private static bool TryStartDarkModeEvent(MatchRuntimeState state)
        {
            if (state == null)
            {
                return false;
            }

            if (state.HasSpecialEventThisRound || IsLightningActive(state) || state.IsInVotePhase || state.IsInDuelPhase)
            {
                return false;
            }

            int randomValue = NextRandom(DARK_MODE_RANDOM_MIN_VALUE, DARK_MODE_RANDOM_MAX_VALUE);
            if (randomValue >= DARK_MODE_PROBABILITY_PERCENT)
            {
                return false;
            }

            state.IsDarkModeActive = true;
            state.DarkModeRoundNumber = state.RoundNumber;
            state.HasSpecialEventThisRound = true;

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, SPECIAL_EVENT_DARK_MODE_STARTED_CODE, SPECIAL_EVENT_DARK_MODE_STARTED_DESCRIPTION),
                "GameplayEngine.DarkMode.Started");

            return true;
        }

        private static void EndDarkModeIfActive(MatchRuntimeState state)
        {
            if (state == null || !state.IsDarkModeActive)
            {
                return;
            }

            NotifyVotersAboutTheirVote(state);

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, SPECIAL_EVENT_DARK_MODE_ENDED_CODE, SPECIAL_EVENT_DARK_MODE_ENDED_DESCRIPTION),
                "GameplayEngine.DarkMode.Ended");

            state.IsDarkModeActive = false;
            state.DarkModeRoundNumber = 0;
        }

        private static void NotifyVotersAboutTheirVote(MatchRuntimeState state)
        {
            foreach (KeyValuePair<int, int?> kvp in state.VotesThisRound)
            {
                int voterUserId = kvp.Key;
                int? targetUserId = kvp.Value;

                MatchPlayerRuntime voter = state.Players.FirstOrDefault(p => p != null && p.UserId == voterUserId);
                if (voter == null || voter.Callback == null)
                {
                    continue;
                }

                string targetDisplayName = ResolveVoteTargetDisplayName(state, targetUserId);

                try
                {
                    voter.Callback.OnSpecialEvent(
                        state.MatchId,
                        SPECIAL_EVENT_DARK_MODE_VOTE_REVEAL_CODE,
                        string.Format(SPECIAL_EVENT_DARK_MODE_VOTE_REVEAL_DESCRIPTION_TEMPLATE, targetDisplayName));
                }
                catch (Exception ex)
                {
                    Logger.Warn("GameplayEngine.DarkMode.VoteReveal callback failed.", ex);
                }
            }
        }

        private static string ResolveVoteTargetDisplayName(MatchRuntimeState state, int? targetUserId)
        {
            if (!targetUserId.HasValue)
            {
                return DARK_MODE_NO_VOTE_DISPLAY_NAME;
            }

            MatchPlayerRuntime target = state.Players.FirstOrDefault(p => p != null && p.UserId == targetUserId.Value);
            if (target != null && !string.IsNullOrWhiteSpace(target.DisplayName))
            {
                return target.DisplayName;
            }

            return string.Format(DARK_MODE_FALLBACK_PLAYER_NAME_TEMPLATE, targetUserId.Value);
        }

        internal static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat("Service fault. Code='{0}', Message='{1}'", code, message);

            ServiceFault fault = new ServiceFault
            {
                Code = code ?? string.Empty,
                Message = message ?? string.Empty
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message ?? string.Empty));
        }

        internal static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            ServiceFault fault = new ServiceFault
            {
                Code = code ?? string.Empty,
                Message = userMessage ?? string.Empty
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage ?? string.Empty));
        }
    }
}
