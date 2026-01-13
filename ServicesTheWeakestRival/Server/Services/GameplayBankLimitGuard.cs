using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Gameplay;
using ServicesTheWeakestRival.Server.Services.Logic;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplayBankLimitGuard
    {
        internal static void EnsureCanAddToBank(MatchRuntimeState state, decimal amountToAdd)
        {
            if (amountToAdd <= 0m)
            {
                return;
            }

            var projected = state.BankedPoints + amountToAdd;

            if (projected > GameplayEngineConstants.MAX_BANKED_POINTS)
            {
                throw GameplayFaults.ThrowFault(
                    GameplayEngineConstants.ERROR_BANK_LIMIT_REACHED,
                    GameplayEngineConstants.MESSAGE_BANK_LIMIT_REACHED);
            }
        }
    }
}