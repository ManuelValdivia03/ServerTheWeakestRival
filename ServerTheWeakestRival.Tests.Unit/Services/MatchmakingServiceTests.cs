using System;
using System.Reflection;
using System.ServiceModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;

namespace ServerTheWeakestRival.Tests.Unit.Services
{
    [TestClass]
    public sealed class MatchmakingServiceTests
    {
        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_AUTH_REQUIRED = "AUTH_REQUIRED";
        private const string ERROR_AUTH_INVALID = "AUTH_INVALID";

        private static readonly PrivateType MatchmakingServicePrivateType = new PrivateType(typeof(MatchmakingService));

        private MatchmakingService service;

        [TestInitialize]
        public void SetUp()
        {
            service = new MatchmakingService();
        }

        private static ServiceFault AssertIsServiceFault(Exception ex, string expectedCode)
        {
            Assert.IsNotNull(ex);

            Exception actual = ex;

            if (ex is TargetInvocationException tie && tie.InnerException != null)
            {
                actual = tie.InnerException;
            }

            // FaultException<ServiceFault> tiene Detail
            var detailProperty = actual.GetType().GetProperty("Detail");
            Assert.IsNotNull(detailProperty, "Exception does not have Detail property.");

            var detail = detailProperty.GetValue(actual) as ServiceFault;
            Assert.IsNotNull(detail, "Detail is not a ServiceFault.");

            Assert.AreEqual(expectedCode, detail.Code);

            return detail;
        }

        #region CreateMatch

        [TestMethod]
        public void CreateMatch_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.CreateMatch(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void CreateMatch_NullToken_ThrowsAuthRequired()
        {
            var request = new CreateMatchRequest
            {
                Token = null
            };

            try
            {
                service.CreateMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void CreateMatch_EmptyToken_ThrowsAuthRequired()
        {
            var request = new CreateMatchRequest
            {
                Token = string.Empty
            };

            try
            {
                service.CreateMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void CreateMatch_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new CreateMatchRequest
            {
                Token = "   "
            };

            try
            {
                service.CreateMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void CreateMatch_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new CreateMatchRequest
            {
                Token = "invalid-token"
            };

            try
            {
                service.CreateMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region JoinMatch

        [TestMethod]
        public void JoinMatch_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.JoinMatch(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void JoinMatch_NullToken_ThrowsAuthRequired()
        {
            var request = new JoinMatchRequest
            {
                Token = null,
                MatchCode = "ABC123"
            };

            try
            {
                service.JoinMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void JoinMatch_EmptyToken_ThrowsAuthRequired()
        {
            var request = new JoinMatchRequest
            {
                Token = string.Empty,
                MatchCode = "ABC123"
            };

            try
            {
                service.JoinMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void JoinMatch_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new JoinMatchRequest
            {
                Token = "   ",
                MatchCode = "ABC123"
            };

            try
            {
                service.JoinMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void JoinMatch_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new JoinMatchRequest
            {
                Token = "invalid-token",
                MatchCode = "ABC123"
            };

            try
            {
                service.JoinMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region LeaveMatch

        [TestMethod]
        public void LeaveMatch_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.LeaveMatch(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void LeaveMatch_NullToken_ThrowsAuthRequired()
        {
            var request = new LeaveMatchRequest
            {
                Token = null,
                MatchId = Guid.NewGuid()
            };

            try
            {
                service.LeaveMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void LeaveMatch_EmptyToken_ThrowsAuthRequired()
        {
            var request = new LeaveMatchRequest
            {
                Token = string.Empty,
                MatchId = Guid.NewGuid()
            };

            try
            {
                service.LeaveMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void LeaveMatch_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new LeaveMatchRequest
            {
                Token = "   ",
                MatchId = Guid.NewGuid()
            };

            try
            {
                service.LeaveMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void LeaveMatch_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new LeaveMatchRequest
            {
                Token = "invalid-token",
                MatchId = Guid.NewGuid()
            };

            try
            {
                service.LeaveMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region StartMatch

        [TestMethod]
        public void StartMatch_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.StartMatch(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void StartMatch_NullToken_ThrowsAuthRequired()
        {
            var request = new StartMatchRequest
            {
                Token = null,
                MatchId = Guid.NewGuid()
            };

            try
            {
                service.StartMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void StartMatch_EmptyToken_ThrowsAuthRequired()
        {
            var request = new StartMatchRequest
            {
                Token = string.Empty,
                MatchId = Guid.NewGuid()
            };

            try
            {
                service.StartMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void StartMatch_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new StartMatchRequest
            {
                Token = "   ",
                MatchId = Guid.NewGuid()
            };

            try
            {
                service.StartMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void StartMatch_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new StartMatchRequest
            {
                Token = "invalid-token",
                MatchId = Guid.NewGuid()
            };

            try
            {
                service.StartMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region Helpers.Authenticate

        [TestMethod]
        public void Authenticate_NullToken_ThrowsAuthRequired()
        {
            try
            {
                MatchmakingServicePrivateType.InvokeStatic(
                    "Authenticate",
                    new object[] { null });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void Authenticate_EmptyToken_ThrowsAuthRequired()
        {
            try
            {
                MatchmakingServicePrivateType.InvokeStatic(
                    "Authenticate",
                    new object[] { string.Empty });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void Authenticate_WhitespaceToken_ThrowsAuthRequired()
        {
            try
            {
                MatchmakingServicePrivateType.InvokeStatic(
                    "Authenticate",
                    new object[] { "   " });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void Authenticate_InvalidToken_ThrowsAuthInvalid()
        {
            try
            {
                MatchmakingServicePrivateType.InvokeStatic(
                    "Authenticate",
                    new object[] { "invalid-token" });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion
    }
}
