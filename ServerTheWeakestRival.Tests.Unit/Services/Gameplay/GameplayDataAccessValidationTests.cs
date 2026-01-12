using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using System;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Tests.Gameplay
{
    [TestClass]
    public sealed class GameplayDataAccessValidationTests
    {
        private const byte DIFFICULTY_VALID = 1;
        private const byte DIFFICULTY_INVALID = 0;

        private const string LOCALE_VALID = "es-MX";
        private const string LOCALE_INVALID = " ";

        private const string TOKEN_VALID = "t";

        private const int MAX_QUESTIONS_VALID = 10;
        private const int MAX_QUESTIONS_INVALID_ZERO = 0;
        private const int MAX_QUESTIONS_INVALID_NEGATIVE = -1;

        private const int CATEGORY_ID_VALID = 1;
        private const int CATEGORY_ID_INVALID_ZERO = 0;
        private const int CATEGORY_ID_INVALID_NEGATIVE = -5;

        private const string LOCALE_TOO_LONG = "01234567890";

        [TestMethod]
        public void ValidateGetQuestionsRequest_RequestNull_ThrowsInvalidRequestFault()
        {
            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayDataAccess.ValidateGetQuestionsRequest(null));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void ValidateGetQuestionsRequest_DifficultyZero_ThrowsInvalidRequestFault()
        {
            var request = new GetQuestionsRequest
            {
                Difficulty = DIFFICULTY_INVALID,
                LocaleCode = LOCALE_VALID,
                Token = TOKEN_VALID
            };

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayDataAccess.ValidateGetQuestionsRequest(request));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void ValidateGetQuestionsRequest_LocaleCodeEmpty_ThrowsInvalidRequestFault()
        {
            var request = new GetQuestionsRequest
            {
                Difficulty = DIFFICULTY_VALID,
                LocaleCode = LOCALE_INVALID,
                Token = TOKEN_VALID
            };

            FaultException<ServiceFault> ex = AssertThrowsFault(() => GameplayDataAccess.ValidateGetQuestionsRequest(request));

            Assert.AreEqual(GameplayEngineConstants.ERROR_INVALID_REQUEST, ex.Detail.Code);
        }

        [TestMethod]
        public void ValidateGetQuestionsRequest_MinValidFieldsAndNoOptionals_DoesNotThrow()
        {
            var request = new GetQuestionsRequest
            {
                Difficulty = DIFFICULTY_VALID,
                LocaleCode = LOCALE_VALID,
                Token = TOKEN_VALID,
                MaxQuestions = null,
                CategoryId = null
            };

            GameplayDataAccess.ValidateGetQuestionsRequest(request);
        }

        [TestMethod]
        public void ValidateGetQuestionsRequest_ValidOptionals_DoesNotThrow()
        {
            var request = new GetQuestionsRequest
            {
                Difficulty = DIFFICULTY_VALID,
                LocaleCode = LOCALE_VALID,
                Token = TOKEN_VALID,
                MaxQuestions = MAX_QUESTIONS_VALID,
                CategoryId = CATEGORY_ID_VALID
            };

            GameplayDataAccess.ValidateGetQuestionsRequest(request);
        }

        [TestMethod]
        public void ValidateGetQuestionsRequest_LocaleCodeTooLong_DoesNotThrow_BecauseNotValidated()
        {
            var request = new GetQuestionsRequest
            {
                Difficulty = DIFFICULTY_VALID,
                LocaleCode = LOCALE_TOO_LONG,
                Token = TOKEN_VALID
            };

            GameplayDataAccess.ValidateGetQuestionsRequest(request);
        }

        [TestMethod]
        public void ValidateGetQuestionsRequest_MaxQuestionsZero_DoesNotThrow_BecauseNotValidated()
        {
            var request = new GetQuestionsRequest
            {
                Difficulty = DIFFICULTY_VALID,
                LocaleCode = LOCALE_VALID,
                Token = TOKEN_VALID,
                MaxQuestions = MAX_QUESTIONS_INVALID_ZERO
            };

            GameplayDataAccess.ValidateGetQuestionsRequest(request);
        }

        [TestMethod]
        public void ValidateGetQuestionsRequest_MaxQuestionsNegative_DoesNotThrow_BecauseNotValidated()
        {
            var request = new GetQuestionsRequest
            {
                Difficulty = DIFFICULTY_VALID,
                LocaleCode = LOCALE_VALID,
                Token = TOKEN_VALID,
                MaxQuestions = MAX_QUESTIONS_INVALID_NEGATIVE
            };

            GameplayDataAccess.ValidateGetQuestionsRequest(request);
        }

        [TestMethod]
        public void ValidateGetQuestionsRequest_CategoryIdZero_DoesNotThrow_BecauseNotValidated()
        {
            var request = new GetQuestionsRequest
            {
                Difficulty = DIFFICULTY_VALID,
                LocaleCode = LOCALE_VALID,
                Token = TOKEN_VALID,
                CategoryId = CATEGORY_ID_INVALID_ZERO
            };

            GameplayDataAccess.ValidateGetQuestionsRequest(request);
        }

        [TestMethod]
        public void ValidateGetQuestionsRequest_CategoryIdNegative_DoesNotThrow_BecauseNotValidated()
        {
            var request = new GetQuestionsRequest
            {
                Difficulty = DIFFICULTY_VALID,
                LocaleCode = LOCALE_VALID,
                Token = TOKEN_VALID,
                CategoryId = CATEGORY_ID_INVALID_NEGATIVE
            };

            GameplayDataAccess.ValidateGetQuestionsRequest(request);
        }

        private static FaultException<ServiceFault> AssertThrowsFault(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
                return null;
            }
            catch (FaultException<ServiceFault> ex)
            {
                Assert.IsNotNull(ex.Detail);
                Assert.IsFalse(string.IsNullOrWhiteSpace(ex.Detail.Code));
                return ex;
            }
        }
    }
}
