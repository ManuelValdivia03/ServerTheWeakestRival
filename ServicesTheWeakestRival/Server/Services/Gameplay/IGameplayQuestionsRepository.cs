using ServicesTheWeakestRival.Contracts.Data;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal interface IGameplayQuestionsRepository
    {
        List<QuestionWithAnswersDto> LoadQuestions(
            SqlConnection connection,
            byte difficulty,
            string localeCode,
            int maxQuestions);
    }
}
