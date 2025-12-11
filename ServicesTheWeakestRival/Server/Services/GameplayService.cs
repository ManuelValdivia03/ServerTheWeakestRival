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
    public sealed class GameplayService : IGameplayService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayService));

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

        private const string ERROR_INVALID_REQUEST_MESSAGE = "Request is null.";
        private const string ERROR_MATCH_NOT_FOUND_MESSAGE = "Match not found.";
        private const string ERROR_NOT_PLAYER_TURN_MESSAGE = "It is not the player turn.";
        private const string ERROR_DUEL_NOT_ACTIVE_MESSAGE = "Duel is not active.";
        private const string ERROR_NOT_WEAKEST_RIVAL_MESSAGE = "Only weakest rival can choose duel opponent.";
        private const string ERROR_INVALID_DUEL_TARGET_MESSAGE = "Invalid duel opponent.";

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

        private const int DEFAULT_MAX_QUESTIONS = 40;
        private const int NEXT_QUESTION_DELAY_MS = 2000;

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
        private const int EXTRA_WILDCARD_PROBABILITY_PERCENT = 100;

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

        private static readonly Random RandomGenerator = new Random();

        public SubmitAnswerResponse SubmitAnswer(SubmitAnswerRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }

            int userId = Authenticate(request.Token);

            if (!Matches.TryGetValue(request.MatchId, out MatchRuntimeState state))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            lock (state.SyncRoot)
            {
                if (state.IsInVotePhase)
                {
                    throw ThrowFault(ERROR_INVALID_REQUEST, "Round is in vote phase. No questions available.");
                }

                MatchPlayerRuntime currentPlayer = state.GetCurrentPlayer();
                if (currentPlayer == null || currentPlayer.UserId != userId)
                {
                    throw ThrowFault(ERROR_NOT_PLAYER_TURN, ERROR_NOT_PLAYER_TURN_MESSAGE);
                }

                if (IsLightningActive(state))
                {
                    return HandleLightningSubmitAnswer(request, state, currentPlayer);
                }

                if (!state.QuestionsById.TryGetValue(state.CurrentQuestionId, out QuestionWithAnswersDto question))
                {
                    throw ThrowFault(ERROR_INVALID_REQUEST, "Current question not found for this match.");
                }

                bool isTimeout = string.IsNullOrWhiteSpace(request.AnswerText);
                bool isCorrect = false;

                if (!isTimeout)
                {
                    AnswerDto selectedAnswer = question.Answers.Find(
                        a => string.Equals(a.Text, request.AnswerText, StringComparison.Ordinal));

                    if (selectedAnswer == null)
                    {
                        throw ThrowFault(ERROR_INVALID_REQUEST, "Answer not found for current question.");
                    }

                    isCorrect = selectedAnswer.IsCorrect;
                }

                if (isCorrect)
                {
                    if (state.CurrentStreak < CHAIN_STEPS.Length)
                    {
                        decimal increment = CHAIN_STEPS[state.CurrentStreak];
                        state.CurrentChain += increment;
                        state.CurrentStreak++;
                    }
                }
                else
                {
                    state.CurrentChain = 0m;
                    state.CurrentStreak = 0;
                }

                AnswerResult result = new AnswerResult
                {
                    QuestionId = request.QuestionId,
                    IsCorrect = isCorrect,
                    ChainIncrement = state.CurrentStreak > 0 && isCorrect
                        ? CHAIN_STEPS[state.CurrentStreak - 1]
                        : 0m,
                    CurrentChain = state.CurrentChain,
                    BankedPoints = state.BankedPoints
                };

                PlayerSummary playerSummary = new PlayerSummary
                {
                    UserId = currentPlayer.UserId,
                    DisplayName = currentPlayer.DisplayName,
                    Avatar = currentPlayer.Avatar
                };

                foreach (MatchPlayerRuntime player in state.Players)
                {
                    try
                    {
                        player.Callback.OnAnswerEvaluated(
                            state.MatchId,
                            playerSummary,
                            result);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error al notificar OnAnswerEvaluated.", ex);
                    }
                }

                SubmitAnswerResponse response = new SubmitAnswerResponse
                {
                    Result = result
                };

                if (state.IsInDuelPhase &&
                    state.WeakestRivalUserId.HasValue &&
                    state.DuelTargetUserId.HasValue &&
                    (currentPlayer.UserId == state.WeakestRivalUserId.Value ||
                     currentPlayer.UserId == state.DuelTargetUserId.Value))
                {
                    if (!isCorrect)
                    {
                        currentPlayer.IsEliminated = true;

                        PlayerSummary eliminatedSummary = new PlayerSummary
                        {
                            UserId = currentPlayer.UserId,
                            DisplayName = currentPlayer.DisplayName,
                            IsOnline = true,
                            Avatar = currentPlayer.Avatar
                        };

                        foreach (MatchPlayerRuntime player in state.Players)
                        {
                            try
                            {
                                player.Callback.OnElimination(
                                    state.MatchId,
                                    eliminatedSummary);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn("Error al notificar OnElimination en duelo.", ex);
                            }
                        }

                        Logger.InfoFormat(
                            "Player eliminated by duel. MatchId={0}, Round={1}, EliminatedUserId={2}",
                            state.MatchId,
                            state.RoundNumber,
                            currentPlayer.UserId);

                        state.IsInDuelPhase = false;
                        state.WeakestRivalUserId = null;
                        state.DuelTargetUserId = null;

                        FinishMatchWithWinnerIfApplicable(state);
                        if (state.IsFinished)
                        {
                            return response;
                        }

                        StartNextRound(state);
                        return response;
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

                    return response;
                }

                state.QuestionsAskedThisRound++;

                int alivePlayersCount = state.Players.Count(p => !p.IsEliminated);
                if (alivePlayersCount <= 0)
                {
                    alivePlayersCount = state.Players.Count;
                }

                int maxQuestionsThisRound = alivePlayersCount * QUESTIONS_PER_PLAYER_PER_ROUND;
                bool noMoreQuestions = state.Questions.Count == 0;

                if (state.QuestionsAskedThisRound >= maxQuestionsThisRound || noMoreQuestions)
                {
                    Logger.InfoFormat(
                        "End of round reached. MatchId={0}, Round={1}, QuestionsAskedThisRound={2}, AlivePlayers={3}, NoMoreQuestions={4}",
                        state.MatchId,
                        state.RoundNumber,
                        state.QuestionsAskedThisRound,
                        alivePlayersCount,
                        noMoreQuestions);

                    StartVotePhase(state);
                }
                else
                {
                    state.AdvanceTurn();
                    SendNextQuestion(state);
                }

                return response;
            }
        }

        public BankResponse Bank(BankRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }

            int userId = Authenticate(request.Token);

            if (!Matches.TryGetValue(request.MatchId, out MatchRuntimeState state))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            lock (state.SyncRoot)
            {
                if (state.IsInVotePhase)
                {
                    throw ThrowFault(ERROR_INVALID_REQUEST, "Round is in vote phase. Banking is not allowed.");
                }

                MatchPlayerRuntime currentPlayer = state.GetCurrentPlayer();
                if (currentPlayer == null || currentPlayer.UserId != userId)
                {
                    throw ThrowFault(ERROR_NOT_PLAYER_TURN, ERROR_NOT_PLAYER_TURN_MESSAGE);
                }

                state.BankedPoints += state.CurrentChain;
                state.CurrentChain = 0m;
                state.CurrentStreak = 0;

                BankState bankState = new BankState
                {
                    MatchId = state.MatchId,
                    CurrentChain = state.CurrentChain,
                    BankedPoints = state.BankedPoints
                };

                foreach (MatchPlayerRuntime player in state.Players)
                {
                    try
                    {
                        player.Callback.OnBankUpdated(state.MatchId, bankState);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error al notificar OnBankUpdated.", ex);
                    }
                }

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
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }

            int userId = Authenticate(request.Token);

            if (!Matches.TryGetValue(request.MatchId, out MatchRuntimeState state))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
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

            int maxQuestions = request.MaxQuestions.HasValue && request.MaxQuestions.Value > 0
                ? request.MaxQuestions.Value
                : DEFAULT_MAX_QUESTIONS;

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
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }

            int userId = Authenticate(request.Token);

            IGameplayServiceCallback callback =
                OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

            MatchRuntimeState state = Matches.GetOrAdd(request.MatchId, id => new MatchRuntimeState(id));

            lock (state.SyncRoot)
            {
                MatchPlayerRuntime existingPlayer = state.Players.Find(p => p.UserId == userId);
                if (existingPlayer != null)
                {
                    existingPlayer.Callback = callback;
                }
                else
                {
                    string displayName = "Jugador " + userId;

                    UserAvatarSql avatarSql = new UserAvatarSql(GetConnectionString());
                    UserAvatarEntity avatarEntity = avatarSql.GetByUserId(userId);
                    AvatarAppearanceDto avatar = MapAvatar(avatarEntity);

                    MatchPlayerRuntime player = new MatchPlayerRuntime(userId, displayName, callback)
                    {
                        Avatar = avatar
                    };

                    state.Players.Add(player);
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
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }

            if (request.Difficulty <= 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Difficulty must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.LocaleCode))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "LocaleCode is required.");
            }

            int userId = Authenticate(request.Token);

            int maxQuestions = request.MaxQuestions.HasValue && request.MaxQuestions.Value > 0
                ? request.MaxQuestions.Value
                : DEFAULT_MAX_QUESTIONS;

            MatchRuntimeState state = Matches.GetOrAdd(request.MatchId, id => new MatchRuntimeState(id));

            try
            {
                lock (state.SyncRoot)
                {
                    if (!state.IsInitialized)
                    {
                        List<QuestionWithAnswersDto> questions =
                            LoadQuestions(request.Difficulty, request.LocaleCode, maxQuestions);

                        state.Initialize(request.Difficulty, request.LocaleCode, questions);

                        state.WildcardMatchId = request.MatchDbId;

                        if (state.Players.Count == 0)
                        {
                            IGameplayServiceCallback callback =
                                OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

                            string displayName = "Jugador " + userId;

                            UserAvatarSql avatarSql = new UserAvatarSql(GetConnectionString());
                            UserAvatarEntity avatarEntity = avatarSql.GetByUserId(userId);
                            AvatarAppearanceDto avatar = MapAvatar(avatarEntity);

                            MatchPlayerRuntime player = new MatchPlayerRuntime(userId, displayName, callback)
                            {
                                Avatar = avatar
                            };

                            state.Players.Add(player);
                            PlayerMatchByUserId[userId] = request.MatchId;
                        }

                        state.CurrentPlayerIndex = 0;
                        state.QuestionsAskedThisRound = 0;
                        state.RoundNumber = 1;
                        state.IsInVotePhase = false;
                        state.IsInDuelPhase = false;
                        state.WeakestRivalUserId = null;
                        state.DuelTargetUserId = null;
                        state.VotersThisRound.Clear();
                        state.VotesThisRound.Clear();

                        TryStartExtraWildcardEvent(state);

                        bool lightningStarted = TryStartLightningChallenge(state);
                        if (!lightningStarted)
                        {
                            SendNextQuestion(state);
                        }
                    }
                }

                return new GameplayStartMatchResponse
                {
                    Started = true
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
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.TargetUserId <= 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "TargetUserId is required.");
            }

            int userId = Authenticate(request.Token);

            if (!PlayerMatchByUserId.TryGetValue(userId, out Guid matchId))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            if (!Matches.TryGetValue(matchId, out MatchRuntimeState state))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
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

                int indexWeakest = state.Players.FindIndex(
                    p => p.UserId == state.WeakestRivalUserId.Value && !p.IsEliminated);

                if (indexWeakest >= 0)
                {
                    state.CurrentPlayerIndex = indexWeakest;
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

            PlayerSummary targetSummary = new PlayerSummary
            {
                UserId = targetPlayer.UserId,
                DisplayName = targetPlayer.DisplayName,
                IsOnline = true,
                Avatar = targetPlayer.Avatar
            };

            foreach (MatchPlayerRuntime player in state.Players)
            {
                try
                {
                    player.Callback.OnNextQuestion(
                        state.MatchId,
                        targetSummary,
                        question,
                        state.CurrentChain,
                        state.BankedPoints);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error al notificar OnNextQuestion.", ex);
                }
            }
        }

        private static void StartVotePhase(MatchRuntimeState state)
        {
            state.IsInVotePhase = true;
            state.IsInDuelPhase = false;
            state.WeakestRivalUserId = null;
            state.DuelTargetUserId = null;
            state.VotersThisRound.Clear();
            state.VotesThisRound.Clear();

            TimeSpan timeLimit = TimeSpan.FromSeconds(VOTE_PHASE_TIME_LIMIT_SECONDS);

            foreach (MatchPlayerRuntime player in state.Players)
            {
                try
                {
                    player.Callback.OnVotePhaseStarted(
                        state.MatchId,
                        timeLimit);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error al notificar OnVotePhaseStarted.", ex);
                }
            }
        }

        private static void ResolveEliminationOrStartDuel(MatchRuntimeState state)
        {
            state.IsInVotePhase = false;

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

            if (voteCounts.Count == 0)
            {
                Logger.InfoFormat(
                    "No valid votes registered. No elimination this round. MatchId={0}, Round={1}",
                    state.MatchId,
                    state.RoundNumber);

                StartNextRound(state);
                return;
            }

            int maxVotes = voteCounts.Values.Max();

            List<int> candidates = voteCounts
                .Where(kvp => kvp.Value == maxVotes)
                .Select(kvp => kvp.Key)
                .ToList();

            int weakestRivalUserId;

            if (candidates.Count == 1)
            {
                weakestRivalUserId = candidates[0];
            }
            else
            {
                int randomIndex = RandomGenerator.Next(0, candidates.Count);
                weakestRivalUserId = candidates[randomIndex];

                Logger.InfoFormat(
                    "Tie in votes. Random weakest rival selected. MatchId={0}, CandidatesCount={1}, WeakestRivalUserId={2}",
                    state.MatchId,
                    candidates.Count,
                    weakestRivalUserId);
            }

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

            foreach (MatchPlayerRuntime player in state.Players)
            {
                try
                {
                    player.Callback.OnCoinFlipResolved(
                        state.MatchId,
                        coinFlip);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error al notificar OnCoinFlipResolved.", ex);
                }
            }

            if (!coinFlip.ShouldEnableDuel)
            {
                weakestRivalPlayer.IsEliminated = true;

                PlayerSummary eliminatedSummary = new PlayerSummary
                {
                    UserId = weakestRivalUserId,
                    DisplayName = weakestRivalPlayer.DisplayName,
                    IsOnline = true,
                    Avatar = weakestRivalPlayer.Avatar
                };

                foreach (MatchPlayerRuntime player in state.Players)
                {
                    try
                    {
                        player.Callback.OnElimination(
                            state.MatchId,
                            eliminatedSummary);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error al notificar OnElimination.", ex);
                    }
                }

                Logger.InfoFormat(
                    "Player eliminated by vote (no duel). MatchId={0}, Round={1}, EliminatedUserId={2}",
                    state.MatchId,
                    state.RoundNumber,
                    weakestRivalUserId);

                FinishMatchWithWinnerIfApplicable(state);
                if (state.IsFinished)
                {
                    return;
                }

                StartNextRound(state);
                return;
            }

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

            if (duelCandidates.Count == 0)
            {
                weakestRivalPlayer.IsEliminated = true;

                PlayerSummary eliminatedSummaryNoDuel = new PlayerSummary
                {
                    UserId = weakestRivalUserId,
                    DisplayName = weakestRivalPlayer.DisplayName,
                    IsOnline = true,
                    Avatar = weakestRivalPlayer.Avatar
                };

                foreach (MatchPlayerRuntime player in state.Players)
                {
                    try
                    {
                        player.Callback.OnElimination(
                            state.MatchId,
                            eliminatedSummaryNoDuel);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error al notificar OnElimination (no duel candidates).", ex);
                    }
                }

                Logger.InfoFormat(
                    "Player eliminated by vote (no valid duel candidates). MatchId={0}, Round={1}, EliminatedUserId={2}",
                    state.MatchId,
                    state.RoundNumber,
                    weakestRivalUserId);

                FinishMatchWithWinnerIfApplicable(state);
                if (state.IsFinished)
                {
                    return;
                }

                StartNextRound(state);
                return;
            }

            state.IsInDuelPhase = true;
            state.WeakestRivalUserId = weakestRivalUserId;
            state.DuelTargetUserId = null;

            DuelCandidatesDto duelDto = new DuelCandidatesDto
            {
                WeakestRivalUserId = weakestRivalUserId,
                Candidates = duelCandidates.ToArray()
            };

            try
            {
                weakestRivalPlayer.Callback.OnDuelCandidates(
                    state.MatchId,
                    duelDto);
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

            if (state.CurrentPlayerIndex < 0 ||
                state.CurrentPlayerIndex >= state.Players.Count ||
                state.Players[state.CurrentPlayerIndex].IsEliminated)
            {
                int firstAlive = state.Players.FindIndex(p => !p.IsEliminated);
                if (firstAlive < 0)
                {
                    Logger.WarnFormat(
                        "StartNextRound: no alive players found. MatchId={0}",
                        state.MatchId);
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

            bool lightningStarted = TryStartLightningChallenge(state);
            if (!lightningStarted)
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
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
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
                    "GameplayService.GetConnectionString",
                    new ConfigurationErrorsException(
                        string.Format("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME)));
            }

            return configurationString.ConnectionString;
        }

        private static CoinFlipResolvedDto PerformCoinFlip(MatchRuntimeState state, int weakestRivalUserId)
        {
            int randomValue = RandomGenerator.Next(
                COIN_FLIP_RANDOM_MIN_VALUE,
                COIN_FLIP_RANDOM_MAX_VALUE);

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

            PlayerSummary winnerSummary = new PlayerSummary
            {
                UserId = winner.UserId,
                DisplayName = winner.DisplayName,
                IsOnline = true,
                Avatar = winner.Avatar
            };

            foreach (MatchPlayerRuntime player in state.Players)
            {
                try
                {
                    player.Callback.OnMatchFinished(
                        state.MatchId,
                        winnerSummary);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error al notificar OnMatchFinished.", ex);
                }
            }

            Logger.InfoFormat(
                "Match finished. MatchId={0}, WinnerUserId={1}",
                state.MatchId,
                winner.UserId);

            Matches.TryRemove(state.MatchId, out _);

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

            int randomValue = RandomGenerator.Next(LIGHTNING_RANDOM_MIN_VALUE, LIGHTNING_RANDOM_MAX_VALUE);
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

            int candidateIndex = RandomGenerator.Next(0, candidates.Count);
            MatchPlayerRuntime targetPlayer = candidates[candidateIndex];

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

            state.ActiveSpecialEvent = SpecialEventType.LightningChallenge;
            state.HasSpecialEventThisRound = true;

            state.LightningChallenge = new LightningChallengeState(
                state.MatchId,
                Guid.NewGuid(),
                targetPlayer.UserId,
                LIGHTNING_TOTAL_QUESTIONS,
                TimeSpan.FromSeconds(LIGHTNING_TOTAL_TIME_SECONDS));

            state.SetLightningQuestions(lightningQuestions);

            PlayerSummary targetSummary = new PlayerSummary
            {
                UserId = targetPlayer.UserId,
                DisplayName = targetPlayer.DisplayName,
                IsOnline = true,
                Avatar = targetPlayer.Avatar
            };

            foreach (MatchPlayerRuntime player in state.Players)
            {
                try
                {
                    player.Callback.OnLightningChallengeStarted(
                        state.MatchId,
                        state.LightningChallenge.RoundId,
                        targetSummary,
                        LIGHTNING_TOTAL_QUESTIONS,
                        LIGHTNING_TOTAL_TIME_SECONDS);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error al notificar OnLightningChallengeStarted.", ex);
                }
            }

            QuestionWithAnswersDto firstQuestion = state.GetCurrentLightningQuestion();

            foreach (MatchPlayerRuntime player in state.Players)
            {
                try
                {
                    player.Callback.OnLightningChallengeQuestion(
                        state.MatchId,
                        state.LightningChallenge.RoundId,
                        1,
                        firstQuestion);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error al notificar OnLightningChallengeQuestion.", ex);
                }
            }

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

            PlayerSummary playerSummary = new PlayerSummary
            {
                UserId = currentPlayer.UserId,
                DisplayName = currentPlayer.DisplayName,
                Avatar = currentPlayer.Avatar
            };

            foreach (MatchPlayerRuntime player in state.Players)
            {
                try
                {
                    player.Callback.OnAnswerEvaluated(
                        state.MatchId,
                        playerSummary,
                        result);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error al notificar OnAnswerEvaluated (lightning).", ex);
                }
            }

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

                foreach (MatchPlayerRuntime player in state.Players)
                {
                    try
                    {
                        player.Callback.OnLightningChallengeQuestion(
                            state.MatchId,
                            challenge.RoundId,
                            questionIndex,
                            nextQuestion);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error al notificar OnLightningChallengeQuestion (next).", ex);
                    }
                }
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

            foreach (MatchPlayerRuntime player in state.Players)
            {
                try
                {
                    player.Callback.OnLightningChallengeFinished(
                        state.MatchId,
                        challenge.RoundId,
                        challenge.CorrectAnswers,
                        isSuccess);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error al notificar OnLightningChallengeFinished.", ex);
                }
            }

            if (isSuccess)
            {
                TryAwardLightningWildcard(state, challenge.PlayerId);
            }

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

            MatchPlayerRuntime targetPlayer = state.Players
                .FirstOrDefault(p => p.UserId == playerUserId);

            if (targetPlayer == null)
            {
                Logger.WarnFormat(
                    "{0}: player not found. MatchId={1}, UserId={2}",
                    logContext,
                    state.MatchId,
                    playerUserId);
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
                Logger.Error(logContext + ": error otorgando comodín en BD", ex);
            }

            string description = string.Format(
                descriptionTemplate,
                targetPlayer.DisplayName);

            foreach (MatchPlayerRuntime player in state.Players)
            {
                try
                {
                    player.Callback.OnSpecialEvent(
                        state.MatchId,
                        specialEventCode,
                        description);
                }
                catch (Exception ex)
                {
                    Logger.Warn(logContext + ": error al notificar OnSpecialEvent.", ex);
                }
            }
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

            int probabilityValue = RandomGenerator.Next(EXTRA_WILDCARD_RANDOM_MIN_VALUE, EXTRA_WILDCARD_RANDOM_MAX_VALUE);
            if (probabilityValue >= EXTRA_WILDCARD_PROBABILITY_PERCENT)
            {
                return false;
            }

            int candidateIndex = RandomGenerator.Next(0, candidates.Count);
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

    internal sealed class MatchRuntimeState
    {
        private readonly List<QuestionWithAnswersDto> _lightningQuestions =
            new List<QuestionWithAnswersDto>();

        private int _currentLightningQuestionIndex;

        public MatchRuntimeState(Guid matchId)
        {
            MatchId = matchId;
            Questions = new Queue<QuestionWithAnswersDto>();
            QuestionsById = new Dictionary<int, QuestionWithAnswersDto>();
            Players = new List<MatchPlayerRuntime>();
            SyncRoot = new object();
            QuestionsAskedThisRound = 0;
            RoundNumber = 1;
            IsInVotePhase = false;
            IsInDuelPhase = false;
            WeakestRivalUserId = null;
            DuelTargetUserId = null;
            VotersThisRound = new HashSet<int>();
            VotesThisRound = new Dictionary<int, int?>();

            IsFinished = false;
            WinnerUserId = null;

            ActiveSpecialEvent = SpecialEventType.None;
            WildcardMatchId = 0;

            HasSpecialEventThisRound = false;
        }

        public Guid MatchId { get; }

        public byte Difficulty { get; private set; }

        public string LocaleCode { get; private set; }

        public Queue<QuestionWithAnswersDto> Questions { get; }

        public Dictionary<int, QuestionWithAnswersDto> QuestionsById { get; }

        public List<MatchPlayerRuntime> Players { get; }

        public int CurrentPlayerIndex { get; set; }

        public int CurrentStreak { get; set; }

        public decimal CurrentChain { get; set; }

        public decimal BankedPoints { get; set; }

        public int CurrentQuestionId { get; set; }

        public bool IsInitialized { get; private set; }

        public object SyncRoot { get; }

        public int QuestionsAskedThisRound { get; set; }

        public int RoundNumber { get; set; }

        public bool IsInVotePhase { get; set; }

        public bool IsInDuelPhase { get; set; }

        public int? WeakestRivalUserId { get; set; }

        public int? DuelTargetUserId { get; set; }

        public HashSet<int> VotersThisRound { get; }

        public Dictionary<int, int?> VotesThisRound { get; }

        public bool IsFinished { get; set; }

        public int? WinnerUserId { get; set; }

        public SpecialEventType ActiveSpecialEvent { get; set; }

        public LightningChallengeState LightningChallenge { get; set; }

        public int WildcardMatchId { get; set; }

        public bool HasSpecialEventThisRound { get; set; }

        public bool IsLightningActive =>
            ActiveSpecialEvent == SpecialEventType.LightningChallenge &&
            LightningChallenge != null;

        public void Initialize(byte difficulty, string localeCode, List<QuestionWithAnswersDto> questions)
        {
            Difficulty = difficulty;
            LocaleCode = localeCode;

            Questions.Clear();
            QuestionsById.Clear();

            foreach (QuestionWithAnswersDto question in questions)
            {
                Questions.Enqueue(question);
                QuestionsById[question.QuestionId] = question;
            }

            CurrentPlayerIndex = 0;
            CurrentStreak = 0;
            CurrentChain = 0m;
            BankedPoints = 0m;
            CurrentQuestionId = 0;
            IsInitialized = true;

            QuestionsAskedThisRound = 0;
            RoundNumber = 1;
            IsInVotePhase = false;
            IsInDuelPhase = false;
            WeakestRivalUserId = null;
            DuelTargetUserId = null;
            VotersThisRound.Clear();
            VotesThisRound.Clear();

            IsFinished = false;
            WinnerUserId = null;

            HasSpecialEventThisRound = false;

            ResetLightningChallenge();
        }

        public MatchPlayerRuntime GetCurrentPlayer()
        {
            if (Players.Count == 0)
            {
                return null;
            }

            if (CurrentPlayerIndex < 0 || CurrentPlayerIndex >= Players.Count)
            {
                CurrentPlayerIndex = 0;
            }

            return Players[CurrentPlayerIndex];
        }

        public void AdvanceTurn()
        {
            if (Players.Count == 0)
            {
                return;
            }

            int nextIndex = CurrentPlayerIndex;

            if (!IsInDuelPhase || !WeakestRivalUserId.HasValue || !DuelTargetUserId.HasValue)
            {
                for (int index = 0; index < Players.Count; index++)
                {
                    nextIndex++;
                    if (nextIndex >= Players.Count)
                    {
                        nextIndex = 0;
                    }

                    if (!Players[nextIndex].IsEliminated)
                    {
                        CurrentPlayerIndex = nextIndex;
                        return;
                    }
                }

                return;
            }

            HashSet<int> duelIds = new HashSet<int>
            {
                WeakestRivalUserId.Value,
                DuelTargetUserId.Value
            };

            for (int index = 0; index < Players.Count; index++)
            {
                nextIndex++;
                if (nextIndex >= Players.Count)
                {
                    nextIndex = 0;
                }

                if (!Players[nextIndex].IsEliminated &&
                    duelIds.Contains(Players[nextIndex].UserId))
                {
                    CurrentPlayerIndex = nextIndex;
                    return;
                }
            }
        }

        public void SetLightningQuestions(IEnumerable<QuestionWithAnswersDto> questions)
        {
            _lightningQuestions.Clear();
            _currentLightningQuestionIndex = 0;

            if (questions == null)
            {
                return;
            }

            foreach (QuestionWithAnswersDto question in questions)
            {
                if (question != null)
                {
                    _lightningQuestions.Add(question);
                }
            }
        }

        public QuestionWithAnswersDto GetCurrentLightningQuestion()
        {
            if (_currentLightningQuestionIndex < 0 ||
                _currentLightningQuestionIndex >= _lightningQuestions.Count)
            {
                return null;
            }

            return _lightningQuestions[_currentLightningQuestionIndex];
        }

        public void MoveToNextLightningQuestion()
        {
            if (_currentLightningQuestionIndex < _lightningQuestions.Count)
            {
                _currentLightningQuestionIndex++;
            }
        }

        public void ResetLightningChallenge()
        {
            ActiveSpecialEvent = SpecialEventType.None;
            LightningChallenge = null;
            _lightningQuestions.Clear();
            _currentLightningQuestionIndex = 0;
        }
    }

    internal sealed class MatchPlayerRuntime
    {
        public MatchPlayerRuntime(int userId, string displayName, IGameplayServiceCallback callback)
        {
            UserId = userId;
            DisplayName = displayName;
            Callback = callback;
        }

        public int UserId { get; }

        public string DisplayName { get; }

        public IGameplayServiceCallback Callback { get; set; }

        public bool IsEliminated { get; set; }

        public bool IsWinner { get; set; }

        public AvatarAppearanceDto Avatar { get; set; }
    }
}
