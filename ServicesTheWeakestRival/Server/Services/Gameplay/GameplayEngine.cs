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
        private const string ERROR_NO_QUESTIONS_MESSAGE = "No se encontraron preguntas para la dificultad/idioma solicitados.";

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
        private const string SPECIAL_EVENT_LIGHTNING_WILDCARD_DESCRIPTION_TEMPLATE = "El jugador {0} ha ganado un comodín relámpago.";

        private const string SPECIAL_EVENT_EXTRA_WILDCARD_CODE = "EXTRA_WILDCARD_AWARDED";
        private const string SPECIAL_EVENT_EXTRA_WILDCARD_DESCRIPTION_TEMPLATE = "El jugador {0} ha recibido un comodín extra.";

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
        private const int DARK_MODE_PROBABILITY_PERCENT = 100;

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

        private static readonly ConcurrentDictionary<int, Guid> PlayerMatchByUserId =
            new ConcurrentDictionary<int, Guid>();

        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>> ExpectedPlayersByMatchId =
            new ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>>();

        private static readonly Random RandomGenerator = new Random();
        private static readonly object RandomSyncRoot = new object();

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
                List<QuestionWithAnswersDto> questions = LoadQuestions(request.Difficulty, request.LocaleCode, maxQuestions);

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

                    UserAvatarEntity avatarEntity = new UserAvatarSql(GetConnectionString()).GetByUserId(userId);

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

                UpdateChainState(state, isCorrect);
                ApplyBombQuestionEffectIfNeeded(state, currentPlayer, isCorrect);

                AnswerResult result = BuildAnswerResult(question.QuestionId, state, isCorrect);

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

            int weakestRivalUserId = ResolveWeakestRivalUserId(voteCounts, state);

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

            return candidates[NextRandom(0, candidates.Count)];
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

            AnswerDto selectedAnswer = question.Answers.Find(a =>
                string.Equals(a.Text, answerText, StringComparison.Ordinal));

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
                    state.CurrentChain += CHAIN_STEPS[state.CurrentStreak];
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

            Matches.TryRemove(state.MatchId, out _);
            ExpectedPlayersByMatchId.TryRemove(state.MatchId, out _);

            foreach (MatchPlayerRuntime player in state.Players)
            {
                PlayerMatchByUserId.TryRemove(player.UserId, out _);
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

            UserAvatarEntity avatarEntity = new UserAvatarSql(GetConnectionString()).GetByUserId(userId);

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

        private static bool IsLightningActive(MatchRuntimeState state)
        {
            return state != null &&
                   state.ActiveSpecialEvent == SpecialEventType.LightningChallenge &&
                   state.LightningChallenge != null;
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
                    string.Equals(a.Text, request.AnswerText, StringComparison.Ordinal));

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
                cb => cb.OnAnswerEvaluated(state.MatchId, BuildPlayerSummary(currentPlayer, isOnline: false), result),
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
            AwardWildcard(
                state,
                playerUserId,
                SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE,
                SPECIAL_EVENT_LIGHTNING_WILDCARD_DESCRIPTION_TEMPLATE);
        }

        private static void TryAwardExtraWildcard(MatchRuntimeState state, int playerUserId)
        {
            AwardWildcard(
                state,
                playerUserId,
                SPECIAL_EVENT_EXTRA_WILDCARD_CODE,
                SPECIAL_EVENT_EXTRA_WILDCARD_DESCRIPTION_TEMPLATE);
        }

        private static void AwardWildcard(MatchRuntimeState state, int playerUserId, string specialEventCode, string descriptionTemplate)
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
                Logger.Error("GameplayEngine.AwardWildcard", ex);
            }

            string description = string.Format(descriptionTemplate, targetPlayer.DisplayName);

            Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, specialEventCode, description),
                "GameplayEngine.SpecialEvent.Wildcard");
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
