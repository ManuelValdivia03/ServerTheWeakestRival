using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class GameplayMatchLogic
    {
        private readonly GameplayEngine _engine;

        public GameplayMatchLogic(GameplayEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public GameplayJoinMatchResponse JoinMatch(GameplayJoinMatchRequest request)
        {
            _engine.ValidateNotNullRequest(request);
            _engine.ValidateMatchId(request.MatchId);

            int userId = _engine.Authenticate(request.Token);

            IGameplayServiceCallback callback =
                OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

            MatchRuntimeState state = _engine.GetOrCreateMatch(request.MatchId);

            _engine.JoinMatchInternal(state, request.MatchId, userId, callback);

            return new GameplayJoinMatchResponse
            {
                Accepted = true
            };
        }

        public GameplayStartMatchResponse StartMatch(GameplayStartMatchRequest request)
        {
            _engine.ValidateNotNullRequest(request);
            _engine.ValidateMatchId(request.MatchId);

            if (request.Difficulty <= 0)
            {
                throw GameplayEngine.ThrowFault(GameplayEngine.ERROR_INVALID_REQUEST, "Difficulty must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.LocaleCode))
            {
                throw GameplayEngine.ThrowFault(GameplayEngine.ERROR_INVALID_REQUEST, "LocaleCode is required.");
            }

            int userId = _engine.Authenticate(request.Token);

            MatchRuntimeState state = _engine.GetOrCreateMatch(request.MatchId);

            _engine.StartMatchInternal(state, request, userId);

            return new GameplayStartMatchResponse
            {
                Started = true
            };
        }

        public ChooseDuelOpponentResponse ChooseDuelOpponent(ChooseDuelOpponentRequest request)
        {
            _engine.ValidateNotNullRequest(request);

            if (request.TargetUserId <= 0)
            {
                throw GameplayEngine.ThrowFault(GameplayEngine.ERROR_INVALID_REQUEST, "TargetUserId is required.");
            }

            int userId = _engine.Authenticate(request.Token);

            Guid matchId = _engine.ResolveMatchIdForUserOrThrow(userId);

            MatchRuntimeState state = _engine.GetMatchOrThrow(matchId);

            _engine.ChooseDuelOpponentInternal(state, userId, request.TargetUserId);

            return new ChooseDuelOpponentResponse
            {
                Accepted = true
            };
        }
    }
}
