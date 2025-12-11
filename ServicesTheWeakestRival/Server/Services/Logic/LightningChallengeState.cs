using System;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    internal sealed class LightningChallengeState
    {
        public Guid MatchId { get; }
        public Guid RoundId { get; }
        public int PlayerId { get; }

        public int RemainingQuestions { get; set; }
        public int CorrectAnswers { get; set; }

        public TimeSpan RemainingTime { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsSuccess { get; set; }

        public LightningChallengeState(
            Guid matchId,
            Guid roundId,
            int playerId,
            int totalQuestions,
            TimeSpan totalTime)
        {
            MatchId = matchId;
            RoundId = roundId;
            PlayerId = playerId;
            RemainingQuestions = totalQuestions;
            RemainingTime = totalTime;
            CorrectAnswers = 0;
            IsCompleted = false;
            IsSuccess = false;
        }
    }
}
