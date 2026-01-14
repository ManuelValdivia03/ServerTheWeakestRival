using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplaySpecialEvents
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplaySpecialEvents));

        internal static bool TryStartBombQuestionEvent(MatchRuntimeState state, MatchPlayerRuntime targetPlayer, int questionId)
        {
            if (questionId <= 0)
            {
                return false;
            }

            if (state.HasSpecialEventThisRound || IsLightningActive(state))
            {
                return false;
            }

            int randomValue = GameplayRandom.Next(
                GameplayEngineConstants.BOMB_QUESTION_RANDOM_MIN_VALUE,
                GameplayEngineConstants.BOMB_QUESTION_RANDOM_MAX_VALUE);

            if (randomValue >= GameplayEngineConstants.BOMB_QUESTION_PROBABILITY_PERCENT)
            {
                return false;
            }

            state.BombQuestionId = questionId;
            state.HasSpecialEventThisRound = true;

            string deltaDisplay = GameplayEngineConstants.BOMB_BANK_DELTA.ToString("0.00", CultureInfo.InvariantCulture);

            string description = string.Format(
                CultureInfo.CurrentCulture,
                GameplayEngineConstants.SPECIAL_EVENT_BOMB_QUESTION_DESCRIPTION_TEMPLATE,
                targetPlayer.DisplayName,
                deltaDisplay);

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, GameplayEngineConstants.SPECIAL_EVENT_BOMB_QUESTION_CODE, description),
                "GameplayEngine.BombQuestion");

            return true;
        }

        internal static void ApplyBombQuestionEffectIfNeeded(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, bool isCorrect)
        {
            if (state.BombQuestionId <= 0 || state.BombQuestionId != state.CurrentQuestionId)
            {
                return;
            }

            decimal previousBank = state.BankedPoints < GameplayEngineConstants.MIN_BANKED_POINTS
                ? GameplayEngineConstants.MIN_BANKED_POINTS
                : state.BankedPoints;

            decimal delta = isCorrect ? GameplayEngineConstants.BOMB_BANK_DELTA : -GameplayEngineConstants.BOMB_BANK_DELTA;

            decimal updatedBank = previousBank + delta;
            if (updatedBank < GameplayEngineConstants.MIN_BANKED_POINTS)
            {
                updatedBank = GameplayEngineConstants.MIN_BANKED_POINTS;
            }

            state.BankedPoints = updatedBank;
            state.BombQuestionId = 0;

            string deltaDisplay = delta.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
            string bankDisplay = state.BankedPoints.ToString("0.00", CultureInfo.InvariantCulture);

            string description = string.Format(
                CultureInfo.CurrentCulture,
                GameplayEngineConstants.SPECIAL_EVENT_BOMB_QUESTION_APPLIED_DESCRIPTION_TEMPLATE,
                currentPlayer.DisplayName,
                deltaDisplay,
                bankDisplay);

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, GameplayEngineConstants.SPECIAL_EVENT_BOMB_QUESTION_APPLIED_CODE, description),
                "GameplayEngine.BombQuestion.Applied");
        }

        internal static bool TryStartSurpriseExamEvent(MatchRuntimeState state)
        {
            if (state == null)
            {
                return false;
            }

            if (state.HasSpecialEventThisRound || IsLightningActive(state) || state.IsInVotePhase || state.IsInDuelPhase)
            {
                return false;
            }

            int[] participantUserIds = GetSurpriseExamParticipantUserIds(state);
            if (participantUserIds.Length <= 0)
            {
                return false;
            }

            if (state.Questions.Count < participantUserIds.Length)
            {
                return false;
            }

            int randomValue = GameplayRandom.Next(
                GameplayEngineConstants.SURPRISE_EXAM_RANDOM_MIN_VALUE,
                GameplayEngineConstants.SURPRISE_EXAM_RANDOM_MAX_VALUE);

            if (randomValue >= GameplayEngineConstants.SURPRISE_EXAM_PROBABILITY_PERCENT)
            {
                return false;
            }

            DateTime deadlineUtc = DateTime.UtcNow.AddSeconds(GameplayEngineConstants.SURPRISE_EXAM_TIME_LIMIT_SECONDS);

            state.ActiveSpecialEvent = SpecialEventType.SurpriseExam;
            state.HasSpecialEventThisRound = true;

            var exam = new SurpriseExamState(deadlineUtc);

            state.SurpriseExam = exam;

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(
                    state.MatchId,
                    GameplayEngineConstants.SPECIAL_EVENT_SURPRISE_EXAM_STARTED_CODE,
                    GameplayEngineConstants.SPECIAL_EVENT_SURPRISE_EXAM_STARTED_DESCRIPTION),
                "GameplayEngine.SurpriseExam.Started");

            foreach (int userId in participantUserIds)
            {
                QuestionWithAnswersDto question = state.Questions.Dequeue();

                exam.QuestionIdByUserId[userId] = question.QuestionId;
                exam.PendingUserIds.Add(userId);

                MatchPlayerRuntime runtime = state.Players.FirstOrDefault(p => p != null && p.UserId == userId && !p.IsEliminated);
                if (runtime != null && runtime.Callback != null)
                {
                    TrySendSurpriseExamQuestionToPlayer(state, runtime, question);
                }
            }

            Timer timer = new Timer(
                SurpriseExamTimeoutCallback,
                state.MatchId,
                TimeSpan.FromSeconds(GameplayEngineConstants.SURPRISE_EXAM_TIME_LIMIT_SECONDS),
                Timeout.InfiniteTimeSpan);

            exam.AttachTimer(timer);

            return true;
        }

        private static int[] GetSurpriseExamParticipantUserIds(MatchRuntimeState state)
        {
            if (state == null)
            {
                return Array.Empty<int>();
            }

            if (GameplayMatchRegistry.TryGetExpectedPlayers(state.MatchId, out ConcurrentDictionary<int, byte> expected) &&
                expected != null &&
                expected.Count > 0)
            {
                return expected.Keys
                    .Where(id => id > 0)
                    .Where(id =>
                    {
                        MatchPlayerRuntime runtime = state.Players.FirstOrDefault(p => p != null && p.UserId == id);
                        return runtime == null || !runtime.IsEliminated;
                    })
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();
            }

            return state.Players
                .Where(p => p != null && !p.IsEliminated)
                .Select(p => p.UserId)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
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
                    GameplayBroadcaster.BuildPlayerSummary(player, isOnline: true),
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

            if (!GameplayMatchRegistry.TryGetMatch(matchId, out MatchRuntimeState state) || state == null)
            {
                return;
            }

            lock (state.SyncRoot)
            {
                if (!state.IsSurpriseExamActive)
                {
                    return;
                }

                ResolveSurpriseExam(state);
            }
        }

        internal static AnswerResult HandleSurpriseExamSubmitAnswer(MatchRuntimeState state, int userId, SubmitAnswerRequest request)
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
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Invalid question for SurpriseExam.");
            }

            if (exam.IsCorrectByUserId.ContainsKey(userId))
            {
                return fallback;
            }

            if (!state.QuestionsById.TryGetValue(request.QuestionId, out QuestionWithAnswersDto question))
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Question not found for SurpriseExam.");
            }

            bool isCorrect = GameplayTurnFlow.EvaluateAnswerOrThrow(question, request.AnswerText);

            exam.IsCorrectByUserId[userId] = isCorrect;
            exam.PendingUserIds.Remove(userId);

            MatchPlayerRuntime answeringPlayer = state.Players.FirstOrDefault(p => p.UserId == userId);

            if (answeringPlayer != null && answeringPlayer.Callback != null)
            {
                try
                {
                    answeringPlayer.Callback.OnAnswerEvaluated(
                        state.MatchId,
                        GameplayBroadcaster.BuildPlayerSummary(answeringPlayer, isOnline: true),
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
                ResolveSurpriseExam(state);
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

        private static void ResolveSurpriseExam(MatchRuntimeState state)
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

            decimal previousBank = state.BankedPoints < GameplayEngineConstants.MIN_BANKED_POINTS
                ? GameplayEngineConstants.MIN_BANKED_POINTS
                : state.BankedPoints;

            decimal delta = allCorrect
                ? GameplayEngineConstants.SURPRISE_EXAM_SUCCESS_BONUS
                : -GameplayEngineConstants.SURPRISE_EXAM_FAILURE_PENALTY;

            decimal updatedBank = previousBank + delta;
            if (updatedBank < GameplayEngineConstants.MIN_BANKED_POINTS)
            {
                updatedBank = GameplayEngineConstants.MIN_BANKED_POINTS;
            }

            state.BankedPoints = updatedBank;

            string outcome = allCorrect
                ? GameplayEngineConstants.SPECIAL_EVENT_SURPRISE_EXAM_OUTCOME_ALL_CORRECT
                : GameplayEngineConstants.SPECIAL_EVENT_SURPRISE_EXAM_OUTCOME_SOME_FAILED;

            string deltaDisplay = delta.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
            string bankDisplay = state.BankedPoints.ToString("0.00", CultureInfo.InvariantCulture);

            string description = string.Format(
                CultureInfo.CurrentCulture,
                GameplayEngineConstants.SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_DESCRIPTION_TEMPLATE,
                outcome,
                correct,
                total,
                deltaDisplay,
                bankDisplay);

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, GameplayEngineConstants.SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_CODE, description),
                "GameplayEngine.SurpriseExam.Resolved");

            BankState bankState = new BankState
            {
                MatchId = state.MatchId,
                CurrentChain = state.CurrentChain,
                BankedPoints = state.BankedPoints
            };

            GameplayBroadcaster.Broadcast(
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
                GameplayVotingAndDuelFlow.StartVotePhase(state);
                return;
            }

            GameplayTurnFlow.SendNextQuestion(state);
        }

        internal static bool IsLightningActive(MatchRuntimeState state)
        {
            return state != null &&
                   state.ActiveSpecialEvent == SpecialEventType.LightningChallenge &&
                   state.LightningChallenge != null;
        }

        internal static void AbortLightningBecauseDisconnected(MatchRuntimeState state, int disconnectedUserId)
        {
            if (!IsLightningActive(state))
            {
                return;
            }

            LightningChallengeState challenge = state.LightningChallenge;
            if (challenge == null || challenge.IsCompleted || challenge.PlayerId != disconnectedUserId)
            {
                return;
            }

            CompleteLightningChallenge(state, isSuccess: false);
        }


        internal static bool TryStartLightningChallenge(MatchRuntimeState state)
        {
            if (state.HasSpecialEventThisRound || IsLightningActive(state))
            {
                return false;
            }

            if (state.Players.Count == 0 || state.Questions.Count < GameplayEngineConstants.LIGHTNING_TOTAL_QUESTIONS)
            {
                return false;
            }

            int randomValue = GameplayRandom.Next(
                GameplayEngineConstants.LIGHTNING_RANDOM_MIN_VALUE,
                GameplayEngineConstants.LIGHTNING_RANDOM_MAX_VALUE);

            if (randomValue >= GameplayEngineConstants.LIGHTNING_PROBABILITY_PERCENT)
            {
                return false;
            }

            List<MatchPlayerRuntime> candidates = state.Players.Where(p => p != null && !p.IsEliminated && p.IsOnline).ToList();
            if (candidates.Count == 0)
            {
                return false;
            }

            MatchPlayerRuntime targetPlayer = candidates[GameplayRandom.Next(0, candidates.Count)];
            int targetPlayerIndex = state.Players.FindIndex(p => p.UserId == targetPlayer.UserId);
            if (targetPlayerIndex < 0)
            {
                return false;
            }

            List<QuestionWithAnswersDto> lightningQuestions = new List<QuestionWithAnswersDto>();
            for (int i = 0; i < GameplayEngineConstants.LIGHTNING_TOTAL_QUESTIONS; i++)
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
                GameplayEngineConstants.LIGHTNING_TOTAL_QUESTIONS,
                TimeSpan.FromSeconds(GameplayEngineConstants.LIGHTNING_TOTAL_TIME_SECONDS));

            state.SetLightningQuestions(lightningQuestions);

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnLightningChallengeStarted(
                    state.MatchId,
                    state.LightningChallenge.RoundId,
                    GameplayBroadcaster.BuildPlayerSummary(targetPlayer, isOnline: true),
                    GameplayEngineConstants.LIGHTNING_TOTAL_QUESTIONS,
                    GameplayEngineConstants.LIGHTNING_TOTAL_TIME_SECONDS),
                "GameplayEngine.Lightning.Started");

            QuestionWithAnswersDto firstQuestion = state.GetCurrentLightningQuestion();

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnLightningChallengeQuestion(
                    state.MatchId,
                    state.LightningChallenge.RoundId,
                    1,
                    firstQuestion),
                "GameplayEngine.Lightning.Question");

            return true;
        }

        internal static AnswerResult HandleLightningSubmitAnswer(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, SubmitAnswerRequest request)
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

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnAnswerEvaluated(state.MatchId, GameplayBroadcaster.BuildPlayerSummary(currentPlayer, isOnline: true), result),
                "GameplayEngine.Lightning.Answer");

            if (challenge.RemainingQuestions <= 0)
            {
                bool isSuccess = challenge.CorrectAnswers == GameplayEngineConstants.LIGHTNING_TOTAL_QUESTIONS;
                CompleteLightningChallenge(state, isSuccess);
                return result;
            }

            state.MoveToNextLightningQuestion();

            QuestionWithAnswersDto nextQuestion = state.GetCurrentLightningQuestion();
            int questionIndex = GameplayEngineConstants.LIGHTNING_TOTAL_QUESTIONS - challenge.RemainingQuestions;

            GameplayBroadcaster.Broadcast(
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

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnLightningChallengeFinished(state.MatchId, challenge.RoundId, challenge.CorrectAnswers, isSuccess),
                "GameplayEngine.Lightning.Finished");

            if (isSuccess)
            {
                TryAwardLightningWildcard(state, challenge.PlayerId);
            }

            state.RestoreTurnAfterLightning();
            state.ResetLightningChallenge();

            GameplayTurnFlow.SendNextQuestion(state);
        }

        internal static bool TryStartExtraWildcardEvent(MatchRuntimeState state)
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

            int probabilityValue = GameplayRandom.Next(
                GameplayEngineConstants.EXTRA_WILDCARD_RANDOM_MIN_VALUE,
                GameplayEngineConstants.EXTRA_WILDCARD_RANDOM_MAX_VALUE);

            if (probabilityValue >= GameplayEngineConstants.EXTRA_WILDCARD_PROBABILITY_PERCENT)
            {
                return false;
            }

            MatchPlayerRuntime targetPlayer = candidates[GameplayRandom.Next(0, candidates.Count)];

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

            string description = string.Format(
                CultureInfo.CurrentCulture,
                GameplayEngineConstants.SPECIAL_EVENT_LIGHTNING_WILDCARD_DESCRIPTION_TEMPLATE,
                targetPlayer.DisplayName);

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, GameplayEngineConstants.SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE, description),
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

            string description = string.Format(
                CultureInfo.CurrentCulture,
                GameplayEngineConstants.SPECIAL_EVENT_EXTRA_WILDCARD_DESCRIPTION_TEMPLATE,
                targetPlayer.DisplayName);

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, GameplayEngineConstants.SPECIAL_EVENT_EXTRA_WILDCARD_CODE, description),
                "GameplayEngine.SpecialEvent.ExtraWildcard");
        }

        internal static bool TryStartDarkModeEvent(MatchRuntimeState state)
        {
            if (state == null)
            {
                return false;
            }

            if (state.HasSpecialEventThisRound || IsLightningActive(state) || state.IsInVotePhase || state.IsInDuelPhase)
            {
                return false;
            }

            int randomValue = GameplayRandom.Next(
                GameplayEngineConstants.DARK_MODE_RANDOM_MIN_VALUE,
                GameplayEngineConstants.DARK_MODE_RANDOM_MAX_VALUE);

            if (randomValue >= GameplayEngineConstants.DARK_MODE_PROBABILITY_PERCENT)
            {
                return false;
            }

            state.IsDarkModeActive = true;
            state.DarkModeRoundNumber = state.RoundNumber;
            state.HasSpecialEventThisRound = true;

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(
                    state.MatchId,
                    GameplayEngineConstants.SPECIAL_EVENT_DARK_MODE_STARTED_CODE,
                    GameplayEngineConstants.SPECIAL_EVENT_DARK_MODE_STARTED_DESCRIPTION),
                "GameplayEngine.DarkMode.Started");

            return true;
        }

        internal static void EndDarkModeIfActive(MatchRuntimeState state)
        {
            if (state == null || !state.IsDarkModeActive)
            {
                return;
            }

            NotifyVotersAboutTheirVote(state);

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(
                    state.MatchId,
                    GameplayEngineConstants.SPECIAL_EVENT_DARK_MODE_ENDED_CODE,
                    GameplayEngineConstants.SPECIAL_EVENT_DARK_MODE_ENDED_DESCRIPTION),
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
                        GameplayEngineConstants.SPECIAL_EVENT_DARK_MODE_VOTE_REVEAL_CODE,
                        string.Format(
                            CultureInfo.CurrentCulture,
                            GameplayEngineConstants.SPECIAL_EVENT_DARK_MODE_VOTE_REVEAL_DESCRIPTION_TEMPLATE,
                            targetDisplayName));
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
                return GameplayEngineConstants.DARK_MODE_NO_VOTE_DISPLAY_NAME;
            }

            MatchPlayerRuntime target = state.Players.FirstOrDefault(p => p != null && p.UserId == targetUserId.Value);
            if (target != null && !string.IsNullOrWhiteSpace(target.DisplayName))
            {
                return target.DisplayName;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                GameplayEngineConstants.DARK_MODE_FALLBACK_PLAYER_NAME_TEMPLATE,
                targetUserId.Value);
        }
    }
}
