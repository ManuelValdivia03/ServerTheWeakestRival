using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Contracts.Services
{
    [ServiceContract]
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
        void OnBankUpdated(
            Guid matchId,
            BankState bank);

        [OperationContract(IsOneWay = true)]
        void OnVotePhaseStarted(
            Guid matchId,
            TimeSpan timeLimit);

        [OperationContract(IsOneWay = true)]
        void OnElimination(
            Guid matchId,
            PlayerSummary eliminatedPlayer);

        [OperationContract(IsOneWay = true)]
        void OnSpecialEvent(
            Guid matchId,
            string eventName,
            string description);

        [OperationContract(IsOneWay = true)]
        void OnCoinFlipResolved(
            Guid matchId,
            CoinFlipResolvedDto coinFlip);

        [OperationContract(IsOneWay = true)]
        void OnDuelCandidates(
            Guid matchId,
            DuelCandidatesDto duelCandidates);

        [OperationContract(IsOneWay = true)]
        void OnMatchFinished(
            Guid matchId,
            PlayerSummary winner);

        [OperationContract(IsOneWay = true)]
        void OnLightningChallengeStarted(
            Guid matchId,
            Guid roundId,
            PlayerSummary targetPlayer,
            int totalQuestions,
            int totalTimeSeconds);

        [OperationContract(IsOneWay = true)]
        void OnLightningChallengeQuestion(
            Guid matchId,
            Guid roundId,
            int questionIndex,
            QuestionWithAnswersDto question);

        [OperationContract(IsOneWay = true)]
        void OnLightningChallengeFinished(
            Guid matchId,
            Guid roundId,
            int correctAnswers,
            bool isSuccess);
    }
}
