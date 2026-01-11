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
                    "Missing callback channel.");
            }

            lock (state.SyncRoot)
            {
                bool hasStarted = state.HasStarted || state.IsInitialized;

                if (hasStarted)
                {
                    if (GameplayMatchRegistry.TryGetExpectedPlayers(matchId, out ConcurrentDictionary<int, byte> expectedPlayers) &&
                        expectedPlayers != null &&
                        expectedPlayers.Count > 0)
                    {
                        if (!expectedPlayers.ContainsKey(userId))
                        {
                            throw GameplayFaults.ThrowFault(
                                GameplayEngineConstants.ERROR_MATCH_ALREADY_STARTED,
                                GameplayEngineConstants.ERROR_MATCH_ALREADY_STARTED_MESSAGE);
                        }
                    }
                    else
                    {
                        MatchPlayerRuntime existingRuntime = state.Players.Find(p => p != null && p.UserId == userId);
                        if (existingRuntime == null)
                        {
                            throw GameplayFaults.ThrowFault(
                                GameplayEngineConstants.ERROR_MATCH_ALREADY_STARTED,
                                GameplayEngineConstants.ERROR_MATCH_ALREADY_STARTED_MESSAGE);
                        }
                    }
                }

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
                }
                else
                {
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

                if (hasStarted && state.IsInitialized)
                {
                    GameplayBroadcaster.TrySendSnapshotToJoiningPlayer(state, userId);
                }
            }

            GameplayMatchRegistry.TrackPlayerMatch(userId, matchId);

            GameplayDisconnectHub.RegisterCurrentSession(matchId, userId, callback);
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

                        List<QuestionWithAnswersDto> questions = GameplayDataAccess.LoadQuestionsWithLocaleFallback(
                            request.Difficulty,
                            request.LocaleCode,
                            maxQuestions);

                        if (questions == null || questions.Count == 0)
                        {
                            throw GameplayFaults.ThrowFault(
                                GameplayEngineConstants.ERROR_NO_QUESTIONS,
                                GameplayEngineConstants.ERROR_NO_QUESTIONS_MESSAGE);
                        }

                        InitializeMatchState(state, request, hostUserId, questions);

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
                    "GameplayEngine.StartMatchInternal",
                    ex);
            }
            catch (Exception ex)
            {
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_UNEXPECTED,
                    GameplayEngineConstants.MESSAGE_UNEXPECTED_ERROR,
                    "GameplayEngine.StartMatchInternal",
                    ex);
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
            List<MatchPlayerRuntime> alivePlayers = state.Players
                .Where(p => !p.IsEliminated)
                .ToList();

            if (alivePlayers.Count < GameplayEngineConstants.MIN_PLAYERS_TO_CONTINUE)
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

        internal static void FinishMatchWithWinnerIfApplicable(MatchRuntimeState state)
        {
            if (state == null || state.IsFinished)
            {
                return;
            }

            List<MatchPlayerRuntime> alivePlayers = state.Players
                .Where(p => p != null && !p.IsEliminated)
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
                "GameplayEngine.MatchFinished");

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
