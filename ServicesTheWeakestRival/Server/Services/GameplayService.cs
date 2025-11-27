using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;
using System.Threading.Tasks;
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;
using TheWeakestRival.Data;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class GameplayService : IGameplayService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayService));

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_DB = "DB_ERROR";
        private const string ERROR_UNEXPECTED = "UNEXPECTED_ERROR";
        private const string ERROR_MATCH_NOT_FOUND = "MATCH_NOT_FOUND";
        private const string ERROR_NOT_PLAYER_TURN = "NOT_PLAYER_TURN";

        private const string ERROR_INVALID_REQUEST_MESSAGE = "Request is null.";
        private const string ERROR_MATCH_NOT_FOUND_MESSAGE = "Match not found.";
        private const string ERROR_NOT_PLAYER_TURN_MESSAGE = "It is not the player turn.";

        private const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";

        private const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        private const int DEFAULT_MAX_QUESTIONS = 40;
        private const int NEXT_QUESTION_DELAY_MS = 2000;

        private static readonly decimal[] CHAIN_STEPS =
        {
            0.10m,
            0.20m,
            0.30m,
            0.40m,
            0.50m
        };

        private static readonly ConcurrentDictionary<Guid, MatchRuntimeState> Matches =
            new ConcurrentDictionary<Guid, MatchRuntimeState>();

        private static readonly ConcurrentDictionary<int, Guid> PlayerMatchByUserId =
            new ConcurrentDictionary<int, Guid>();

        public SubmitAnswerResponse SubmitAnswer(SubmitAnswerRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }

            int userId = Authenticate(request.Token);

            if (!Matches.TryGetValue(request.MatchId, out var state))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            lock (state.SyncRoot)
            {
                var currentPlayer = state.GetCurrentPlayer();
                if (currentPlayer == null || currentPlayer.UserId != userId)
                {
                    throw ThrowFault(ERROR_NOT_PLAYER_TURN, ERROR_NOT_PLAYER_TURN_MESSAGE);
                }

                if (!state.QuestionsById.TryGetValue(state.CurrentQuestionId, out var question))
                {
                    throw ThrowFault(ERROR_INVALID_REQUEST, "Current question not found for this match.");
                }

                bool isTimeout = string.IsNullOrWhiteSpace(request.AnswerText);

                bool isCorrect = false;

                if (!isTimeout)
                {
                    var selectedAnswer = question.Answers.Find(
                        a => string.Equals(a.Text, request.AnswerText, StringComparison.Ordinal));

                    if (selectedAnswer == null)
                    {
                        throw ThrowFault(ERROR_INVALID_REQUEST, "Answer not found for current question.");
                    }

                    isCorrect = selectedAnswer.IsCorrect;
                }

                if (isCorrect)
                {
                    if (state.CurrentStreak < CHAIN_STEPS.Length)
                    {
                        decimal increment = CHAIN_STEPS[state.CurrentStreak];
                        state.CurrentChain += increment;
                        state.CurrentStreak++;
                    }
                }
                else
                {
                    state.CurrentChain = 0m;
                    state.CurrentStreak = 0;
                }

                var result = new AnswerResult
                {
                    QuestionId = request.QuestionId,
                    IsCorrect = isCorrect,
                    ChainIncrement = state.CurrentStreak > 0 && isCorrect
                    ? CHAIN_STEPS[state.CurrentStreak - 1]
                    : 0m,
                    CurrentChain = state.CurrentChain,
                    BankedPoints = state.BankedPoints
                };


                var playerSummary = new PlayerSummary
                {
                    UserId = currentPlayer.UserId,
                    DisplayName = currentPlayer.DisplayName,
                    Avatar = currentPlayer.Avatar
                };

                foreach (var player in state.Players)
                {
                    try
                    {
                        player.Callback.OnAnswerEvaluated(
                            state.MatchId,
                            playerSummary,
                            result);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error al notificar OnAnswerEvaluated.", ex);
                    }
                }

                var response = new SubmitAnswerResponse
                {
                    Result = result
                };

                state.AdvanceTurn();
                SendNextQuestion(state);

                return response;
            }
        }


        public BankResponse Bank(BankRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }

            int userId = Authenticate(request.Token);

            if (!Matches.TryGetValue(request.MatchId, out var state))
            {
                throw ThrowFault(ERROR_MATCH_NOT_FOUND, ERROR_MATCH_NOT_FOUND_MESSAGE);
            }

            lock (state.SyncRoot)
            {
                var currentPlayer = state.GetCurrentPlayer();
                if (currentPlayer == null || currentPlayer.UserId != userId)
                {
                    throw ThrowFault(ERROR_NOT_PLAYER_TURN, ERROR_NOT_PLAYER_TURN_MESSAGE);
                }

                state.BankedPoints += state.CurrentChain;
                state.CurrentChain = 0m;
                state.CurrentStreak = 0;

                var bankState = new BankState
                {
                    MatchId = state.MatchId,
                    CurrentChain = state.CurrentChain,
                    BankedPoints = state.BankedPoints
                };

                foreach (var player in state.Players)
                {
                    try
                    {
                        player.Callback.OnBankUpdated(state.MatchId, bankState);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Error al notificar OnBankUpdated.", ex);
                    }
                }

                return new BankResponse
                {
                    Bank = bankState
                };
            }
        }

        public UseLifelineResponse UseLifeline(UseLifelineRequest request)
        {
            return new UseLifelineResponse
            {
                Outcome = "OK"
            };
        }

        public CastVoteResponse CastVote(CastVoteRequest request)
        {
            return new CastVoteResponse
            {
                Accepted = true
            };
        }

        public AckEventSeenResponse AckEventSeen(AckEventSeenRequest request)
        {
            return new AckEventSeenResponse
            {
                Acknowledged = true
            };
        }

        public GetQuestionsResponse GetQuestions(GetQuestionsRequest request)
        {
            ValidateGetQuestionsRequest(request);

            Authenticate(request.Token);

            int maxQuestions = request.MaxQuestions.HasValue && request.MaxQuestions.Value > 0
                ? request.MaxQuestions.Value
                : DEFAULT_MAX_QUESTIONS;

            try
            {
                var questions = LoadQuestions(request.Difficulty, request.LocaleCode, maxQuestions);

                Logger.InfoFormat(
                    "GetQuestions: Difficulty={0}, Locale={1}, RequestedMax={2}, Returned={3}",
                    request.Difficulty,
                    request.LocaleCode,
                    maxQuestions,
                    questions.Count);

                return new GetQuestionsResponse
                {
                    Questions = questions
                };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "GameplayService.GetQuestions",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "GameplayService.GetQuestions",
                    ex);
            }
        }

        public GameplayJoinMatchResponse JoinMatch(GameplayJoinMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }

            int userId = Authenticate(request.Token);

            var callback = OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();

            var state = Matches.GetOrAdd(request.MatchId, id => new MatchRuntimeState(id));

            lock (state.SyncRoot)
            {
                var existingPlayer = state.Players.Find(p => p.UserId == userId);
                if (existingPlayer != null)
                {
                    existingPlayer.Callback = callback;
                }
                else
                {
                    var displayName = "Jugador " + userId;

                    var avatarSql = new UserAvatarSql(GetConnectionString());
                    var avatarEntity = avatarSql.GetByUserId(userId);
                    var avatar = MapAvatar(avatarEntity);

                    var player = new MatchPlayerRuntime(userId, displayName, callback)
                    {
                        Avatar = avatar
                    };

                    state.Players.Add(player);
                }
            }

            PlayerMatchByUserId[userId] = request.MatchId;

            return new GameplayJoinMatchResponse
            {
                Accepted = true
            };
        }

        public GameplayStartMatchResponse StartMatch(GameplayStartMatchRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.MatchId == Guid.Empty)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "MatchId is required.");
            }

            if (request.Difficulty <= 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Difficulty must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.LocaleCode))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "LocaleCode is required.");
            }

            int userId = Authenticate(request.Token);

            int maxQuestions = request.MaxQuestions.HasValue && request.MaxQuestions.Value > 0
                ? request.MaxQuestions.Value
                : DEFAULT_MAX_QUESTIONS;

            var state = Matches.GetOrAdd(request.MatchId, id => new MatchRuntimeState(id));

            try
            {
                lock (state.SyncRoot)
                {
                    if (!state.IsInitialized)
                    {
                        var questions = LoadQuestions(request.Difficulty, request.LocaleCode, maxQuestions);
                        state.Initialize(request.Difficulty, request.LocaleCode, questions);

                        if (state.Players.Count == 0)
                        {
                            var callback = OperationContext.Current.GetCallbackChannel<IGameplayServiceCallback>();
                            var displayName = "Jugador " + userId;

                            var avatarSql = new UserAvatarSql(GetConnectionString());
                            var avatarEntity = avatarSql.GetByUserId(userId);
                            var avatar = MapAvatar(avatarEntity);

                            var player = new MatchPlayerRuntime(userId, displayName, callback)
                            {
                                Avatar = avatar
                            };

                            state.Players.Add(player);
                            PlayerMatchByUserId[userId] = request.MatchId;
                        }

                        state.CurrentPlayerIndex = 0;

                        SendNextQuestion(state);
                    }
                }

                return new GameplayStartMatchResponse
                {
                    Started = true
                };
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "GameplayService.StartMatch",
                    ex);
            }
            catch (Exception ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_UNEXPECTED,
                    MESSAGE_UNEXPECTED_ERROR,
                    "GameplayService.StartMatch",
                    ex);
            }
        }

        private static void SendNextQuestion(MatchRuntimeState state)
        {
            if (state.Questions.Count == 0)
            {
                return;
            }

            var question = state.Questions.Dequeue();
            state.CurrentQuestionId = question.QuestionId;

            var targetPlayer = state.GetCurrentPlayer();
            if (targetPlayer == null)
            {
                return;
            }

            var targetSummary = new PlayerSummary
            {
                UserId = targetPlayer.UserId,
                DisplayName = targetPlayer.DisplayName,
                IsOnline = true,
                Avatar = targetPlayer.Avatar
            };

            foreach (var player in state.Players)
            {
                try
                {
                    player.Callback.OnNextQuestion(
                        state.MatchId,
                        targetSummary,
                        question,
                        state.CurrentChain,
                        state.BankedPoints);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Error al notificar OnNextQuestion.", ex);
                }
            }
        }

        private static List<QuestionWithAnswersDto> LoadQuestions(byte difficulty, string localeCode, int maxQuestions)
        {
            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                using (var command = new SqlCommand(QuestionsSql.Text.LIST_QUESTIONS_WITH_ANSWERS, connection))
                {
                    command.CommandType = CommandType.Text;

                    command.Parameters.Add("@MaxQuestions", SqlDbType.Int).Value = maxQuestions;
                    command.Parameters.Add("@Difficulty", SqlDbType.TinyInt).Value = difficulty;
                    command.Parameters.Add("@LocaleCode", SqlDbType.NVarChar, 10).Value = localeCode;

                    connection.Open();

                    var questionsById = new Dictionary<int, QuestionWithAnswersDto>();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int questionId = reader.GetInt32(0);

                            if (!questionsById.TryGetValue(questionId, out var question))
                            {
                                question = new QuestionWithAnswersDto
                                {
                                    QuestionId = questionId,
                                    CategoryId = reader.GetInt32(1),
                                    Difficulty = reader.GetByte(2),
                                    LocaleCode = reader.GetString(3),
                                    Body = reader.GetString(4),
                                    Answers = new List<AnswerDto>()
                                };

                                questionsById.Add(questionId, question);
                            }

                            var answer = new AnswerDto
                            {
                                AnswerId = reader.GetInt32(5),
                                Text = reader.GetString(6),
                                IsCorrect = reader.GetBoolean(7),
                                DisplayOrder = reader.GetByte(8)
                            };

                            question.Answers.Add(answer);
                        }
                    }

                    return new List<QuestionWithAnswersDto>(questionsById.Values);
                }
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "GameplayService.LoadQuestions",
                    ex);
            }
        }

        private static void ValidateGetQuestionsRequest(GetQuestionsRequest request)
        {
            if (request == null)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, ERROR_INVALID_REQUEST_MESSAGE);
            }

            if (request.Difficulty <= 0)
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "Difficulty must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.LocaleCode))
            {
                throw ThrowFault(ERROR_INVALID_REQUEST, "LocaleCode is required.");
            }
        }

        private static string GetConnectionString()
        {
            var configurationString = ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (configurationString == null || string.IsNullOrWhiteSpace(configurationString.ConnectionString))
            {
                Logger.ErrorFormat("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME);

                throw ThrowTechnicalFault(
                    "CONFIG_ERROR",
                    "Configuration error. Please contact support.",
                    "GameplayService.GetConnectionString",
                    new ConfigurationErrorsException(
                        string.Format("Missing connection string '{0}'.", MAIN_CONNECTION_STRING_NAME)));
            }

            return configurationString.ConnectionString;
        }

        private static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat("Service fault. Code='{0}', Message='{1}'", code, message);

            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        private static FaultException<ServiceFault> ThrowTechnicalFault(
            string code,
            string userMessage,
            string context,
            Exception ex)
        {
            Logger.Error(context, ex);

            var fault = new ServiceFault
            {
                Code = code,
                Message = userMessage
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }

        private static int Authenticate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ThrowFault("AUTH_REQUIRED", "Missing token.");
            }

            if (!TokenCache.TryGetValue(token, out var authToken))
            {
                throw ThrowFault("AUTH_INVALID", "Invalid token.");
            }

            if (authToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw ThrowFault("AUTH_EXPIRED", "Token expired.");
            }

            return authToken.UserId;
        }

        private static AvatarAppearanceDto MapAvatar(UserAvatarEntity entity)
        {
            if (entity == null)
            {
                return new AvatarAppearanceDto
                {
                    BodyColor = AvatarBodyColor.Blue,
                    PantsColor = AvatarPantsColor.Black,
                    HatType = AvatarHatType.None,
                    HatColor = AvatarHatColor.Default,
                    FaceType = AvatarFaceType.Default,
                    UseProfilePhotoAsFace = false
                };
            }

            return new AvatarAppearanceDto
            {
                BodyColor = (AvatarBodyColor)entity.BodyColor,
                PantsColor = (AvatarPantsColor)entity.PantsColor,
                HatType = (AvatarHatType)entity.HatType,
                HatColor = (AvatarHatColor)entity.HatColor,
                FaceType = (AvatarFaceType)entity.FaceType,
                UseProfilePhotoAsFace = entity.UseProfilePhoto
            };
        }
    }

    internal sealed class MatchRuntimeState
    {
        public MatchRuntimeState(Guid matchId)
        {
            MatchId = matchId;
            Questions = new Queue<QuestionWithAnswersDto>();
            QuestionsById = new Dictionary<int, QuestionWithAnswersDto>();
            Players = new List<MatchPlayerRuntime>();
            SyncRoot = new object();
        }

        public Guid MatchId { get; }

        public byte Difficulty { get; private set; }

        public string LocaleCode { get; private set; }

        public Queue<QuestionWithAnswersDto> Questions { get; }

        public Dictionary<int, QuestionWithAnswersDto> QuestionsById { get; }

        public List<MatchPlayerRuntime> Players { get; }

        public int CurrentPlayerIndex { get; set; }

        public int CurrentStreak { get; set; }

        public decimal CurrentChain { get; set; }

        public decimal BankedPoints { get; set; }

        public int CurrentQuestionId { get; set; }

        public bool IsInitialized { get; private set; }

        public object SyncRoot { get; }

        public void Initialize(byte difficulty, string localeCode, List<QuestionWithAnswersDto> questions)
        {
            Difficulty = difficulty;
            LocaleCode = localeCode;

            Questions.Clear();
            QuestionsById.Clear();

            foreach (var question in questions)
            {
                Questions.Enqueue(question);
                QuestionsById[question.QuestionId] = question;
            }

            CurrentPlayerIndex = 0;
            CurrentStreak = 0;
            CurrentChain = 0m;
            BankedPoints = 0m;
            CurrentQuestionId = 0;
            IsInitialized = true;
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

            for (int index = 0; index < Players.Count; index++)
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

        public AvatarAppearanceDto Avatar { get; set; }
    }
}
