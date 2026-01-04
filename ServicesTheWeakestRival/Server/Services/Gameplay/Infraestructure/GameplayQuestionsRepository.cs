using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Gameplay.Infrastructure
{
    internal sealed class GameplayQuestionsRepository : IGameplayQuestionsRepository
    {
        private const int LOCALE_CODE_LENGTH = 10;

        public List<QuestionWithAnswersDto> LoadQuestions(
            SqlConnection connection,
            byte difficulty,
            string localeCode,
            int maxQuestions)
        {
            var questionsById = new Dictionary<int, QuestionWithAnswersDto>();

            using (SqlCommand command = new SqlCommand(QuestionsSql.Text.LIST_QUESTIONS_WITH_ANSWERS, connection))
            {
                command.CommandType = CommandType.Text;

                command.Parameters.Add("@MaxQuestions", SqlDbType.Int).Value = maxQuestions;
                command.Parameters.Add("@Difficulty", SqlDbType.TinyInt).Value = difficulty;
                command.Parameters.Add("@LocaleCode", SqlDbType.NVarChar, LOCALE_CODE_LENGTH).Value = localeCode;

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

                        AnswerDto answer = new AnswerDto
                        {
                            AnswerId = reader.GetInt32(5),
                            Text = reader.GetString(6),
                            IsCorrect = reader.GetBoolean(7),
                            DisplayOrder = reader.GetByte(8)
                        };

                        question.Answers.Add(answer);
                    }
                }
            }

            return new List<QuestionWithAnswersDto>(questionsById.Values);
        }
    }
}
