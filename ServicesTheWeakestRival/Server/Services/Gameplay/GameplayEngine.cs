// GameplayEngine.cs  (fachada <500 líneas)
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
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

        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayEngine));

        private GameplayEngine()
        {
        }

        internal GetQuestionsResponse GetQuestions(GetQuestionsRequest request)
        {
            GameplayDataAccess.ValidateGetQuestionsRequest(request);

            GameplayAuth.Authenticate(request.Token);

            int maxQuestions = GameplayDataAccess.GetMaxQuestionsOrDefault(request.MaxQuestions);

            try
            {
                List<QuestionWithAnswersDto> questions =
                    GameplayDataAccess.LoadQuestions(request.Difficulty, request.LocaleCode, maxQuestions);

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
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_DB,
                    GameplayEngineConstants.MESSAGE_DB_ERROR,
                    "GameplayEngine.GetQuestions",
                    ex);
            }
            catch (Exception ex)
            {
                throw GameplayFaults.ThrowTechnicalFault(
                    GameplayEngineConstants.ERROR_UNEXPECTED,
                    GameplayEngineConstants.MESSAGE_UNEXPECTED_ERROR,
                    "GameplayEngine.GetQuestions",
                    ex);
            }
        }

        internal MatchRuntimeState GetOrCreateMatch(Guid matchId)
        {
            return GameplayMatchRegistry.GetOrCreateMatch(matchId);
        }

        internal MatchRuntimeState GetMatchOrThrow(Guid matchId)
        {
            return GameplayMatchRegistry.GetMatchOrThrow(matchId);
        }

        internal MatchRuntimeState GetMatchByWildcardDbIdOrThrow(int wildcardMatchId)
        {
            return GameplayMatchRegistry.GetMatchByWildcardDbIdOrThrow(wildcardMatchId);
        }

        internal Guid ResolveMatchIdForUserOrThrow(int userId)
        {
            return GameplayMatchRegistry.ResolveMatchIdForUserOrThrow(userId);
        }

        internal void JoinMatchInternal(
            MatchRuntimeState state,
            Guid matchId,
            int userId,
            IGameplayServiceCallback callback)
        {
            GameplayMatchFlow.JoinMatchInternal(state, matchId, userId, callback);
        }

        internal void StartMatchInternal(MatchRuntimeState state, GameplayStartMatchRequest request, int hostUserId)
        {
            GameplayMatchFlow.StartMatchInternal(state, request, hostUserId);
        }

        internal void InitializeMatchState(
            MatchRuntimeState state,
            GameplayStartMatchRequest request,
            int hostUserId,
            List<QuestionWithAnswersDto> questions)
        {
            GameplayMatchFlow.InitializeMatchState(state, request, hostUserId, questions);
        }

        internal void ChooseDuelOpponentInternal(MatchRuntimeState state, int userId, int targetUserId)
        {
            GameplayActionsFlow.ChooseDuelOpponentInternal(state, userId, targetUserId);
        }

        internal AnswerResult SubmitAnswerInternal(MatchRuntimeState state, int userId, SubmitAnswerRequest request)
        {
            return GameplayActionsFlow.SubmitAnswerInternal(state, userId, request);
        }

        internal BankState BankInternal(MatchRuntimeState state, int userId)
        {
            return GameplayActionsFlow.BankInternal(state, userId);
        }

        internal bool CastVoteInternal(MatchRuntimeState state, int userId, int? targetUserId)
        {
            return GameplayActionsFlow.CastVoteInternal(state, userId, targetUserId);
        }

        internal int ApplyWildcardFromDbOrThrow(int wildcardMatchId, int userId, string wildcardCode, int clientRoundNumber)
        {
            return GameplayActionsFlow.ApplyWildcardFromDbOrThrow(wildcardMatchId, userId, wildcardCode, clientRoundNumber);
        }

        internal void ValidateNotNullRequest(object request)
        {
            if (request == null)
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Request is null.");
            }
        }

        internal void ValidateMatchId(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "MatchId is required.");
            }
        }
    }
}
