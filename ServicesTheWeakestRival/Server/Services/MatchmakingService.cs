using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class MatchmakingService : IMatchmakingService
    {
        private static readonly ConcurrentDictionary<Guid, IMatchmakingClientCallback> Cbs =
            new ConcurrentDictionary<Guid, IMatchmakingClientCallback>();

        public CreateMatchResponse CreateMatch(CreateMatchRequest request)
        {
            var cb = OperationContext.Current.GetCallbackChannel<IMatchmakingClientCallback>();
            var match = new MatchInfo
            {
                MatchId = Guid.NewGuid(),
                MatchCode = new Random().Next(100000, 999999).ToString(),
                Players = new List<PlayerSummary>(),
                State = "Waiting"
            };
            Cbs[match.MatchId] = cb;
            cb.OnMatchCreated(match);
            return new CreateMatchResponse { Match = match };
        }

        public JoinMatchResponse JoinMatch(JoinMatchRequest request)
        {
            // Demo: retorna un match vacío
            var match = new MatchInfo
            {
                MatchId = Guid.NewGuid(),
                MatchCode = request.MatchCode,
                Players = new List<PlayerSummary>(),
                State = "Waiting"
            };
            return new JoinMatchResponse { Match = match };
        }

        public void LeaveMatch(LeaveMatchRequest request) { /* TODO */ }

        public StartMatchResponse StartMatch(StartMatchRequest request)
        {
            var match = new MatchInfo { MatchId = request.MatchId, MatchCode = "NA", Players = new List<PlayerSummary>(), State = "InProgress" };
            IMatchmakingClientCallback cb;
            if (Cbs.TryGetValue(request.MatchId, out cb))
            {
                cb.OnMatchStarted(match);
            }
            return new StartMatchResponse { Match = match };
        }

        public ListOpenMatchesResponse ListOpenMatches(ListOpenMatchesRequest request) =>
            new ListOpenMatchesResponse { Matches = new List<MatchInfo>() };
    }
}