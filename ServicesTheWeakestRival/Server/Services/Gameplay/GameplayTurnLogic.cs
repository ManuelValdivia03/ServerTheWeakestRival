using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class GameplayTurnLogic
    {
        public GameplayTurnLogic()
        {
        }

        public SubmitAnswerResponse SubmitAnswer(SubmitAnswerRequest request)
        {
            GameplayEngine.ValidateNotNullRequest(request);
            GameplayEngine.ValidateMatchId(request.MatchId);

            int userId = GameplayEngine.Authenticate(request.Token);
            MatchRuntimeState state = GameplayEngine.GetMatchOrThrow(request.MatchId);

            AnswerResult result = GameplayEngine.SubmitAnswerInternal(state, userId, request);

            return new SubmitAnswerResponse
            {
                Result = result
            };
        }

        public BankResponse Bank(BankRequest request)
        {
            GameplayEngine.ValidateNotNullRequest(request);
            GameplayEngine.ValidateMatchId(request.MatchId);

            int userId = GameplayEngine.Authenticate(request.Token);
            MatchRuntimeState state = GameplayEngine.GetMatchOrThrow(request.MatchId);

            BankState bank = GameplayEngine.BankInternal(state, userId);

            return new BankResponse
            {
                Bank = bank
            };
        }

        public CastVoteResponse CastVote(CastVoteRequest request)
        {
            GameplayEngine.ValidateNotNullRequest(request);
            GameplayEngine.ValidateMatchId(request.MatchId);

            int userId = GameplayEngine.Authenticate(request.Token);
            MatchRuntimeState state = GameplayEngine.GetMatchOrThrow(request.MatchId);

            bool accepted = GameplayEngine.CastVoteInternal(state, userId, request.TargetUserId);

            return new CastVoteResponse
            {
                Accepted = accepted
            };
        }
    }
}
