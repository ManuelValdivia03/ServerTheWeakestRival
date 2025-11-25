using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services;

namespace ServerTheWeakestRival.Tests.Unit.Services
{
    [TestClass]
    public sealed class AuthServiceTests
    {
        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_EMAIL_TAKEN = "EMAIL_TAKEN";
        private const string ERROR_INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
        private const string ERROR_ACCOUNT_BLOCKED = "ACCOUNT_BLOCKED";
        private const string ERROR_DB_ERROR = "DB_ERROR";

        private const string ERROR_CODE_MISSING = "CODE_MISSING";
        private const string ERROR_CODE_EXPIRED = "CODE_EXPIRED";
        private const string ERROR_CODE_INVALID = "CODE_INVALID";

        private const string ERROR_TOO_SOON = "TOO_SOON";
        private const string ERROR_SMTP = "SMTP_ERROR";

        private const string ERROR_PAYLOAD_NULL = "Request payload is null.";

        private const int PASSWORD_MIN_LENGTH = 8;

        private static readonly PrivateType AuthServicePrivateType = new PrivateType(typeof(AuthService));

        private AuthService service;

        [TestInitialize]
        public void SetUp()
        {
            service = new AuthService();
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

        #region Ping

        [TestMethod]
        public void Ping_NullRequest_ReturnsDefaultPong()
        {
            PingResponse response = service.Ping(null);

            Assert.IsNotNull(response);
            Assert.AreEqual("pong", response.Echo);
        }

        [TestMethod]
        public void Ping_WithMessage_EchoesSameMessage()
        {
            var request = new PingRequest
            {
                Message = "hello"
            };

            PingResponse response = service.Ping(request);

            Assert.AreEqual("hello", response.Echo);
        }

        [TestMethod]
        public void Ping_WhitespaceMessage_ReturnsPong()
        {
            var request = new PingRequest
            {
                Message = "   "
            };

            PingResponse response = service.Ping(request);

            Assert.AreEqual("pong", response.Echo);
        }

        [TestMethod]
        public void Ping_EmptyMessage_ReturnsPong()
        {
            var request = new PingRequest
            {
                Message = string.Empty
            };

            PingResponse response = service.Ping(request);

            Assert.AreEqual("pong", response.Echo);
        }

        [TestMethod]
        public void Ping_UtcIsCloseToNow()
        {
            DateTime before = DateTime.UtcNow;

            PingResponse response = service.Ping(null);

            DateTime after = DateTime.UtcNow;

            Assert.IsTrue(response.Utc >= before);
            Assert.IsTrue(response.Utc <= after);
        }

        #endregion

        #region BeginRegister

        [TestMethod]
        public void BeginRegister_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.BeginRegister(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email is required.", detail.Message);
            }
        }

