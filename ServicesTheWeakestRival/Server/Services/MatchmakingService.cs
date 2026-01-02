using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Matchmaking;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class MatchmakingService : IMatchmakingService
    {
        private readonly MatchmakingLogic _logic;

        public MatchmakingService()
        {
            _logic = new MatchmakingLogic();
        }

        public CreateMatchResponse CreateMatch(CreateMatchRequest request)
        {
            return _logic.CreateMatch(request);
        }

        public JoinMatchResponse JoinMatch(JoinMatchRequest request)
        {
            return _logic.JoinMatch(request);
        }

        public void LeaveMatch(LeaveMatchRequest request)
        {
            _logic.LeaveMatch(request);

        }

        public StartMatchResponse StartMatch(StartMatchRequest request)
        {
            return _logic.StartMatch(request);
        }

        public ListOpenMatchesResponse ListOpenMatches(ListOpenMatchesRequest request)
        {
            return _logic.ListOpenMatches(request);
        }
    }
}
