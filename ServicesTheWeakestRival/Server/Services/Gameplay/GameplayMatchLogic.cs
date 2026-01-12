using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    internal sealed class GameplayMatchLogic
    {
        public GameplayMatchLogic()
        {
        }

        public GameplayJoinMatchResponse JoinMatch(GameplayJoinMatchRequest request)
        {
            GameplayEngine.ValidateNotNullRequest(request);
            GameplayEngine.ValidateMatchId(request.MatchId);

            int userId = GameplayEngine.Authenticate(request.Token);

            IGameplayServiceCallback callback =
                OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

            MatchRuntimeState state = GameplayEngine.GetOrCreateMatch(request.MatchId);

            GameplayEngine.JoinMatchInternal(state, request.MatchId, userId, callback);

            return new GameplayJoinMatchResponse
            {
                Accepted = true
            };
        }

        public GameplayStartMatchResponse StartMatch(GameplayStartMatchRequest request)
        {
            GameplayEngine.ValidateNotNullRequest(request);
            GameplayEngine.ValidateMatchId(request.MatchId);

            if (request.Difficulty <= 0)
            {
                throw GameplayEngine.ThrowFault(
                    GameplayEngine.ERROR_INVALID_REQUEST,
                    "Difficulty must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.LocaleCode))
            {
                throw GameplayEngine.ThrowFault(
                    GameplayEngine.ERROR_INVALID_REQUEST,
                    "LocaleCode is required.");
            }

            int userId = GameplayEngine.Authenticate(request.Token);

            MatchRuntimeState state = GameplayEngine.GetOrCreateMatch(request.MatchId);

            GameplayEngine.StartMatchInternal(state, request, userId);

            return new GameplayStartMatchResponse
            {
                Started = true
            };
        }

        public ChooseDuelOpponentResponse ChooseDuelOpponent(ChooseDuelOpponentRequest request)
        {
            GameplayEngine.ValidateNotNullRequest(request);

            if (request.TargetUserId <= 0)
            {
                throw GameplayEngine.ThrowFault(
                    GameplayEngine.ERROR_INVALID_REQUEST,
                    "TargetUserId is required.");
            }

            int userId = GameplayEngine.Authenticate(request.Token);

            Guid matchId = GameplayEngine.ResolveMatchIdForUserOrThrow(userId);

            MatchRuntimeState state = GameplayEngine.GetMatchOrThrow(matchId);

            GameplayEngine.ChooseDuelOpponentInternal(state, userId, request.TargetUserId);

            return new ChooseDuelOpponentResponse
            {
                Accepted = true
            };
        }
    }
}
