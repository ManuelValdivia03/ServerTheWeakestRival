using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;

namespace ServerTheWeakestRival.Tests.Unit.Services
{
    [TestClass]
    public sealed class ChatServiceTests
    {
        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_VALIDATION_ERROR = "VALIDATION_ERROR";
        private const string ERROR_UNAUTHORIZED = "UNAUTHORIZED";

        private const int MAX_MESSAGE_LENGTH = 500;

        private static readonly PrivateType ChatServicePrivateType = new PrivateType(typeof(ChatService));

        private ChatService service;

        [TestInitialize]
        public void SetUp()
        {
            service = new ChatService();
        }

        private static ServiceFault AssertIsServiceFault(Exception ex, string expectedCode)
        {
            Assert.IsNotNull(ex);

            Exception actual = ex;

            if (ex is TargetInvocationException tie && tie.InnerException != null)
            {
                actual = tie.InnerException;
            }

            var detailProperty = actual.GetType().GetProperty("Detail");
            Assert.IsNotNull(detailProperty, "Exception does not have Detail property.");

            var detail = detailProperty.GetValue(actual) as ServiceFault;
            Assert.IsNotNull(detail, "Detail is not a ServiceFault.");

            Assert.AreEqual(expectedCode, detail.Code);

            return detail;
        }

        #region SendChatMessage

        [TestMethod]
        public void SendChatMessage_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.SendChatMessage(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request cannot be null.", detail.Message);
            }
        }

        [TestMethod]
        public void SendChatMessage_EmptyMessage_ThrowsValidationError()
        {
            var request = new SendChatMessageRequest
            {
                AuthToken = "token",
                MessageText = ""
            };

            try
            {
                service.SendChatMessage(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_VALIDATION_ERROR);
                Assert.AreEqual("MessageText cannot be empty.", detail.Message);
            }
        }

        [TestMethod]
        public void SendChatMessage_WhitespaceMessage_ThrowsValidationError()
        {
            var request = new SendChatMessageRequest
            {
                AuthToken = "token",
                MessageText = "   "
            };

            try
            {
                service.SendChatMessage(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_VALIDATION_ERROR);
                Assert.AreEqual("MessageText cannot be empty.", detail.Message);
            }
        }

        [TestMethod]
        public void SendChatMessage_MessageTooLong_ThrowsValidationError()
        {
            var longText = new string('x', MAX_MESSAGE_LENGTH + 1);

            var request = new SendChatMessageRequest
            {
                AuthToken = "token",
                MessageText = longText
            };

            try
            {
                service.SendChatMessage(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_VALIDATION_ERROR);
                Assert.AreEqual(
                    "MessageText exceeds " + MAX_MESSAGE_LENGTH + " characters.",
                    detail.Message);
            }
        }

        [TestMethod]
        public void SendChatMessage_NullAuthToken_ThrowsUnauthorized()
        {
            var request = new SendChatMessageRequest
            {
                AuthToken = null,
                MessageText = "Hola"
            };

            try
            {
                service.SendChatMessage(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.AreEqual("Auth token is required.", detail.Message);
            }
        }

        [TestMethod]
        public void SendChatMessage_EmptyAuthToken_ThrowsUnauthorized()
        {
            var request = new SendChatMessageRequest
            {
                AuthToken = "",
                MessageText = "Hola"
            };

            try
            {
                service.SendChatMessage(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.AreEqual("Auth token is required.", detail.Message);
            }
        }

        [TestMethod]
        public void SendChatMessage_InvalidAuthToken_ThrowsUnauthorized()
        {
            var request = new SendChatMessageRequest
            {
                AuthToken = "invalid",
                MessageText = "Hola"
            };

            try
            {
                service.SendChatMessage(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.AreEqual("Auth token is invalid.", detail.Message);
            }
        }

        #endregion

        #region GetChatMessages

        [TestMethod]
        public void GetChatMessages_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.GetChatMessages(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request cannot be null.", detail.Message);
            }
        }

        [TestMethod]
        public void GetChatMessages_NullToken_ThrowsUnauthorized()
        {
            var request = new GetChatMessagesRequest
            {
                AuthToken = null
            };

            try
            {
                service.GetChatMessages(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.AreEqual("Auth token is required.", detail.Message);
            }
        }

        [TestMethod]
        public void GetChatMessages_EmptyToken_ThrowsUnauthorized()
        {
            var request = new GetChatMessagesRequest
            {
                AuthToken = ""
            };

            try
            {
                service.GetChatMessages(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.AreEqual("Auth token is required.", detail.Message);
            }
        }

        [TestMethod]
        public void GetChatMessages_WhitespaceToken_ThrowsUnauthorized()
        {
            var request = new GetChatMessagesRequest
            {
                AuthToken = "   "
            };

            try
            {
                service.GetChatMessages(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.AreEqual("Auth token is required.", detail.Message);
            }
        }

        [TestMethod]
        public void GetChatMessages_InvalidToken_ThrowsUnauthorized()
        {
            var request = new GetChatMessagesRequest
            {
                AuthToken = "invalid",
                SinceChatMessageId = 0,
                MaxCount = 10
            };

            try
            {
                service.GetChatMessages(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.AreEqual("Auth token is invalid.", detail.Message);
            }
        }

        #endregion

        #region EnsureAuthorizedAndGetUserId

        [TestMethod]
        public void EnsureAuthorizedAndGetUserId_NullToken_ThrowsUnauthorized()
        {
            try
            {
                ChatServicePrivateType.InvokeStatic(
                    "EnsureAuthorizedAndGetUserId",
                    new object[] { null });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.AreEqual("Auth token is required.", detail.Message);
            }
        }

        [TestMethod]
        public void EnsureAuthorizedAndGetUserId_WhitespaceToken_ThrowsUnauthorized()
        {
            try
            {
                ChatServicePrivateType.InvokeStatic(
                    "EnsureAuthorizedAndGetUserId",
                    new object[] { "   " });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.AreEqual("Auth token is required.", detail.Message);
            }
        }

        [TestMethod]
        public void EnsureAuthorizedAndGetUserId_InvalidToken_ThrowsUnauthorized()
        {
            try
            {
                ChatServicePrivateType.InvokeStatic(
                    "EnsureAuthorizedAndGetUserId",
                    new object[] { "invalid" });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.AreEqual("Auth token is invalid.", detail.Message);
            }
        }

        #endregion
    }
}
