namespace ServicesTheWeakestRival.Server.Services.Logic
{
    public static class QuestionsSql
    {
        public static class Text
        {
            public const string LIST_QUESTIONS_WITH_ANSWERS = @"
                            ;WITH RandomQuestions AS
                            (
                                SELECT TOP (@MaxQuestions)
                                       q.question_id,
                                       q.category_id,
                                       q.difficulty,
                                       q.locale_code,
                                       q.body
                                FROM dbo.Questions q
                                WHERE q.is_active = 1
                                  AND q.difficulty = @Difficulty
                                  AND q.locale_code = @LocaleCode
                                ORDER BY NEWID()
                            )
                            SELECT 
                                   rq.question_id,
                                   rq.category_id,
                                   rq.difficulty,
                                   rq.locale_code,
                                   rq.body,
                                   a.answer_id,
                                   a.answer_text,
                                   a.is_correct,
                                   a.display_ord
                            FROM RandomQuestions rq
                            INNER JOIN dbo.Answers a
                                ON a.question_id = rq.question_id
                            ORDER BY rq.question_id, a.display_ord;";

        }
    }
}
