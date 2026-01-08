using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Gameplay;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.ServiceModel;
using log4net;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class GameplayService : IGameplayService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayService));

        private const string CTX_SUBMIT_ANSWER = "GameplayService.SubmitAnswer";
        private const string CTX_BANK = "GameplayService.Bank";
        private const string CTX_CAST_VOTE = "GameplayService.CastVote";
        private const string CTX_JOIN_MATCH = "GameplayService.JoinMatch";
        private const string CTX_START_MATCH = "GameplayService.StartMatch";
        private const string CTX_CHOOSE_DUEL = "GameplayService.ChooseDuelOpponent";
        private const string CTX_GET_QUESTIONS = "GameplayService.GetQuestions";

        private readonly GameplayEngine engine;
        private readonly GameplayMatchLogic matchLogic;
        private readonly GameplayTurnLogic turnLogic;

        public GameplayService()
        {
            engine = GameplayEngine.Shared;
            matchLogic = new GameplayMatchLogic(engine);
            turnLogic = new GameplayTurnLogic(engine);
        }

        public SubmitAnswerResponse SubmitAnswer(SubmitAnswerRequest request)
        {
            return ExecuteService(CTX_SUBMIT_ANSWER, () => turnLogic.SubmitAnswer(request));
        }

        public BankResponse Bank(BankRequest request)
        {
            return ExecuteService(CTX_BANK, () => turnLogic.Bank(request));
        }

        public UseLifelineResponse UseLifeline(UseLifelineRequest request)
        {
            return ExecuteService("GameplayService.UseLifeline", () =>
            {
                if (request == null)
                {
                    throw GameplayEngine.ThrowFault(GameplayEngine.ERROR_INVALID_REQUEST, "Request is null.");
                }

                return new UseLifelineResponse
                {
                    Outcome = "OK"
                };
            });
        }

        public CastVoteResponse CastVote(CastVoteRequest request)
        {
            return ExecuteService(CTX_CAST_VOTE, () => turnLogic.CastVote(request));
        }

        public AckEventSeenResponse AckEventSeen(AckEventSeenRequest request)
        {
            return ExecuteService("GameplayService.AckEventSeen", () =>
            {
                if (request == null)
                {
                    throw GameplayEngine.ThrowFault(GameplayEngine.ERROR_INVALID_REQUEST, "Request is null.");
                }

                return new AckEventSeenResponse
                {
                    Acknowledged = true
                };
            });
        }

        public GetQuestionsResponse GetQuestions(GetQuestionsRequest request)
        {
            return ExecuteService(CTX_GET_QUESTIONS, () => engine.GetQuestions(request));
        }

        public GameplayJoinMatchResponse JoinMatch(GameplayJoinMatchRequest request)
        {
            return ExecuteService(CTX_JOIN_MATCH, () => matchLogic.JoinMatch(request));
        }

        public GameplayStartMatchResponse StartMatch(GameplayStartMatchRequest request)
        {
            return ExecuteService(CTX_START_MATCH, () => matchLogic.StartMatch(request));
        }

        public ChooseDuelOpponentResponse ChooseDuelOpponent(ChooseDuelOpponentRequest request)
        {
            return ExecuteService(CTX_CHOOSE_DUEL, () => matchLogic.ChooseDuelOpponent(request));
        }

        private static T ExecuteService<T>(string context, Func<T> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            try
            {
                return action();
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(context, ex);
                throw GameplayEngine.ThrowTechnicalFault(
                    GameplayEngine.ERROR_UNEXPECTED,
                    GameplayEngine.MESSAGE_UNEXPECTED_ERROR,
                    context,
                    ex);
            }
        }
    }
}