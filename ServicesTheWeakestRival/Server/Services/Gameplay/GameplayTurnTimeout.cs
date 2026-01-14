using log4net;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Globalization;
using System.Threading;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal static class GameplayTurnTimeout
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayTurnTimeout));

        private sealed class TimeoutPayload
        {
            public Guid MatchId { get; set; }
            public long Sequence { get; set; }
            public int UserId { get; set; }
            public int QuestionId { get; set; }
        }

        internal static void Arm(MatchRuntimeState state, int userId, int questionId)
        {
            if (state == null || userId <= 0 || questionId <= 0)
            {
                return;
            }

            Cancel(state);

            state.TurnTimeoutUserId = userId;
            state.TurnTimeoutQuestionId = questionId;

            state.TurnSequence = state.TurnSequence + 1;

            var payload = new TimeoutPayload
            {
                MatchId = state.MatchId,
                Sequence = state.TurnSequence,
                UserId = userId,
                QuestionId = questionId
            };

            state.TurnTimeoutTimer = new Timer(
                TimeoutCallback,
                payload,
                TimeSpan.FromSeconds(GameplayEngineConstants.QUESTION_TIME_LIMIT_SECONDS),
                Timeout.InfiniteTimeSpan);
        }

        internal static void Cancel(MatchRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            Timer timer = state.TurnTimeoutTimer;
            state.TurnTimeoutTimer = null;
            state.TurnTimeoutUserId = 0;
            state.TurnTimeoutQuestionId = 0;

            if (timer == null)
            {
                return;
            }

            try
            {
                timer.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warn("GameplayTurnTimeout.Cancel dispose failed.", ex);
            }
        }

        internal static void ForceExpireCurrentTurnIfMatches(MatchRuntimeState state, int userId)
        {
            if (state == null || userId <= 0)
            {
                return;
            }

            MatchPlayerRuntime current = state.GetCurrentPlayer();
            if (current == null || current.UserId != userId)
            {
                return;
            }

            int qid = state.CurrentQuestionId;
            if (qid <= 0)
            {
                return;
            }

            TryApplyTimeout(state, userId, qid);
        }

        private static void TimeoutCallback(object stateObj)
        {
            var payload = stateObj as TimeoutPayload;
            if (payload == null || payload.MatchId == Guid.Empty)
            {
                return;
            }

            if (!GameplayMatchRegistry.TryGetMatch(payload.MatchId, out MatchRuntimeState state) || state == null)
            {
                return;
            }

            lock (state.SyncRoot)
            {
                if (state.IsFinished)
                {
                    return;
                }

                if (state.TurnSequence != payload.Sequence)
                {
                    return;
                }

                if (state.CurrentQuestionId != payload.QuestionId)
                {
                    return;
                }

                MatchPlayerRuntime current = state.GetCurrentPlayer();
                if (current == null || current.UserId != payload.UserId)
                {
                    return;
                }

                TryApplyTimeout(state, payload.UserId, payload.QuestionId);
            }
        }

        private static void TryApplyTimeout(MatchRuntimeState state, int userId, int questionId)
        {
            try
            {
                Cancel(state);

                GameplayActionsFlow.HandleAnswerTimeoutLocked(state, userId, questionId);
            }
            catch (Exception ex)
            {
                Logger.Error("GameplayTurnTimeout.TryApplyTimeout failed.", ex);
            }
        }
    }
}
