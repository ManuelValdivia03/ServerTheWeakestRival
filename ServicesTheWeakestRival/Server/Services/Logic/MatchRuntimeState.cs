using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Contracts.Services;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    internal sealed class MatchRuntimeState
    {
        private readonly List<QuestionWithAnswersDto> lightningQuestions =
            new List<QuestionWithAnswersDto>();

        private int currentLightningQuestionIndex;

        private int? preLightningPlayerIndex;

        public MatchRuntimeState(Guid matchId)
        {
            MatchId = matchId;

            Questions = new Queue<QuestionWithAnswersDto>();
            QuestionsById = new Dictionary<int, QuestionWithAnswersDto>();
            Players = new List<MatchPlayerRuntime>();

            SyncRoot = new object();

            QuestionsAskedThisRound = 0;
            RoundNumber = 1;

            IsInVotePhase = false;
            IsInDuelPhase = false;

            IsInFinalPhase = false;
            IsFinalSuddenDeath = false;
            FinalAnsweredByUserId = new Dictionary<int, int>();
            FinalCorrectByUserId = new Dictionary<int, int>();

            WeakestRivalUserId = null;
            DuelTargetUserId = null;

            VotersThisRound = new HashSet<int>();
            VotesThisRound = new Dictionary<int, int?>();

            IsFinished = false;
            WinnerUserId = null;

            ActiveSpecialEvent = SpecialEventType.None;
            WildcardMatchId = 0;

            HasSpecialEventThisRound = false;
            BombQuestionId = 0;

            HasStarted = false;

            SurpriseExam = null;

            IsDarkModeActive = false;
            DarkModeRoundNumber = 0;
        }

        public Guid MatchId { get; }

        public byte Difficulty { get; private set; }

        public string LocaleCode { get; private set; }

        public Queue<QuestionWithAnswersDto> Questions { get; }

        public Dictionary<int, QuestionWithAnswersDto> QuestionsById { get; }

        public int CurrentQuestionId { get; set; }

        public bool IsInitialized { get; private set; }

        public int QuestionsAskedThisRound { get; set; }

        public int RoundNumber { get; set; }

        public List<MatchPlayerRuntime> Players { get; }

        public int CurrentPlayerIndex { get; set; }

        public bool IsInVotePhase { get; set; }

        public bool IsInDuelPhase { get; set; }

        public bool IsInFinalPhase { get; set; }

        public bool IsFinalSuddenDeath { get; set; }

        public Dictionary<int, int> FinalAnsweredByUserId { get; }

        public Dictionary<int, int> FinalCorrectByUserId { get; }

        public int? WeakestRivalUserId { get; set; }

        public int? DuelTargetUserId { get; set; }

        public HashSet<int> VotersThisRound { get; }

        public Dictionary<int, int?> VotesThisRound { get; }

        public bool HasStarted { get; set; }

        public int CurrentStreak { get; set; }

        public decimal CurrentChain { get; set; }

        public decimal BankedPoints { get; set; }

        public bool IsFinished { get; set; }

        public int? WinnerUserId { get; set; }

        public object SyncRoot { get; }

        public SpecialEventType ActiveSpecialEvent { get; set; }

        public LightningChallengeState LightningChallenge { get; set; }

        public int WildcardMatchId { get; set; }

        public bool HasSpecialEventThisRound { get; set; }

        public int BombQuestionId { get; set; }

        public SurpriseExamState SurpriseExam { get; set; }

        public bool IsDarkModeActive { get; set; }

        public int DarkModeRoundNumber { get; set; }

        public bool IsLightningActive =>
            ActiveSpecialEvent == SpecialEventType.LightningChallenge &&
            LightningChallenge != null;

        public bool IsSurpriseExamActive =>
            ActiveSpecialEvent == SpecialEventType.SurpriseExam &&
            SurpriseExam != null;

        public void SetPreLightningTurnIndex()
        {
            if (!preLightningPlayerIndex.HasValue)
            {
                preLightningPlayerIndex = CurrentPlayerIndex;
            }
        }

        public void OverrideTurnForLightning(int targetPlayerIndex)
        {
            SetPreLightningTurnIndex();
            CurrentPlayerIndex = targetPlayerIndex;
        }

        public void RestoreTurnAfterLightning()
        {
            if (preLightningPlayerIndex.HasValue)
            {
                CurrentPlayerIndex = preLightningPlayerIndex.Value;
                preLightningPlayerIndex = null;
            }
        }

        public void Initialize(
            byte difficulty,
            string localeCode,
            List<QuestionWithAnswersDto> questions,
            decimal initialBankedPoints)
        {
            Difficulty = difficulty;
            LocaleCode = localeCode;

            Questions.Clear();
            QuestionsById.Clear();

            if (questions != null)
            {
                foreach (QuestionWithAnswersDto question in questions)
                {
                    if (question == null)
                    {
                        continue;
                    }

                    Questions.Enqueue(question);
                    QuestionsById[question.QuestionId] = question;
                }
            }

            CurrentPlayerIndex = 0;
            CurrentStreak = 0;
            CurrentChain = 0m;
            BankedPoints = initialBankedPoints;
            CurrentQuestionId = 0;

            IsInitialized = true;

            QuestionsAskedThisRound = 0;
            RoundNumber = 1;

            IsInVotePhase = false;
            IsInDuelPhase = false;

            ResetFinalPhase();

            WeakestRivalUserId = null;
            DuelTargetUserId = null;

            VotersThisRound.Clear();
            VotesThisRound.Clear();

            IsFinished = false;
            WinnerUserId = null;

            HasSpecialEventThisRound = false;
            BombQuestionId = 0;

            IsDarkModeActive = false;
            DarkModeRoundNumber = 0;

            ResetSurpriseExam();
            RestoreTurnAfterLightning();
            ResetLightningChallenge();

            foreach (MatchPlayerRuntime player in Players)
            {
                if (player == null)
                {
                    continue;
                }

                player.IsShieldActive = false;
                player.IsDoublePointsActive = false;
                player.BlockWildcardsRoundNumber = 0;
                player.PendingTimeDeltaSeconds = 0;
            }
        }

        public void ResetFinalPhase()
        {
            IsInFinalPhase = false;
            IsFinalSuddenDeath = false;
            FinalAnsweredByUserId.Clear();
            FinalCorrectByUserId.Clear();
        }

        public MatchPlayerRuntime GetCurrentPlayer()
        {
            if (Players.Count == 0)
            {
                return null;
            }

            if (CurrentPlayerIndex < 0 || CurrentPlayerIndex >= Players.Count)
            {
                CurrentPlayerIndex = 0;
            }

            return Players[CurrentPlayerIndex];
        }

        public void AdvanceTurn()
        {
            if (Players.Count == 0)
            {
                return;
            }

            int nextIndex = CurrentPlayerIndex;

            if (!IsInDuelPhase || !WeakestRivalUserId.HasValue || !DuelTargetUserId.HasValue)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    nextIndex++;

                    if (nextIndex >= Players.Count)
                    {
                        nextIndex = 0;
                    }

                    if (!Players[nextIndex].IsEliminated)
                    {
                        CurrentPlayerIndex = nextIndex;
                        return;
                    }
                }

                return;
            }

            HashSet<int> duelPlayers = new HashSet<int>
            {
                WeakestRivalUserId.Value,
                DuelTargetUserId.Value
            };

            for (int i = 0; i < Players.Count; i++)
            {
                nextIndex++;

                if (nextIndex >= Players.Count)
                {
                    nextIndex = 0;
                }

                if (!Players[nextIndex].IsEliminated &&
                    duelPlayers.Contains(Players[nextIndex].UserId))
                {
                    CurrentPlayerIndex = nextIndex;
                    return;
                }
            }
        }

        public void SetLightningQuestions(IEnumerable<QuestionWithAnswersDto> questions)
        {
            lightningQuestions.Clear();
            currentLightningQuestionIndex = 0;

            if (questions == null)
            {
                return;
            }

            foreach (QuestionWithAnswersDto question in questions)
            {
                if (question != null)
                {
                    lightningQuestions.Add(question);
                }
            }
        }

        public QuestionWithAnswersDto GetCurrentLightningQuestion()
        {
            if (currentLightningQuestionIndex < 0 ||
                currentLightningQuestionIndex >= lightningQuestions.Count)
            {
                return null;
            }

            return lightningQuestions[currentLightningQuestionIndex];
        }

        public void MoveToNextLightningQuestion()
        {
            if (currentLightningQuestionIndex < lightningQuestions.Count)
            {
                currentLightningQuestionIndex++;
            }
        }

        public void ResetLightningChallenge()
        {
            if (ActiveSpecialEvent == SpecialEventType.LightningChallenge)
            {
                ActiveSpecialEvent = SpecialEventType.None;
            }

            LightningChallenge = null;

            lightningQuestions.Clear();
            currentLightningQuestionIndex = 0;

            RestoreTurnAfterLightning();
        }

        public void ResetSurpriseExam()
        {
            if (SurpriseExam != null)
            {
                SurpriseExam.DisposeTimerSafely();
            }

            SurpriseExam = null;

            if (ActiveSpecialEvent == SpecialEventType.SurpriseExam)
            {
                ActiveSpecialEvent = SpecialEventType.None;
            }
        }
    }

    internal sealed class MatchPlayerRuntime
    {
        public MatchPlayerRuntime(int userId, string displayName, IGameplayServiceCallback callback)
        {
            UserId = userId;
            DisplayName = displayName;
            Callback = callback;

            IsEliminated = false;
            IsWinner = false;

            IsShieldActive = false;
            IsDoublePointsActive = false;
            BlockWildcardsRoundNumber = 0;

            PendingTimeDeltaSeconds = 0;
        }

        public int UserId { get; }

        public string DisplayName { get; }

        public IGameplayServiceCallback Callback { get; set; }

        public bool IsEliminated { get; set; }

        public bool IsWinner { get; set; }

        public AvatarAppearanceDto Avatar { get; set; }

        public bool IsShieldActive { get; set; }

        public bool IsDoublePointsActive { get; set; }

        public int BlockWildcardsRoundNumber { get; set; }

        public int PendingTimeDeltaSeconds { get; set; }
    }

    internal sealed class SurpriseExamState
    {
        public SurpriseExamState(DateTime utcDeadline)
        {
            UtcDeadline = utcDeadline;
        }

        public DateTime UtcDeadline { get; }

        public Dictionary<int, int> QuestionIdByUserId { get; } = new Dictionary<int, int>();

        public Dictionary<int, bool> IsCorrectByUserId { get; } = new Dictionary<int, bool>();

        public HashSet<int> PendingUserIds { get; } = new HashSet<int>();

        public bool IsResolved { get; set; }

        public Timer Timer { get; private set; }

        public void AttachTimer(Timer timer)
        {
            Timer = timer;
        }

        public void DisposeTimerSafely()
        {
            Timer timer = Timer;
            Timer = null;

            if (timer != null)
            {
                timer.Dispose();
            }
        }
    }
}
