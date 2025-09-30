using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    public interface IGameplayClientCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnNextQuestion(Guid matchId, PlayerSummary targetPlayer, Question question, decimal currentChain, decimal banked);

        [OperationContract(IsOneWay = true)]
        void OnAnswerEvaluated(Guid matchId, PlayerSummary player, AnswerResult result);

        [OperationContract(IsOneWay = true)]
        void OnBankUpdated(Guid matchId, BankState bank);

        [OperationContract(IsOneWay = true)]
        void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit);

        [OperationContract(IsOneWay = true)]
        void OnElimination(Guid matchId, PlayerSummary eliminatedPlayer);

        [OperationContract(IsOneWay = true)]
        void OnSpecialEvent(Guid matchId, string eventName, string description);
    }

    [ServiceContract(CallbackContract = typeof(IGameplayClientCallback))]
    public interface IGameplayService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        SubmitAnswerResponse SubmitAnswer(SubmitAnswerRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        BankResponse Bank(BankRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        UseLifelineResponse UseLifeline(UseLifelineRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        CastVoteResponse CastVote(CastVoteRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        AckEventSeenResponse AckEventSeen(AckEventSeenRequest request);
    }
}
