using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Server.Services.Reports;
using System;
using System.ServiceModel;

namespace ServerTheWeakestRival.Tests.Unit.Services.Reports
{
    [TestClass]
    public sealed class ReportRequestValidatorTests
    {
        private const int VALID_REPORTER_ID = 10;
        private const int VALID_REPORTED_ID = 20;

        private ReportRequestValidator validator;

        [TestInitialize]
        public void TestInitialize()
        {
            validator = new ReportRequestValidator();
        }

        [TestMethod]
        public void ValidateSubmitPlayerReportRequest_WhenRequestIsNull_ThrowsFault()
        {
            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => validator.ValidateSubmitPlayerReportRequest(null));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.RequestNull,
                ReportConstants.MessageKey.RequestNull);
        }

        [TestMethod]
        public void ValidateSubmitPlayerReportRequest_WhenReasonIsInvalid_ThrowsFault()
        {
            var request = new SubmitPlayerReportRequest
            {
                Token = "t",
                ReportedAccountId = VALID_REPORTED_ID,
                LobbyId = null,
                ReasonCode = (ReportReasonCode)250,
                Comment = null
            };

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => validator.ValidateSubmitPlayerReportRequest(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.InvalidReason,
                ReportConstants.MessageKey.InvalidReason);
        }

        [TestMethod]
        public void ValidateSubmitPlayerReportRequest_WhenCommentTooLong_ThrowsFault()
        {
            const int extraChars = 1;

            var request = new SubmitPlayerReportRequest
            {
                Token = "t",
                ReportedAccountId = VALID_REPORTED_ID,
                LobbyId = null,
                ReasonCode = ReportReasonCode.Harassment,
                Comment = new string('a', ReportConstants.Sql.CommentMaxLength + extraChars)
            };

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => validator.ValidateSubmitPlayerReportRequest(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.CommentTooLong,
                ReportConstants.MessageKey.CommentTooLong);
        }

        [TestMethod]
        public void ValidateSubmitPlayerReportRequest_WhenCommentIsNull_DoesNotThrow()
        {
            var request = new SubmitPlayerReportRequest
            {
                Token = "t",
                ReportedAccountId = VALID_REPORTED_ID,
                LobbyId = null,
                ReasonCode = ReportReasonCode.Spam,
                Comment = null
            };

            validator.ValidateSubmitPlayerReportRequest(request);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void ValidateReporterAndTarget_WhenReportedAccountIdIsInvalid_ThrowsFault()
        {
            const int invalidReportedId = 0;

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => validator.ValidateReporterAndTarget(VALID_REPORTER_ID, invalidReportedId));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.InvalidTarget,
                ReportConstants.MessageKey.InvalidTarget);
        }

        [TestMethod]
        public void ValidateReporterAndTarget_WhenSelfReport_ThrowsFault()
        {
            const int sameId = 10;

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => validator.ValidateReporterAndTarget(sameId, sameId));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.SelfReport,
                ReportConstants.MessageKey.SelfReport);
        }

        [TestMethod]
        public void ValidateReporterAndTarget_WhenValid_DoesNotThrow()
        {
            validator.ValidateReporterAndTarget(VALID_REPORTER_ID, VALID_REPORTED_ID);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void ValidateSubmitPlayerReportRequest_WhenCommentIsExactlyMaxLength_DoesNotThrow()
        {
            var request = new SubmitPlayerReportRequest
            {
                Token = "t",
                ReportedAccountId = VALID_REPORTED_ID,
                LobbyId = null,
                ReasonCode = ReportReasonCode.Other,
                Comment = new string('a', ReportConstants.Sql.CommentMaxLength)
            };

            validator.ValidateSubmitPlayerReportRequest(request);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void ValidateSubmitPlayerReportRequest_WhenCommentIsWhitespace_DoesNotThrow()
        {
            var request = new SubmitPlayerReportRequest
            {
                Token = "t",
                ReportedAccountId = VALID_REPORTED_ID,
                LobbyId = null,
                ReasonCode = ReportReasonCode.Spam,
                Comment = "   "
            };

            validator.ValidateSubmitPlayerReportRequest(request);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void ValidateSubmitPlayerReportRequest_WhenReasonIsValidForAllEnumValues_DoesNotThrow()
        {
            ReportReasonCode[] reasons =
            {
        ReportReasonCode.Harassment,
        ReportReasonCode.Cheating,
        ReportReasonCode.Spam,
        ReportReasonCode.InappropriateName,
        ReportReasonCode.Other
    };

            foreach (ReportReasonCode reason in reasons)
            {
                var request = new SubmitPlayerReportRequest
                {
                    Token = "t",
                    ReportedAccountId = VALID_REPORTED_ID,
                    LobbyId = null,
                    ReasonCode = reason,
                    Comment = null
                };

                validator.ValidateSubmitPlayerReportRequest(request);
            }

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void ValidateReporterAndTarget_WhenReportedAccountIdIsNegative_ThrowsFault()
        {
            const int invalidReportedId = -1;

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => validator.ValidateReporterAndTarget(VALID_REPORTER_ID, invalidReportedId));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.InvalidTarget,
                ReportConstants.MessageKey.InvalidTarget);
        }

        [TestMethod]
        public void ValidateReporterAndTarget_WhenReporterAccountIdIsZero_AndTargetValid_DoesNotThrow()
        {
            const int reporterIdZero = 0;

            validator.ValidateReporterAndTarget(reporterIdZero, VALID_REPORTED_ID);

            Assert.IsTrue(true);
        }


        private static class FaultAssert
        {
            internal static FaultException<ServiceFault> CaptureFault(Action action)
            {
                try
                {
                    action();
                    Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
                    return null;
                }
                catch (FaultException<ServiceFault> ex)
                {
                    return ex;
                }
            }

            internal static void AssertFault(FaultException<ServiceFault> fault, string expectedCode, string expectedMessageKey)
            {
                Assert.IsNotNull(fault);
                Assert.IsNotNull(fault.Detail);

                Assert.AreEqual(expectedCode, fault.Detail.Code);
                Assert.AreEqual(expectedMessageKey, fault.Detail.Message);
            }
        }
    }
}
