using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Contracts.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using TheWeakestRival.Contracts.Enums;

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
        }

        // ---------------------------------------------------------------
        // IDENTIDAD
        // ---------------------------------------------------------------
        public Guid MatchId { get; }

        // ID en DB (GameplayService lo espera)
        public int DbMatchId { get; set; }

        // ---------------------------------------------------------------
        // PREGUNTAS
        // ---------------------------------------------------------------
        public byte Difficulty { get; private set; }

        public string LocaleCode { get; private set; }

        public Queue<QuestionWithAnswersDto> Questions { get; }

        public Dictionary<int, QuestionWithAnswersDto> QuestionsById { get; }

        public int CurrentQuestionId { get; set; }

        public bool IsInitialized { get; private set; }

        public int QuestionsAskedThisRound { get; set; }

        public int RoundNumber { get; set; }

        // ---------------------------------------------------------------
        // JUGADORES
        // ---------------------------------------------------------------
        public List<MatchPlayerRuntime> Players { get; }

        public int CurrentPlayerIndex { get; set; }

        public bool IsInVotePhase { get; set; }

        public bool IsInDuelPhase { get; set; }

        public int? WeakestRivalUserId { get; set; }

        public int? DuelTargetUserId { get; set; }

        public HashSet<int> VotersThisRound { get; }

        public Dictionary<int, int?> VotesThisRound { get; }

        // ---------------------------------------------------------------
        // CADENA / BANCO
        // ---------------------------------------------------------------
        public int CurrentStreak { get; set; }

        public decimal CurrentChain { get; set; }

        public decimal BankedPoints { get; set; }

        // ---------------------------------------------------------------
        // FINALIZACIÓN
        // ---------------------------------------------------------------
        public bool IsFinished { get; set; }

        public int? WinnerUserId { get; set; }

        // ---------------------------------------------------------------
        // SINCRONIZACIÓN
        // ---------------------------------------------------------------
        public object SyncRoot { get; }

        // ---------------------------------------------------------------
        // EVENTOS ESPECIALES
        // ---------------------------------------------------------------
        public SpecialEventType ActiveSpecialEvent { get; set; }

        public LightningChallengeState LightningChallenge { get; set; }

        public bool IsLightningActive =>
            ActiveSpecialEvent == SpecialEventType.LightningChallenge &&
            LightningChallenge != null;

        // ---------------------------------------------------------------
        // INICIALIZACIÓN
        // ---------------------------------------------------------------
        public void Initialize(byte difficulty, string localeCode, List<QuestionWithAnswersDto> questions)
        {
            Difficulty = difficulty;
            LocaleCode = localeCode;

            Questions.Clear();
            QuestionsById.Clear();

            foreach (var q in questions)
            {
                Questions.Enqueue(q);
                QuestionsById[q.QuestionId] = q;
            }

            CurrentPlayerIndex = 0;
            CurrentStreak = 0;
            CurrentChain = 0m;
            BankedPoints = 0m;

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

            ResetLightningChallenge();
        }

        // ---------------------------------------------------------------
        // TURNOS
        // ---------------------------------------------------------------
        public MatchPlayerRuntime GetCurrentPlayer()
        {
            if (Players.Count == 0)
                return null;

            if (CurrentPlayerIndex < 0 || CurrentPlayerIndex >= Players.Count)
                CurrentPlayerIndex = 0;

            return Players[CurrentPlayerIndex];
        }

        public void AdvanceTurn()
        {
            if (Players.Count == 0)
                return;

            int nextIndex = CurrentPlayerIndex;

            // Turno normal
            if (!IsInDuelPhase || !WeakestRivalUserId.HasValue || !DuelTargetUserId.HasValue)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    nextIndex++;

                    if (nextIndex >= Players.Count)
                        nextIndex = 0;

                    if (!Players[nextIndex].IsEliminated)
                    {
                        CurrentPlayerIndex = nextIndex;
                        return;
                    }
                }
                return;
            }

            // Turno de duelo
            var duelPlayers = new HashSet<int>
            {
                WeakestRivalUserId.Value,
                DuelTargetUserId.Value
            };

            for (int i = 0; i < Players.Count; i++)
            {
                nextIndex++;

                if (nextIndex >= Players.Count)
                    nextIndex = 0;

                if (!Players[nextIndex].IsEliminated &&
                    duelPlayers.Contains(Players[nextIndex].UserId))
                {
                    CurrentPlayerIndex = nextIndex;
                    return;
                }
            }
        }

        // ---------------------------------------------------------------
        // LIGHTNING CHALLENGE
        // ---------------------------------------------------------------
        public void SetLightningQuestions(IEnumerable<QuestionWithAnswersDto> questions)
        {
            _lightningQuestions.Clear();
            _currentLightningQuestionIndex = 0;

            foreach (var q in questions)
            {
                if (q != null)
                    _lightningQuestions.Add(q);
            }
        }

        public QuestionWithAnswersDto GetCurrentLightningQuestion()
        {
            if (_currentLightningQuestionIndex < 0 ||
                _currentLightningQuestionIndex >= _lightningQuestions.Count)
                return null;

            return _lightningQuestions[_currentLightningQuestionIndex];
        }

        public void MoveToNextLightningQuestion()
        {
            if (_currentLightningQuestionIndex < _lightningQuestions.Count)
                _currentLightningQuestionIndex++;
        }

        public void ResetLightningChallenge()
        {
            ActiveSpecialEvent = SpecialEventType.None;
            LightningChallenge = null;

            _lightningQuestions.Clear();
            _currentLightningQuestionIndex = 0;
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
