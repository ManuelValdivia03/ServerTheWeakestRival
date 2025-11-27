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
    }

    public interface IGameplayServiceCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnNextQuestion(
            Guid matchId,
            PlayerSummary targetPlayer,
            QuestionWithAnswersDto question,
            decimal currentChain,
            decimal banked);

        [OperationContract(IsOneWay = true)]
        void OnAnswerEvaluated(
            Guid matchId,
            PlayerSummary player,
            AnswerResult result);

        [OperationContract(IsOneWay = true)]
        void OnBankUpdated(Guid matchId, BankState bank);

        [OperationContract(IsOneWay = true)]
        void OnVotePhaseStarted(Guid matchId, TimeSpan timeLimit);

        [OperationContract(IsOneWay = true)]
        void OnElimination(Guid matchId, PlayerSummary eliminatedPlayer);

        [OperationContract(IsOneWay = true)]
        void OnSpecialEvent(Guid matchId, string eventName, string description);
    }
}
