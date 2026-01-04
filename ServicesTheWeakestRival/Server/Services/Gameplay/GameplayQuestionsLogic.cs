using ServicesTheWeakestRival.Contracts.Data;
using System;
using System.Collections.Generic;

namespace ServicesTheWeakestRival.Server.Services.Gameplay
{
    internal sealed class GameplayQuestionsLogic
    {
        private readonly IGameplayQuestionsRepository _questionsRepository;

        public GameplayQuestionsLogic(IGameplayQuestionsRepository questionsRepository)
        {
            _questionsRepository = questionsRepository ??
                throw new ArgumentNullException(nameof(questionsRepository));
        }

        public GetQuestionsResponse GetQuestions(GetQuestionsRequest request)
        {
            GameplayServiceContext.ValidateNotNullRequest(request);

            if (request.Difficulty <= 0)
            {
                throw GameplayServiceContext.ThrowFault(GameplayServiceContext.ERROR_INVALID_REQUEST, "Difficulty must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.LocaleCode))
            {
                throw GameplayServiceContext.ThrowFault(GameplayServiceContext.ERROR_INVALID_REQUEST, "LocaleCode is required.");
            }

            GameplayServiceContext.Authenticate(request.Token);

            int maxQuestions = GameplayServiceContext.GetMaxQuestionsOrDefault(request.MaxQuestions);

            return GameplayServiceContext.ExecuteDbOperation(
                GameplayServiceContext.CONTEXT_GET_QUESTIONS,
                connection =>
                {
                    List<QuestionWithAnswersDto> questions = _questionsRepository.LoadQuestions(
                        connection,
                        request.Difficulty,
                        request.LocaleCode,
                        maxQuestions);

                    GameplayServiceContext.Logger.InfoFormat(
                        "GetQuestions: Difficulty={0}, Locale={1}, RequestedMax={2}, Returned={3}",
                        request.Difficulty,
                        request.LocaleCode,
                        maxQuestions,
                        questions.Count);

                    return new GetQuestionsResponse
                    {
                        Questions = questions
                    };
                });
        }

        internal List<QuestionWithAnswersDto> LoadQuestionsWithLocaleFallback(
            byte difficulty,
            string localeCode,
            int maxQuestions)
        {
            return GameplayServiceContext.ExecuteDbOperation(
                "GameplayService.LoadQuestionsWithLocaleFallback",
                connection =>
                {
                    List<QuestionWithAnswersDto> questions = _questionsRepository.LoadQuestions(connection, difficulty, localeCode, maxQuestions);
                    if (questions.Count > 0)
                    {
                        return questions;
                    }

                    string languageOnly = ExtractLanguageCode(localeCode);
                    if (!string.IsNullOrWhiteSpace(languageOnly) &&
                        !string.Equals(languageOnly, localeCode, StringComparison.OrdinalIgnoreCase))
                    {
                        questions = _questionsRepository.LoadQuestions(connection, difficulty, languageOnly, maxQuestions);
                        if (questions.Count > 0)
                        {
                            return questions;
                        }
                    }

                    return _questionsRepository.LoadQuestions(connection, difficulty, GameplayServiceContext.FALLBACK_LOCALE_EN_US, maxQuestions);
                });
        }

        private static string ExtractLanguageCode(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return string.Empty;
            }

            string trimmed = localeCode.Trim();
            int dashIndex = trimmed.IndexOf('-');

            if (dashIndex <= 0)
            {
                return trimmed;
            }

            return trimmed.Substring(0, dashIndex);
        }
    }
}
