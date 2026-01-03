using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal sealed class GameplayEventLogic
    {
        public UseLifelineResponse UseLifeline(UseLifelineRequest request)
        {
            return new UseLifelineResponse
            {
                Outcome = "OK"
            };
        }

        public AckEventSeenResponse AckEventSeen(AckEventSeenRequest request)
        {
            return new AckEventSeenResponse
            {
                Acknowledged = true
            };
        }
    }
}
