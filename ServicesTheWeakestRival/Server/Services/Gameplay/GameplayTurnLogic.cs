using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class GameplayTurnLogic
    {
        private readonly GameplayEngine _engine;

        public GameplayTurnLogic(GameplayEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public SubmitAnswerResponse SubmitAnswer(SubmitAnswerRequest request)
        {
            _engine.ValidateNotNullRequest(request);
            _engine.ValidateMatchId(request.MatchId);

            int userId = _engine.Authenticate(request.Token);
            MatchRuntimeState state = _engine.GetMatchOrThrow(request.MatchId);

            AnswerResult result = _engine.SubmitAnswerInternal(state, userId, request);

            return new SubmitAnswerResponse
            {
                Result = result
            };
        }

        public BankResponse Bank(BankRequest request)
        {
            _engine.ValidateNotNullRequest(request);
            _engine.ValidateMatchId(request.MatchId);

            int userId = _engine.Authenticate(request.Token);
            MatchRuntimeState state = _engine.GetMatchOrThrow(request.MatchId);

            BankState bank = _engine.BankInternal(state, userId);

            return new BankResponse
            {
                Bank = bank
            };
        }

        public CastVoteResponse CastVote(CastVoteRequest request)
        {
            _engine.ValidateNotNullRequest(request);
            _engine.ValidateMatchId(request.MatchId);

            int userId = _engine.Authenticate(request.Token);
            MatchRuntimeState state = _engine.GetMatchOrThrow(request.MatchId);

            bool accepted = _engine.CastVoteInternal(state, userId, request.TargetUserId);

            return new CastVoteResponse
            {
                Accepted = accepted
            };
        }
    }
}
