using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Gameplay;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class GameplayEngine
    {
        internal static readonly GameplayEngine Shared = new GameplayEngine();

        internal const string ERROR_INVALID_REQUEST = GameplayEngineConstants.ERROR_INVALID_REQUEST;
        internal const string ERROR_UNEXPECTED = GameplayEngineConstants.ERROR_UNEXPECTED;

        internal const string MESSAGE_UNEXPECTED_ERROR = GameplayEngineConstants.MESSAGE_UNEXPECTED_ERROR;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayEngine));

        private const string CTX_GET_QUESTIONS = "GameplayEngine.GetQuestions";
        private const string MESSAGE_REQUEST_IS_NULL = "Request is null.";
        private const string MESSAGE_MATCH_ID_REQUIRED = "MatchId is required.";

        private const int MIN_ANSWERS_REQUIRED = 2;
        private const int EXPECTED_CORRECT_ANSWERS = 1;
        private const byte DISPLAY_ORDER_START = 1;

        private static readonly Random RandomGenerator = new Random();
        private static readonly object RandomSyncRoot = new object();

        private GameplayEngine()
        {
        }

        internal static GetQuestionsResponse GetQuestions(GetQuestionsRequest request)
        {
            GameplayDataAccess.ValidateGetQuestionsRequest(request);

            Authenticate(request.Token);

            int maxQuestions = GameplayDataAccess.GetMaxQuestionsOrDefault(request.MaxQuestions);

            try
            {
                List<QuestionWithAnswersDto> questions =
                    GameplayDataAccess.LoadQuestions(request.Difficulty, request.LocaleCode, maxQuestions);

                List<QuestionWithAnswersDto> safeQuestions = questions ?? new List<QuestionWithAnswersDto>();

                ShuffleAnswersForGameplay(safeQuestions);

                return new GetQuestionsResponse
                {
                    Questions = safeQuestions
                };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_DB,
                    GameplayEngineConstants.MESSAGE_DB_ERROR,
                    CTX_GET_QUESTIONS,
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_UNEXPECTED,
                    GameplayEngineConstants.MESSAGE_UNEXPECTED_ERROR,
                    CTX_GET_QUESTIONS,
                    ex);
            }
        }

        internal static MatchRuntimeState GetOrCreateMatch(Guid matchId)
        {
            return GameplayMatchRegistry.GetOrCreateMatch(matchId);
        }

        internal static MatchRuntimeState GetMatchOrThrow(Guid matchId)
        {
            return GameplayMatchRegistry.GetMatchOrThrow(matchId);
        }

        internal static MatchRuntimeState GetMatchByWildcardDbIdOrThrow(int wildcardMatchId)
        {
            return GameplayMatchRegistry.GetMatchByWildcardDbIdOrThrow(wildcardMatchId);
        }

        internal static Guid ResolveMatchIdForUserOrThrow(int userId)
        {
            return GameplayMatchRegistry.ResolveMatchIdForUserOrThrow(userId);
        }

        internal static void JoinMatchInternal(
            MatchRuntimeState state,
            Guid matchId,
            int userId,
            IGameplayServiceCallback callback)
        {
            GameplayMatchFlow.JoinMatchInternal(state, matchId, userId, callback);
        }

        internal static void StartMatchInternal(MatchRuntimeState state, GameplayStartMatchRequest request, int hostUserId)
        {
            GameplayMatchFlow.StartMatchInternal(state, request, hostUserId);
        }

        internal static void InitializeMatchState(
            MatchRuntimeState state,
            GameplayStartMatchRequest request,
            int hostUserId,
            List<QuestionWithAnswersDto> questions)
        {
            GameplayMatchFlow.InitializeMatchState(state, request, hostUserId, questions);
        }

        internal static void ChooseDuelOpponentInternal(MatchRuntimeState state, int userId, int targetUserId)
        {
            GameplayActionsFlow.ChooseDuelOpponentInternal(state, userId, targetUserId);
        }

        internal static AnswerResult SubmitAnswerInternal(MatchRuntimeState state, int userId, SubmitAnswerRequest request)
        {
            return GameplayActionsFlow.SubmitAnswerInternal(state, userId, request);
        }

        internal static BankState BankInternal(MatchRuntimeState state, int userId)
        {
            return GameplayActionsFlow.BankInternal(state, userId);
        }

        internal static bool CastVoteInternal(MatchRuntimeState state, int userId, int? targetUserId)
        {
            return GameplayActionsFlow.CastVoteInternal(state, userId, targetUserId);
        }

        internal static int ApplyWildcardFromDbOrThrow(int wildcardMatchId, int userId, string wildcardCode, int clientRoundNumber)
        {
            return GameplayActionsFlow.ApplyWildcardFromDbOrThrow(wildcardMatchId, userId, wildcardCode, clientRoundNumber);
        }

        internal static int Authenticate(string token)
        {
            return GameplayAuth.Authenticate(token);
        }

        internal static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            return GameplayFaults.ThrowFault(code, message);
        }

        internal static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string message,
            string context,
            Exception ex)
        {
            return GameplayFaults.ThrowTechnicalFault(code, message, context, ex);
        }

        internal static void ValidateNotNullRequest(object request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, MESSAGE_REQUEST_IS_NULL);
            }
        }

        internal static void ValidateMatchId(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, MESSAGE_MATCH_ID_REQUIRED);
            }
        }

        private static void ShuffleAnswersForGameplay(List<QuestionWithAnswersDto> questions)
        {
            if (questions == null || questions.Count == 0)
            {
                return;
            }

            foreach (QuestionWithAnswersDto question in questions)
            {
                if (question == null)
                {
                    continue;
                }

                List<AnswerDto> answers = question.Answers;
                if (answers == null || answers.Count < MIN_ANSWERS_REQUIRED)
                {
                    Logger.WarnFormat(
                        "ShuffleAnswersForGameplay: QuestionId={0} has insufficient answers. Count={1}.",
                        question.QuestionId,
                        answers == null ? 0 : answers.Count);
                    continue;
                }

                int correctCount = 0;
                foreach (AnswerDto answer in answers)
                {
                    if (answer != null && answer.IsCorrect)
                    {
                        correctCount++;
                    }
                }

                if (correctCount != EXPECTED_CORRECT_ANSWERS)
                {
                    Logger.WarnFormat(
                        "ShuffleAnswersForGameplay: QuestionId={0} has invalid correct answers count. Expected={1}, Actual={2}.",
                        question.QuestionId,
                        EXPECTED_CORRECT_ANSWERS,
                        correctCount);
                    continue;
                }

                ShuffleInPlace(answers);

                ReassignDisplayOrder(answers);
            }
        }

        private static void ShuffleInPlace(List<AnswerDto> answers)
        {
            if (answers == null || answers.Count <= 1)
            {
                return;
            }

            lock (RandomSyncRoot)
            {
                for (int i = answers.Count - 1; i > 0; i--)
                {
                    int j = RandomGenerator.Next(i + 1);

                    AnswerDto temp = answers[i];
                    answers[i] = answers[j];
                    answers[j] = temp;
                }
            }
        }

        private static void ReassignDisplayOrder(List<AnswerDto> answers)
        {
            if (answers == null || answers.Count == 0)
            {
                return;
            }

            byte displayOrder = DISPLAY_ORDER_START;

            foreach (AnswerDto answer in answers)
            {
                if (answer != null)
                {
                    answer.DisplayOrder = displayOrder;
                }

                unchecked
                {
                    displayOrder++;
                }
            }
        }
    }
}
