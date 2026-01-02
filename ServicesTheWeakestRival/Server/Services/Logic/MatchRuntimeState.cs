using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Contracts.Services;
using System;
using System.Collections.Generic;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    internal sealed class MatchRuntimeState
    {
        private readonly List<QuestionWithAnswersDto> _lightningQuestions =
            new List<QuestionWithAnswersDto>();

        private int _currentLightningQuestionIndex;

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
            _preLightningPlayerIndex = null;
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

        public bool IsLightningActive =>
            ActiveSpecialEvent == SpecialEventType.LightningChallenge &&
            LightningChallenge != null;

        private int? _preLightningPlayerIndex;

        public void SetPreLightningTurnIndex()
        {
            if (!_preLightningPlayerIndex.HasValue)
            {
                _preLightningPlayerIndex = CurrentPlayerIndex;
            }
        }

        public void OverrideTurnForLightning(int targetPlayerIndex)
        {
            SetPreLightningTurnIndex();
            CurrentPlayerIndex = targetPlayerIndex;
        }

        public void RestoreTurnAfterLightning()
        {
            if (_preLightningPlayerIndex.HasValue)
            {
                CurrentPlayerIndex = _preLightningPlayerIndex.Value;
                _preLightningPlayerIndex = null;
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

            WeakestRivalUserId = null;
            DuelTargetUserId = null;

            VotersThisRound.Clear();
            VotesThisRound.Clear();

            IsFinished = false;
            WinnerUserId = null;

            HasSpecialEventThisRound = false;
            BombQuestionId = 0;

            RestoreTurnAfterLightning();
            ResetLightningChallenge();
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
            _lightningQuestions.Clear();
            _currentLightningQuestionIndex = 0;

            if (questions == null)
            {
                return;
            }

            foreach (QuestionWithAnswersDto question in questions)
            {
                if (question != null)
                {
                    _lightningQuestions.Add(question);
                }
            }
        }

        public QuestionWithAnswersDto GetCurrentLightningQuestion()
        {
            if (_currentLightningQuestionIndex < 0 ||
                _currentLightningQuestionIndex >= _lightningQuestions.Count)
            {
                return null;
            }

            return _lightningQuestions[_currentLightningQuestionIndex];
        }

        public void MoveToNextLightningQuestion()
        {
            if (_currentLightningQuestionIndex < _lightningQuestions.Count)
            {
                _currentLightningQuestionIndex++;
            }
        }

        public void ResetLightningChallenge()
        {
            ActiveSpecialEvent = SpecialEventType.None;
            LightningChallenge = null;

            _lightningQuestions.Clear();
            _currentLightningQuestionIndex = 0;

            RestoreTurnAfterLightning();
        }
    }

    internal sealed class MatchPlayerRuntime
    {
        public MatchPlayerRuntime(int userId, string displayName, IGameplayServiceCallback callback)
        {
            UserId = userId;
            DisplayName = displayName;
            Callback = callback;
        }

        public int UserId { get; }

        public string DisplayName { get; }

        public IGameplayServiceCallback Callback { get; set; }

        public bool IsEliminated { get; set; }

        public bool IsWinner { get; set; }

        public AvatarAppearanceDto Avatar { get; set; }
    }
}
