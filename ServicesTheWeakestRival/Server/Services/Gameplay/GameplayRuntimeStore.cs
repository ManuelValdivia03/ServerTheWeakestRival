using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Concurrent;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal static class GameplayRuntimeStore
    {
        private static readonly ConcurrentDictionary<Guid, MatchRuntimeState> Matches =
            new ConcurrentDictionary<Guid, MatchRuntimeState>();

        private static readonly ConcurrentDictionary<int, Guid> PlayerMatchByUserId =
            new ConcurrentDictionary<int, Guid>();

        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>> ExpectedPlayersByMatchId =
            new ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>>();

        internal static MatchRuntimeState GetOrCreateMatch(Guid matchId)
        {
            return Matches.GetOrAdd(matchId, id => new MatchRuntimeState(id));
        }

        internal static MatchRuntimeState GetMatchOrThrow(Guid matchId)
        {
            if (!Matches.TryGetValue(matchId, out MatchRuntimeState state))
            {
                throw GameplayServiceContext.ThrowFault(
                    GameplayServiceContext.ERROR_MATCH_NOT_FOUND,
                    GameplayServiceContext.ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            return state;
        }

        internal static bool TryGetPlayerMatchId(int userId, out Guid matchId)
        {
            return PlayerMatchByUserId.TryGetValue(userId, out matchId);
        }

        internal static void UpsertPlayerMatch(int userId, Guid matchId)
        {
            PlayerMatchByUserId[userId] = matchId;
        }

        internal static void RemoveMatch(Guid matchId)
        {
            Matches.TryRemove(matchId, out _);
            ExpectedPlayersByMatchId.TryRemove(matchId, out _);
        }

        internal static void RemovePlayerMatch(int userId)
        {
            PlayerMatchByUserId.TryRemove(userId, out _);
        }

        internal static ConcurrentDictionary<int, byte> GetOrCreateExpectedPlayers(Guid matchId)
        {
            return ExpectedPlayersByMatchId.GetOrAdd(matchId, _ => new ConcurrentDictionary<int, byte>());
        }

        internal static bool TryGetExpectedPlayers(Guid matchId, out ConcurrentDictionary<int, byte> expectedPlayers)
        {
            return ExpectedPlayersByMatchId.TryGetValue(matchId, out expectedPlayers);
        }
    }
}
