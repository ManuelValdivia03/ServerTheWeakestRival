using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplayDataAccess
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(GameplayDataAccess));

        internal static int GetMaxQuestionsOrDefault(int? requested)
        {
            return requested.HasValue && requested.Value > 0
                ? requested.Value
                : GameplayEngineConstants.DEFAULT_MAX_QUESTIONS;
        }

        internal static void ValidateGetQuestionsRequest(GetQuestionsRequest request)
        {
            if (request == null)
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Request is null.");
            }

            if (request.Difficulty <= 0)
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "Difficulty must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.LocaleCode))
            {
                throw GameplayFaults.ThrowFault(GameplayEngineConstants.ERROR_INVALID_REQUEST, "LocaleCode is required.");
            }
        }

        internal static string GetConnectionString()
        {
            ConnectionStringSettings configurationString =
                ConfigurationManager.ConnectionStrings[GameplayEngineConstants.MAIN_CONNECTION_STRING_NAME];

            if (configurationString == null || string.IsNullOrWhiteSpace(configurationString.ConnectionString))
            {
                Logger.ErrorFormat("Missing connection string '{0}'.", GameplayEngineConstants.MAIN_CONNECTION_STRING_NAME);

                throw GameplayFaults.ThrowTechnicalFault(
                    "CONFIG_ERROR",
                    "Configuration error. Please contact support.",
                    "GameplayEngine.GetConnectionString",
                    new ConfigurationErrorsException(
                        string.Format(CultureInfo.InvariantCulture, "Missing connection string '{0}'.", GameplayEngineConstants.MAIN_CONNECTION_STRING_NAME)));
            }

            return configurationString.ConnectionString;
        }

        internal static List<QuestionWithAnswersDto> LoadQuestionsWithLocaleFallback(byte difficulty, string localeCode, int maxQuestions)
        {
            List<QuestionWithAnswersDto> questions = LoadQuestions(difficulty, localeCode, maxQuestions);
            if (questions.Count > 0)
            {
                return questions;
            }

            string languageOnly = ExtractLanguageCode(localeCode);
            if (!string.IsNullOrWhiteSpace(languageOnly) &&
                !string.Equals(languageOnly, localeCode, StringComparison.OrdinalIgnoreCase))
            {
                questions = LoadQuestions(difficulty, languageOnly, maxQuestions);
                if (questions.Count > 0)
                {
                    return questions;
                }
            }

            return LoadQuestions(difficulty, GameplayEngineConstants.FALLBACK_LOCALE_EN_US, maxQuestions);
        }

        private static string ExtractLanguageCode(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return string.Empty;
            }

            string trimmed = localeCode.Trim();
            int dashIndex = trimmed.IndexOf('-');
            return dashIndex > 0 ? trimmed.Substring(0, dashIndex) : trimmed;
        }

        internal static List<QuestionWithAnswersDto> LoadQuestions(byte difficulty, string localeCode, int maxQuestions)
        {
            List<QuestionWithAnswersDto> result = new List<QuestionWithAnswersDto>();

            using (SqlConnection connection = new SqlConnection(GetConnectionString()))
            using (SqlCommand command = new SqlCommand(QuestionsSql.Text.LIST_QUESTIONS_WITH_ANSWERS, connection))
            {
                command.CommandType = CommandType.Text;

                command.Parameters.Add("@MaxQuestions", SqlDbType.Int).Value = maxQuestions;
                command.Parameters.Add("@Difficulty", SqlDbType.TinyInt).Value = difficulty;

                string safeLocale = (localeCode ?? string.Empty).Trim();
                if (safeLocale.Length > GameplayEngineConstants.LOCALE_CODE_MAX_LENGTH)
                {
                    safeLocale = safeLocale.Substring(0, GameplayEngineConstants.LOCALE_CODE_MAX_LENGTH);
                }

                command.Parameters.Add("@LocaleCode", SqlDbType.NVarChar, GameplayEngineConstants.LOCALE_CODE_MAX_LENGTH).Value = safeLocale;

                connection.Open();

                Dictionary<int, QuestionWithAnswersDto> questionsById =
                    new Dictionary<int, QuestionWithAnswersDto>();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int questionId = reader.GetInt32(0);

                        if (!questionsById.TryGetValue(questionId, out QuestionWithAnswersDto question))
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

                        question.Answers.Add(new AnswerDto
                        {
                            AnswerId = reader.GetInt32(5),
                            Text = reader.GetString(6),
                            IsCorrect = reader.GetBoolean(7),
                            DisplayOrder = reader.GetByte(8)
                        });
                    }
                }

                result = new List<QuestionWithAnswersDto>(questionsById.Values);
            }

            return result;
        }

        internal static AvatarAppearanceDto MapAvatar(UserAvatarEntity entity)
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

        internal static UserAvatarEntity GetAvatarByUserId(int userId)
        {
            return new UserAvatarSql(GetConnectionString()).GetByUserId(userId);
        }
    }
}
