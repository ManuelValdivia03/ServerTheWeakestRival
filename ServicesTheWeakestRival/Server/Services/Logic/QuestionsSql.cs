namespace ServicesTheWeakestRival.Server.Services.Logic
{
    public static class QuestionsSql
    {
        public static class Text
        {
            public const string LIST_QUESTIONS_WITH_ANSWERS = @"
                SELECT TOP (@MaxQuestions)
                       q.question_id,
                       q.category_id,
                       q.difficulty,
                       q.locale_code,
                       q.body,
                       a.answer_id,
                       a.answer_text,
                       a.is_correct,
                       a.display_ord
                FROM dbo.Questions q
                INNER JOIN dbo.Answers a
                    ON a.question_id = q.question_id
                WHERE q.is_active = 1
                  AND q.difficulty = @Difficulty
                  AND q.locale_code = @LocaleCode
                ORDER BY NEWID(), q.question_id, a.display_ord;";
        }
    }
}
