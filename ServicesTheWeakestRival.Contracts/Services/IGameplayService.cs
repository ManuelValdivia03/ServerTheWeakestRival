using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    [ServiceContract(CallbackContract = typeof(IGameplayServiceCallback))]
    public interface IGameplayService
    {
        [OperationContract]
        SubmitAnswerResponse SubmitAnswer(SubmitAnswerRequest request);

        [OperationContract]
        BankResponse Bank(BankRequest request);

        [OperationContract]
        UseLifelineResponse UseLifeline(UseLifelineRequest request);

        [OperationContract]
        CastVoteResponse CastVote(CastVoteRequest request);

        [OperationContract]
        AckEventSeenResponse AckEventSeen(AckEventSeenRequest request);

        [OperationContract]
        GetQuestionsResponse GetQuestions(GetQuestionsRequest request);

        [OperationContract]
        GameplayJoinMatchResponse JoinMatch(GameplayJoinMatchRequest request);

        [OperationContract]
        GameplayStartMatchResponse StartMatch(GameplayStartMatchRequest request);

        [OperationContract]
        ChooseDuelOpponentResponse ChooseDuelOpponent(ChooseDuelOpponentRequest request);
    }
}
