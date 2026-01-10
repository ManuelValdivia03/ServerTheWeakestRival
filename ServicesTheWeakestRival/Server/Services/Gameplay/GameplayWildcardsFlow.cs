using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Globalization;
using System.Linq;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplayWildcardsFlow
    {
        internal static void ApplyWildcardLocked(MatchRuntimeState state, MatchPlayerRuntime currentPlayer, string wildcardCode)
        {
            switch (wildcardCode)
            {
                case GameplayEngineConstants.WILDCARD_CHANGE_Q:
                    ApplyWildcardChangeQuestion(state, currentPlayer);
                    return;

                case GameplayEngineConstants.WILDCARD_PASS_Q:
                    ApplyWildcardPassQuestion(state, currentPlayer);
                    return;

                case GameplayEngineConstants.WILDCARD_SHIELD:
                    ApplyWildcardShield(currentPlayer);
                    return;

                case GameplayEngineConstants.WILDCARD_FORCED_BANK:
                    ApplyWildcardForcedBank(state);
                    return;

                case GameplayEngineConstants.WILDCARD_DOUBLE:
                    ApplyWildcardDouble(currentPlayer);
                    return;

                case GameplayEngineConstants.WILDCARD_BLOCK:
                    ApplyWildcardBlock(state, currentPlayer);
                    return;

                case GameplayEngineConstants.WILDCARD_SABOTAGE:
                    ApplyWildcardSabotage(state, currentPlayer);
                    return;

                case GameplayEngineConstants.WILDCARD_EXTRA_TIME:
                    ApplyWildcardExtraTime(state, currentPlayer);
                    return;

                default:
                    throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Código de comodín inválido.");
            }
        }

        private static void ApplyWildcardChangeQuestion(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            if (state.Questions.Count <= 0)
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_NO_QUESTIONS, GameplayEngineConstants.ERROR_NO_QUESTIONS_MESSAGE);
            }

            QuestionWithAnswersDto nextQuestion = state.Questions.Dequeue();
            state.CurrentQuestionId = nextQuestion.QuestionId;

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnNextQuestion(
                    state.MatchId,
                    GameplayBroadcaster.BuildPlayerSummary(currentPlayer, isOnline: true),
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
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Target inválido para PASS_Q.");
            }

            state.CurrentPlayerIndex = targetIndex;

            GameplayBroadcaster.BroadcastTurnOrderInitialized(state);

            if (!state.QuestionsById.TryGetValue(state.CurrentQuestionId, out QuestionWithAnswersDto currentQuestion))
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Current question not found for PASS_Q.");
            }

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnNextQuestion(
                    state.MatchId,
                    GameplayBroadcaster.BuildPlayerSummary(target, isOnline: true),
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

            GameplayBroadcaster.Broadcast(
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

        private static void ApplyWildcardSabotage(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            MatchPlayerRuntime target = ResolveNextAlivePlayerOrThrow(state, currentPlayer.UserId);

            target.PendingTimeDeltaSeconds -= GameplayEngineConstants.WILDCARD_TIME_PENALTY_SECONDS;

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(
                    state.MatchId,
                    GameplayEngineConstants.SPECIAL_EVENT_TIME_PENALTY_CODE,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        GameplayEngineConstants.SPECIAL_EVENT_TIME_PENALTY_DESCRIPTION_TEMPLATE,
                        target.DisplayName,
                        GameplayEngineConstants.WILDCARD_TIME_PENALTY_SECONDS)),
                "GameplayEngine.Wildcard.Sabotage");
        }

        private static void ApplyWildcardExtraTime(MatchRuntimeState state, MatchPlayerRuntime currentPlayer)
        {
            currentPlayer.PendingTimeDeltaSeconds += GameplayEngineConstants.WILDCARD_TIME_BONUS_SECONDS;

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(
                    state.MatchId,
                    GameplayEngineConstants.SPECIAL_EVENT_TIME_BONUS_CODE,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        GameplayEngineConstants.SPECIAL_EVENT_TIME_BONUS_DESCRIPTION_TEMPLATE,
                        currentPlayer.DisplayName,
                        GameplayEngineConstants.WILDCARD_TIME_BONUS_SECONDS)),
                "GameplayEngine.Wildcard.ExtraTime");
        }

        internal static void BroadcastWildcardUsed(MatchRuntimeState state, MatchPlayerRuntime actor, string wildcardCode)
        {
            string code = string.Format(CultureInfo.InvariantCulture, GameplayEngineConstants.SPECIAL_EVENT_WILDCARD_USED_CODE_TEMPLATE, wildcardCode);
            string description = string.Format(CultureInfo.CurrentCulture, GameplayEngineConstants.SPECIAL_EVENT_WILDCARD_USED_DESCRIPTION_TEMPLATE, actor.DisplayName, wildcardCode);

            GameplayBroadcaster.Broadcast(
                state,
                cb => cb.OnSpecialEvent(state.MatchId, code, description),
                "GameplayEngine.Wildcard.Used");
        }

        private static MatchPlayerRuntime ResolveNextAlivePlayerOrThrow(MatchRuntimeState state, int currentUserId)
        {
            if (state == null || state.Players.Count <= 1)
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "No hay jugador objetivo.");
            }

            int startIndex = state.Players.FindIndex(p => p != null && p.UserId == currentUserId);
            if (startIndex < 0)
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Jugador actual inválido.");
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

            throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "No hay jugador vivo objetivo.");
        }
    }
}
