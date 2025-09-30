using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class GameplayService : IGameplayService
    {
        public SubmitAnswerResponse SubmitAnswer(SubmitAnswerRequest request)
        {
            return new SubmitAnswerResponse
            {
                Result = new AnswerResult
                {
                    QuestionId = request.QuestionId,
                    IsCorrect = true,
                    ChainIncrement = 0.1m,
                    CurrentChain = 0.1m,
                    BankedPoints = 0m
                }
            };
        }

        public BankResponse Bank(BankRequest request) =>
            new BankResponse { Bank = new BankState { MatchId = request.MatchId, CurrentChain = 0m, BankedPoints = 1.0m } };

        public UseLifelineResponse UseLifeline(UseLifelineRequest request) =>
            new UseLifelineResponse { Outcome = "OK" };

        public CastVoteResponse CastVote(CastVoteRequest request) =>
            new CastVoteResponse { Accepted = true };

        public AckEventSeenResponse AckEventSeen(AckEventSeenRequest request) =>
            new AckEventSeenResponse { Acknowledged = true };
    }
}
