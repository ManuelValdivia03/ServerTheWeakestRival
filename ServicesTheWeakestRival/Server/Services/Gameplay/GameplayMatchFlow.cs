using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Gameplay;
using ServicesTheWeakestRival.Server.Services.Logic;
using ServicesTheWeakestRival.Server.Services.Stats;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplayMatchFlow
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayMatchFlow));

        private const string ERROR_MISSING_CALLBACK_MESSAGE = "Missing callback channel.";

        private const string CTX_START_MATCH_INTERNAL = "GameplayEngine.StartMatchInternal";
        private const string CTX_MATCH_FINISHED = "GameplayEngine.MatchFinished";

        internal static void JoinMatchInternal(
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
                throw GameplayFaults.ThrowFault(
                    GameplayEngineConstants.ERROR_INVALID_REQUEST,
                    ERROR_MISSING_CALLBACK_MESSAGE);
            }

            lock (state.SyncRoot)
            {
                bool hasStarted = state.HasStarted || state.IsInitialized;

                EnsureJoinAllowedOrThrow(state, matchId, userId, hasStarted);

                UpsertPlayerCallbackOrAddPlayer(state, userId, callback);

                if (hasStarted && state.IsInitialized)
                {
                    GameplayBroadcaster.TrySendSnapshotToJoiningPlayer(state, userId);
                }
            }

            GameplayMatchRegistry.TrackPlayerMatch(userId, matchId);
            GameplayDisconnectHub.RegisterCurrentSession(matchId, userId, callback);
        }

        private static void EnsureJoinAllowedOrThrow(MatchRuntimeState state, Guid matchId, int userId, bool hasStarted)
        {
            if (!hasStarted)
            {
                return;
            }

            if (IsUserAllowedToRejoinStartedMatch(state, matchId, userId))
            {
                return;
            }

            throw GameplayFaults.ThrowFault(
                GameplayEngineConstants.ERROR_MATCH_ALREADY_STARTED,
                GameplayEngineConstants.ERROR_MATCH_ALREADY_STARTED_MESSAGE);
        }

        private static bool IsUserAllowedToRejoinStartedMatch(MatchRuntimeState state, Guid matchId, int userId)
        {
            if (GameplayMatchRegistry.TryGetExpectedPlayers(matchId, out ConcurrentDictionary<int, byte> expectedPlayers) &&
                expectedPlayers != null &&
                expectedPlayers.Count > 0)
            {
                return expectedPlayers.ContainsKey(userId);
            }

            MatchPlayerRuntime existingRuntime = state.Players.Find(p => p != null && p.UserId == userId);
            return existingRuntime != null;
        }

        private static void UpsertPlayerCallbackOrAddPlayer(
            MatchRuntimeState state,
            int userId,
            IGameplayServiceCallback callback)
        {
            MatchPlayerRuntime existingPlayer = state.Players.Find(p => p != null && p.UserId == userId);
            if (existingPlayer != null)
            {
                if (existingPlayer.IsEliminated)
                {
                    throw GameplayFaults.ThrowFault(
                        GameplayEngineConstants.ERROR_MATCH_ALREADY_STARTED,
                        GameplayEngineConstants.ERROR_MATCH_ALREADY_STARTED_MESSAGE);
                }

                existingPlayer.Callback = callback;
                return;
            }

            string displayName = string.Format(
                CultureInfo.CurrentCulture,
                GameplayEngineConstants.DARK_MODE_FALLBACK_PLAYER_NAME_TEMPLATE,
                userId);

            UserAvatarEntity avatarEntity = GameplayDataAccess.GetAvatarByUserId(userId);

            MatchPlayerRuntime player = new MatchPlayerRuntime(userId, displayName, callback)
            {
                Avatar = GameplayDataAccess.MapAvatar(avatarEntity)
            };

            state.Players.Add(player);
        }

        internal static void StartMatchInternal(MatchRuntimeState state, GameplayStartMatchRequest request, int hostUserId)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            int maxQuestions = GameplayDataAccess.GetMaxQuestionsOrDefault(request.MaxQuestions);

            try
            {
                lock (state.SyncRoot)
                {
                    if (state.HasStarted || state.IsInitialized)
                    {
                        GameplayMatchRegistry.StoreOrMergeExpectedPlayers(request.MatchId, request.ExpectedPlayerUserIds, hostUserId);
                        return;
                    }

                    state.HasStarted = true;

                    try
                    {
                        GameplayMatchRegistry.StoreOrMergeExpectedPlayers(request.MatchId, request.ExpectedPlayerUserIds, hostUserId);

                        List<QuestionWithAnswersDto> questions = LoadQuestionsOrThrow(
                            request.Difficulty,
                            request.LocaleCode,
                            maxQuestions);

                        InitializeMatchState(state, request, hostUserId, questions);

                        StartInitialSpecialEventsAndFirstTurn(state);
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
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_DB,
                    GameplayEngineConstants.MESSAGE_DB_ERROR,
                    CTX_START_MATCH_INTERNAL,
                    ex);
            }
            catch (Exception ex)
            {
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_UNEXPECTED,
                    GameplayEngineConstants.MESSAGE_UNEXPECTED_ERROR,
                    CTX_START_MATCH_INTERNAL,
                    ex);
            }
        }

        private static List<QuestionWithAnswersDto> LoadQuestionsOrThrow(byte difficulty, string localeCode, int maxQuestions)
        {
            List<QuestionWithAnswersDto> questions = GameplayDataAccess.LoadQuestionsWithLocaleFallback(
                difficulty,
                localeCode,
                maxQuestions);

            if (questions == null || questions.Count == 0)
            {
                throw GameplayFaults.ThrowFault(
                    GameplayEngineConstants.ERROR_NO_QUESTIONS,
                    GameplayEngineConstants.ERROR_NO_QUESTIONS_MESSAGE);
            }

            return questions;
        }

        private static void StartInitialSpecialEventsAndFirstTurn(MatchRuntimeState state)
        {
            GameplaySpecialEvents.TryStartDarkModeEvent(state);
            GameplaySpecialEvents.TryStartExtraWildcardEvent(state);

            if (GameplaySpecialEvents.TryStartSurpriseExamEvent(state))
            {
                return;
            }

            bool hasLightningStarted = GameplaySpecialEvents.TryStartLightningChallenge(state);
            if (!hasLightningStarted)
            {
                GameplayTurnFlow.SendNextQuestion(state);
            }
        }

        internal static void InitializeMatchState(
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
                GameplayEngineConstants.INITIAL_BANKED_POINTS);

            state.WildcardMatchId = request.MatchDbId;

            if (state.WildcardMatchId > 0)
            {
                GameplayMatchRegistry.MapWildcardMatchId(state.WildcardMatchId, state.MatchId);
            }

            EnsureHostPlayerRegistered(state, hostUserId);

            ResetRoundStateForStart(state);
            ShufflePlayersForStart(state);

            GameplayBroadcaster.BroadcastTurnOrderInitialized(state);
        }

        internal static void StartNextRound(MatchRuntimeState state)
        {
            List<MatchPlayerRuntime> alivePlayers = GetAlivePlayers(state);

            if (alivePlayers.Count < GameplayEngineConstants.MIN_PLAYERS_TO_CONTINUE)
            {
                FinishMatchWithWinnerIfApplicable(state);
                return;
            }

            ResetRoundStateForNextRound(state);

            EnsureCurrentPlayerIndexPointsToAlivePlayerOrReturn(state);

            GameplaySpecialEvents.TryStartDarkModeEvent(state);

            if (GameplaySpecialEvents.TryStartSurpriseExamEvent(state))
            {
                return;
            }

            bool hasLightningStarted = GameplaySpecialEvents.TryStartLightningChallenge(state);
            if (!hasLightningStarted)
            {
                GameplayTurnFlow.SendNextQuestion(state);
            }
        }

        internal static bool StartFinalPhaseIfApplicable(MatchRuntimeState state)
        {
            if (state == null || state.IsFinished)
            {
                return false;
            }

            if (state.IsInFinalPhase)
            {
                return true;
            }

            List<MatchPlayerRuntime> alivePlayers = GetAlivePlayers(state);
            if (alivePlayers.Count != GameplayEngineConstants.FINAL_PLAYERS_COUNT)
            {
                return false;
            }

            StartFinalPhase(state, alivePlayers);
            return true;
        }

        internal static void ProcessFinalAnswerAndContinue(MatchRuntimeState state, int userId, bool isCorrect)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!state.IsInFinalPhase || state.IsFinished)
            {
                return;
            }

            EnsureFinalPlayerIsTracked(state, userId);

            state.FinalAnsweredByUserId[userId] = state.FinalAnsweredByUserId[userId] + GameplayEngineConstants.FINAL_ANSWER_INCREMENT;

            if (isCorrect)
            {
                state.FinalCorrectByUserId[userId] = state.FinalCorrectByUserId[userId] + GameplayEngineConstants.FINAL_ANSWER_INCREMENT;
            }

            if (TryFinishFinalIfApplicable(state))
            {
                return;
            }

            state.AdvanceTurn();
            EnsureFinalQuestionPool(state);

            GameplayTurnFlow.SendNextQuestion(state);
        }

        private static void StartFinalPhase(MatchRuntimeState state, List<MatchPlayerRuntime> alivePlayers)
        {
            state.IsInVotePhase = false;
            state.IsInDuelPhase = false;

            state.WeakestRivalUserId = null;
            state.DuelTargetUserId = null;
            state.VotersThisRound.Clear();
            state.VotesThisRound.Clear();

            state.QuestionsAskedThisRound = 0;
            state.CurrentChain = 0m;
            state.CurrentStreak = 0;

            state.HasSpecialEventThisRound = false;
            state.BombQuestionId = 0;

            state.IsDarkModeActive = false;
            state.DarkModeRoundNumber = 0;

            state.ResetSurpriseExam();
            state.RestoreTurnAfterLightning();
            state.ResetLightningChallenge();

            state.ActiveSpecialEvent = ServicesTheWeakestRival.Contracts.Enums.SpecialEventType.None;

            state.ResetFinalPhase();
            state.IsInFinalPhase = true;

            foreach (MatchPlayerRuntime player in alivePlayers)
            {
                if (player == null)
                {
                    continue;
                }

                state.FinalAnsweredByUserId[player.UserId] = 0;
                state.FinalCorrectByUserId[player.UserId] = 0;
            }

            EnsureCurrentPlayerIndexPointsToAlivePlayerOrReturn(state);

            EnsureFinalQuestionPool(state);
            GameplayTurnFlow.SendNextQuestion(state);
        }

        private static bool TryFinishFinalIfApplicable(MatchRuntimeState state)
        {
            List<MatchPlayerRuntime> alivePlayers = GetAlivePlayers(state);
            if (alivePlayers.Count != GameplayEngineConstants.FINAL_PLAYERS_COUNT)
            {
                return false;
            }

            int playerAId = alivePlayers[0].UserId;
            int playerBId = alivePlayers[1].UserId;

            EnsureFinalPlayerIsTracked(state, playerAId);
            EnsureFinalPlayerIsTracked(state, playerBId);

            int answeredA = state.FinalAnsweredByUserId[playerAId];
            int answeredB = state.FinalAnsweredByUserId[playerBId];

            if (!state.IsFinalSuddenDeath)
            {
                if (answeredA < GameplayEngineConstants.FINAL_QUESTIONS_PER_PLAYER ||
                    answeredB < GameplayEngineConstants.FINAL_QUESTIONS_PER_PLAYER)
                {
                    return false;
                }

                int correctA = state.FinalCorrectByUserId[playerAId];
                int correctB = state.FinalCorrectByUserId[playerBId];

                if (correctA == correctB)
                {
                    state.IsFinalSuddenDeath = true;
                    return false;
                }

                int winnerUserId = correctA > correctB ? playerAId : playerBId;
                FinishMatchByWinnerUserId(state, winnerUserId);
                return true;
            }

            if (answeredA == answeredB)
            {
                int correctA = state.FinalCorrectByUserId[playerAId];
                int correctB = state.FinalCorrectByUserId[playerBId];

                if (correctA != correctB)
                {
                    int winnerUserId = correctA > correctB ? playerAId : playerBId;
                    FinishMatchByWinnerUserId(state, winnerUserId);
                    return true;
                }
            }

            return false;
        }

        private static void EnsureFinalQuestionPool(MatchRuntimeState state)
        {
            if (state.Questions.Count >= GameplayEngineConstants.FINAL_MIN_QUEUE_QUESTIONS)
            {
                return;
            }

            List<QuestionWithAnswersDto> batch = GameplayDataAccess.LoadQuestionsWithLocaleFallback(
                state.Difficulty,
                state.LocaleCode,
                GameplayEngineConstants.FINAL_REFILL_BATCH_SIZE);

            if (batch == null || batch.Count == 0)
            {
                return;
            }

            foreach (QuestionWithAnswersDto question in batch)
            {
                if (question == null)
                {
                    continue;
                }

                if (state.QuestionsById.ContainsKey(question.QuestionId))
                {
                    continue;
                }

                state.QuestionsById[question.QuestionId] = question;
                state.Questions.Enqueue(question);
            }
        }

        private static void EnsureFinalPlayerIsTracked(MatchRuntimeState state, int userId)
        {
            if (!state.FinalAnsweredByUserId.ContainsKey(userId))
            {
                state.FinalAnsweredByUserId[userId] = 0;
            }

            if (!state.FinalCorrectByUserId.ContainsKey(userId))
            {
                state.FinalCorrectByUserId[userId] = 0;
            }
        }

        private static List<MatchPlayerRuntime> GetAlivePlayers(MatchRuntimeState state)
        {
            if (state == null)
            {
                return new List<MatchPlayerRuntime>();
            }

            return state.Players
                .Where(p => p != null && !p.IsEliminated)
                .ToList();
        }

        private static void ResetRoundStateForNextRound(MatchRuntimeState state)
        {
            state.RoundNumber++;
            state.QuestionsAskedThisRound = 0;
            state.CurrentChain = 0m;
            state.CurrentStreak = 0;
            state.IsInVotePhase = false;
            state.IsInDuelPhase = false;

            state.ResetFinalPhase();

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
        }

        private static void EnsureCurrentPlayerIndexPointsToAlivePlayerOrReturn(MatchRuntimeState state)
        {
            if (state.CurrentPlayerIndex >= 0 &&
                state.CurrentPlayerIndex < state.Players.Count &&
                state.Players[state.CurrentPlayerIndex] != null &&
                !state.Players[state.CurrentPlayerIndex].IsEliminated)
            {
                return;
            }

            int firstAlive = state.Players.FindIndex(p => p != null && !p.IsEliminated);
            if (firstAlive < 0)
            {
                return;
            }

            state.CurrentPlayerIndex = firstAlive;
        }

        internal static void FinishMatchWithWinnerIfApplicable(MatchRuntimeState state)
        {
            if (state == null || state.IsFinished)
            {
                return;
            }

            List<MatchPlayerRuntime> alivePlayers = GetAlivePlayers(state);

            if (alivePlayers.Count != 1)
            {
                return;
            }

            FinishMatchByWinnerUserId(state, alivePlayers[0].UserId);
        }

        private static void FinishMatchByWinnerUserId(MatchRuntimeState state, int winnerUserId)
        {
            MatchPlayerRuntime winner = state.Players.FirstOrDefault(p => p != null && p.UserId == winnerUserId);
            if (winner == null)
            {
                return;
            }

            state.IsFinished = true;
            state.WinnerUserId = winner.UserId;
            winner.IsWinner = true;

            state.ResetSurpriseExam();

            int matchDbId = state.WildcardMatchId;

            if (matchDbId > 0)
            {
                List<int> participantUserIds = state.Players
                    .Where(p => p != null)
                    .Select(p => p.UserId)
                    .Distinct()
                    .ToList();

                StatsMatchResultsWriter.TryPersistFinalResults(
                    matchDbId,
                    winner.UserId,
                    state.BankedPoints,
                    participantUserIds);
            }

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnMatchFinished(state.MatchId, GameplayBroadcaster.BuildPlayerSummary(winner, isOnline: true)),
                CTX_MATCH_FINISHED);

            GameplayDisconnectHub.CleanupMatch(state.MatchId);
            GameplayMatchRegistry.CleanupFinishedMatch(state);
        }

        private static void EnsureHostPlayerRegistered(MatchRuntimeState state, int userId)
        {
            if (state.Players.Count != 0)
            {
                return;
            }

            IGameplayServiceCallback callback =
                OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

            string displayName = string.Format(
                CultureInfo.CurrentCulture,
                GameplayEngineConstants.DARK_MODE_FALLBACK_PLAYER_NAME_TEMPLATE,
                userId);

            UserAvatarEntity avatarEntity = GameplayDataAccess.GetAvatarByUserId(userId);

            state.Players.Add(new MatchPlayerRuntime(userId, displayName, callback)
            {
                Avatar = GameplayDataAccess.MapAvatar(avatarEntity)
            });

            GameplayMatchRegistry.TrackPlayerMatch(userId, state.MatchId);
        }

        private static void ResetRoundStateForStart(MatchRuntimeState state)
        {
            state.CurrentPlayerIndex = 0;
            state.QuestionsAskedThisRound = 0;
            state.RoundNumber = 1;
            state.IsInVotePhase = false;
            state.IsInDuelPhase = false;

            state.ResetFinalPhase();

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

        private static void ShufflePlayersForStart(MatchRuntimeState state)
        {
            if (state.Players.Count <= 1)
            {
                state.CurrentPlayerIndex = 0;
                return;
            }

            for (int i = state.Players.Count - 1; i > 0; i--)
            {
                int j = GameplayRandom.Next(0, i + 1);

                MatchPlayerRuntime temp = state.Players[i];
                state.Players[i] = state.Players[j];
                state.Players[j] = temp;
            }

            state.CurrentPlayerIndex = 0;
        }
    }
}