        [TestMethod]
        public void BeginRegister_NullEmail_ThrowsInvalidRequest()
        {
            var request = new BeginRegisterRequest
            {
                Email = null
            };

            try
            {
                service.BeginRegister(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email is required.", detail.Message);
            }
        }

        [TestMethod]
        public void BeginRegister_EmptyEmail_ThrowsInvalidRequest()
        {
            var request = new BeginRegisterRequest
            {
                Email = string.Empty
            };

            try
            {
                service.BeginRegister(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email is required.", detail.Message);
            }
        }

        [TestMethod]
        public void BeginRegister_WhitespaceEmail_ThrowsInvalidRequest()
        {
            var request = new BeginRegisterRequest
            {
                Email = "   "
            };

            try
            {
                service.BeginRegister(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email is required.", detail.Message);
            }
        }

        [TestMethod]
        public void BeginRegister_TabAndSpaceEmail_ThrowsInvalidRequest()
        {
            var request = new BeginRegisterRequest
            {
                Email = " \t "
            };

            try
            {
                service.BeginRegister(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email is required.", detail.Message);
            }
        }

        #endregion

        #region CompleteRegister

        [TestMethod]
        public void CompleteRegister_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.CompleteRegister(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual(ERROR_PAYLOAD_NULL, detail.Message);
            }
        }

        [TestMethod]
        public void CompleteRegister_EmptyEmail_ThrowsInvalidRequest()
        {
            var request = new CompleteRegisterRequest
            {
                Email = "",
                DisplayName = "User",
                Password = "Password123",
                Code = "123456"
            };

            try
            {
                service.CompleteRegister(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email, display name, password and code are required.", detail.Message);
            }
        }

        [TestMethod]
        public void CompleteRegister_EmptyDisplayName_ThrowsInvalidRequest()
        {
            var request = new CompleteRegisterRequest
            {
                Email = "user@example.com",
                DisplayName = " ",
                Password = "Password123",
                Code = "123456"
            };

            try
            {
                service.CompleteRegister(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email, display name, password and code are required.", detail.Message);
            }
        }

        [TestMethod]
        public void CompleteRegister_EmptyPassword_ThrowsInvalidRequest()
        {
            var request = new CompleteRegisterRequest
            {
                Email = "user@example.com",
                DisplayName = "User",
                Password = " ",
                Code = "123456"
            };

            try
            {
                service.CompleteRegister(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email, display name, password and code are required.", detail.Message);
            }
        }

        [TestMethod]
        public void CompleteRegister_EmptyCode_ThrowsInvalidRequest()
        {
            var request = new CompleteRegisterRequest
            {
                Email = "user@example.com",
                DisplayName = "User",
                Password = "Password123",
                Code = " "
            };

            try
            {
                service.CompleteRegister(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email, display name, password and code are required.", detail.Message);
            }
        }

        #endregion

        #region Register

        [TestMethod]
        public void Register_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.Register(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual(ERROR_PAYLOAD_NULL, detail.Message);
            }
        }

        #endregion

        #region Login

        [TestMethod]
        public void Login_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.Login(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual(ERROR_PAYLOAD_NULL, detail.Message);
            }
        }

        #endregion

        #region Logout

        [TestMethod]
        public void Logout_NullRequest_DoesNothing()
        {
            service.Logout(null);
        }

        [TestMethod]
        public void Logout_EmptyToken_DoesNothing()
        {
            var request = new LogoutRequest
            {
                Token = string.Empty
            };

            service.Logout(request);
        }

        [TestMethod]
        public void Logout_WhitespaceToken_DoesNothing()
        {
            var request = new LogoutRequest
            {
                Token = "   "
            };

            service.Logout(request);
        }

        [TestMethod]
        public void Logout_NullToken_DoesNothing()
        {
            var request = new LogoutRequest
            {
                Token = null
            };

            service.Logout(request);
        }

        [TestMethod]
        public void Logout_TokenWithSpacesOnly_DoesNothing()
        {
            var request = new LogoutRequest
            {
                Token = " \t "
            };

            service.Logout(request);
        }

        #endregion

        #region BeginPasswordReset

        [TestMethod]
        public void BeginPasswordReset_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.BeginPasswordReset(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email is required.", detail.Message);
            }
        }

        [TestMethod]
        public void BeginPasswordReset_NullEmail_ThrowsInvalidRequest()
        {
            var request = new BeginPasswordResetRequest
            {
                Email = null
            };

            try
            {
                service.BeginPasswordReset(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email is required.", detail.Message);
            }
        }

        [TestMethod]
        public void BeginPasswordReset_EmptyEmail_ThrowsInvalidRequest()
        {
            var request = new BeginPasswordResetRequest
            {
                Email = string.Empty
            };

            try
            {
                service.BeginPasswordReset(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email is required.", detail.Message);
            }
        }

        [TestMethod]
        public void BeginPasswordReset_WhitespaceEmail_ThrowsInvalidRequest()
        {
            var request = new BeginPasswordResetRequest
            {
                Email = "   "
            };

            try
            {
                service.BeginPasswordReset(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email is required.", detail.Message);
            }
        }

        [TestMethod]
        public void BeginPasswordReset_TabAndSpaceEmail_ThrowsInvalidRequest()
        {
            var request = new BeginPasswordResetRequest
            {
                Email = " \t "
            };

            try
            {
                service.BeginPasswordReset(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email is required.", detail.Message);
            }
        }

        #endregion

        #region CompletePasswordReset

        [TestMethod]
        public void CompletePasswordReset_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.CompletePasswordReset(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual(ERROR_PAYLOAD_NULL, detail.Message);
            }
        }

        [TestMethod]
        public void CompletePasswordReset_EmptyEmail_ThrowsInvalidRequest()
        {
            var request = new CompletePasswordResetRequest
            {
                Email = "",
                Code = "123456",
                NewPassword = "NewPassword123"
            };

            try
            {
                service.CompletePasswordReset(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email, code and new password are required.", detail.Message);
            }
        }

        [TestMethod]
        public void CompletePasswordReset_EmptyCode_ThrowsInvalidRequest()
        {
            var request = new CompletePasswordResetRequest
            {
                Email = "user@example.com",
                Code = " ",
                NewPassword = "NewPassword123"
            };

            try
            {
                service.CompletePasswordReset(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email, code and new password are required.", detail.Message);
            }
        }

        [TestMethod]
        public void CompletePasswordReset_EmptyNewPassword_ThrowsInvalidRequest()
        {
            var request = new CompletePasswordResetRequest
            {
                Email = "user@example.com",
                Code = "123456",
                NewPassword = " "
            };

            try
            {
                service.CompletePasswordReset(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Email, code and new password are required.", detail.Message);
            }
        }

        [TestMethod]
        public void CompletePasswordReset_TooShortPassword_ThrowsWeakPassword()
        {
            var request = new CompletePasswordResetRequest
            {
                Email = "user@example.com",
                Code = "123456",
                NewPassword = new string('a', PASSWORD_MIN_LENGTH - 1)
            };

            try
            {
                service.CompletePasswordReset(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, "WEAK_PASSWORD");
                Assert.AreEqual("New password does not meet the minimum length requirements.", detail.Message);
            }
        }

        #endregion

        #region HelperMethods

        [TestMethod]
        public void IssueToken_ValidUserId_ReturnsTokenAndStoresInCache()
        {
            const int userId = 123;

            var token = (AuthToken)AuthServicePrivateType.InvokeStatic(
                "IssueToken",
                userId);

            Assert.IsNotNull(token);
            Assert.AreEqual(userId, token.UserId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(token.Token));
            Assert.IsTrue(token.ExpiresAtUtc > DateTime.UtcNow);
        }

        [TestMethod]
        public void HashPasswordBc_AndVerifyPasswordBc_AreCompatible()
        {
            const string password = "StrongPassword123";

            string hash = (string)AuthServicePrivateType.InvokeStatic(
                "HashPasswordBc",
                password);

            Assert.IsFalse(string.IsNullOrWhiteSpace(hash));

            bool isValid = (bool)AuthServicePrivateType.InvokeStatic(
                "VerifyPasswordBc",
                password,
                hash);

            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void VerifyPasswordBc_WrongPassword_ReturnsFalse()
        {
            const string password = "StrongPassword123";

            string hash = (string)AuthServicePrivateType.InvokeStatic(
                "HashPasswordBc",
                password);

            bool isValid = (bool)AuthServicePrivateType.InvokeStatic(
                "VerifyPasswordBc",
                "OtherPassword",
                hash);

            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void VerifyPasswordBc_EmptyHash_ReturnsFalse()
        {
            bool isValid = (bool)AuthServicePrivateType.InvokeStatic(
                "VerifyPasswordBc",
                "password",
                "");

            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ThrowFault_BuildsFaultWithCodeAndMessage()
        {
            const string code = ERROR_EMAIL_TAKEN;
            const string message = "Email is already registered.";

            var ex = (Exception)AuthServicePrivateType.InvokeStatic(
                "ThrowFault",
                code,
                message);

            ServiceFault detail = AssertIsServiceFault(ex, ERROR_EMAIL_TAKEN);
            Assert.AreEqual(message, detail.Message);
        }

        [TestMethod]
        public void ThrowTechnicalFault_BuildsFaultWithTechnicalCodeAndUserMessage()
        {
            const string code = ERROR_DB_ERROR;
            const string userMessage = "Unexpected database error.";
            const string context = "AuthServiceTests.ThrowTechnicalFault";

            var inner = new InvalidOperationException("Inner");

            var ex = (Exception)AuthServicePrivateType.InvokeStatic(
                "ThrowTechnicalFault",
                code,
                userMessage,
                context,
                inner);

            ServiceFault detail = AssertIsServiceFault(ex, ERROR_DB_ERROR);
            Assert.AreEqual(userMessage, detail.Message);
        }

        #endregion
    }
}
