using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    public interface IMatchmakingClientCallback
    {
        [OperationContract(IsOneWay = true)] void OnMatchCreated(MatchInfo match);
        [OperationContract(IsOneWay = true)] void OnMatchPlayerJoined(Guid matchId, PlayerSummary player);
        [OperationContract(IsOneWay = true)] void OnMatchPlayerLeft(Guid matchId, Guid playerId);
        [OperationContract(IsOneWay = true)] void OnMatchStarted(MatchInfo match);
        [OperationContract(IsOneWay = true)] void OnMatchCancelled(Guid matchId, string reason);
    }

    [ServiceContract(CallbackContract = typeof(IMatchmakingClientCallback))]
    public interface IMatchmakingService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        CreateMatchResponse CreateMatch(CreateMatchRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        JoinMatchResponse JoinMatch(JoinMatchRequest request);

        [OperationContract]
        void LeaveMatch(LeaveMatchRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        StartMatchResponse StartMatch(StartMatchRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        ListOpenMatchesResponse ListOpenMatches(ListOpenMatchesRequest request);
    }
}
