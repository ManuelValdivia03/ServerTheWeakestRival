using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class GameplayMatchLogic
    {
        private readonly GameplayEngine engine;

        public GameplayMatchLogic(GameplayEngine engine)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public GameplayJoinMatchResponse JoinMatch(GameplayJoinMatchRequest request)
        {
            engine.ValidateNotNullRequest(request);
            engine.ValidateMatchId(request.MatchId);

            int userId = engine.Authenticate(request.Token);

            IGameplayServiceCallback callback =
                OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

            MatchRuntimeState state = engine.GetOrCreateMatch(request.MatchId);

            engine.JoinMatchInternal(state, request.MatchId, userId, callback);

            return new GameplayJoinMatchResponse
            {
                Accepted = true
            };
        }

        public GameplayStartMatchResponse StartMatch(GameplayStartMatchRequest request)
        {
            engine.ValidateNotNullRequest(request);
            engine.ValidateMatchId(request.MatchId);

            if (request.Difficulty <= 0)
            {
                throw GameplayEngine.ThrowFault(GameplayEngine.ERROR_INVALID_REQUEST, "Difficulty must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.LocaleCode))
            {
                throw GameplayEngine.ThrowFault(GameplayEngine.ERROR_INVALID_REQUEST, "LocaleCode is required.");
            }

            int userId = engine.Authenticate(request.Token);

            MatchRuntimeState state = engine.GetOrCreateMatch(request.MatchId);

            engine.StartMatchInternal(state, request, userId);

            return new GameplayStartMatchResponse
            {
                Started = true
            };
        }

        public ChooseDuelOpponentResponse ChooseDuelOpponent(ChooseDuelOpponentRequest request)
        {
            engine.ValidateNotNullRequest(request);

            if (request.TargetUserId <= 0)
            {
                throw GameplayEngine.ThrowFault(GameplayEngine.ERROR_INVALID_REQUEST, "TargetUserId is required.");
            }

            int userId = engine.Authenticate(request.Token);

            Guid matchId = engine.ResolveMatchIdForUserOrThrow(userId);

            MatchRuntimeState state = engine.GetMatchOrThrow(matchId);

            engine.ChooseDuelOpponentInternal(state, userId, request.TargetUserId);

            return new ChooseDuelOpponentResponse
            {
                Accepted = true
            };
        }
    }
}
