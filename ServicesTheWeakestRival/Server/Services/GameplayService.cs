using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;
using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Logic;

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

        private const string ERROR_INVALID_REQUEST_MESSAGE = "Request is null.";

        private const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";

        private const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        private const int DEFAULT_MAX_QUESTIONS = 40;

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

        public BankResponse Bank(BankRequest request)
        {
            return new BankResponse
            {
                Bank = new BankState
                {
                    MatchId = request.MatchId,
                    CurrentChain = 0m,
                    BankedPoints = 1.0m
                }
            };
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
                using (var connection = new SqlConnection(GetConnectionString()))
                using (var command = new SqlCommand(QuestionsSql.Text.LIST_QUESTIONS_WITH_ANSWERS, connection))
                {
                    command.CommandType = CommandType.Text;

                    command.Parameters.Add("@MaxQuestions", SqlDbType.Int).Value = maxQuestions;
                    command.Parameters.Add("@Difficulty", SqlDbType.TinyInt).Value = request.Difficulty;
                    command.Parameters.Add("@LocaleCode", SqlDbType.NVarChar, 10).Value = request.LocaleCode;

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

                    var questions = new List<QuestionWithAnswersDto>(questionsById.Values);

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
            }
            catch (SqlException ex)
            {
                throw ThrowTechnicalFault(
                    ERROR_DB,
                    MESSAGE_DB_ERROR,
                    "GameplayService.GetQuestions",
                    ex);
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
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
    }
}
