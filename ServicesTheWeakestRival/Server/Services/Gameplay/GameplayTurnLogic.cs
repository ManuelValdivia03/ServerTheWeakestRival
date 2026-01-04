using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class GameplayTurnLogic
    {
        private readonly GameplayEngine engine;

        public GameplayTurnLogic(GameplayEngine engine)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public SubmitAnswerResponse SubmitAnswer(SubmitAnswerRequest request)
        {
            engine.ValidateNotNullRequest(request);
            engine.ValidateMatchId(request.MatchId);

            int userId = engine.Authenticate(request.Token);
            MatchRuntimeState state = engine.GetMatchOrThrow(request.MatchId);

            AnswerResult result = engine.SubmitAnswerInternal(state, userId, request);

            return new SubmitAnswerResponse
            {
                Result = result
            };
        }

        public BankResponse Bank(BankRequest request)
        {
            engine.ValidateNotNullRequest(request);
            engine.ValidateMatchId(request.MatchId);

            int userId = engine.Authenticate(request.Token);
            MatchRuntimeState state = engine.GetMatchOrThrow(request.MatchId);

            BankState bank = engine.BankInternal(state, userId);

            return new BankResponse
            {
                Bank = bank
            };
        }

        public CastVoteResponse CastVote(CastVoteRequest request)
        {
            engine.ValidateNotNullRequest(request);
            engine.ValidateMatchId(request.MatchId);

            int userId = engine.Authenticate(request.Token);
            MatchRuntimeState state = engine.GetMatchOrThrow(request.MatchId);

            bool accepted = engine.CastVoteInternal(state, userId, request.TargetUserId);

            return new CastVoteResponse
            {
                Accepted = accepted
            };
        }
    }
}
