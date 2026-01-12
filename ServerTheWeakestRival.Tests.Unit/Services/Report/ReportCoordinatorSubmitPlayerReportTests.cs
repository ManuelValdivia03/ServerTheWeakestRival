using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Server.Services.Reports;
using System;
using System.Configuration;
using System.ServiceModel;

namespace ServerTheWeakestRival.Tests.Unit.Services.Reports
{
    [TestClass]
    public sealed class ReportCoordinatorSubmitPlayerReportTests
    {
        private const string VALID_TOKEN = "token";
        private const int REPORTER_ACCOUNT_ID = 10;
        private const int REPORTED_ACCOUNT_ID = 20;
        private const int ALT_REPORTER_ACCOUNT_ID = 77;

        private const long REPORT_ID = 99;

        private const byte SANCTION_TYPE = 2;

        private static readonly DateTime SANCTION_END_UTC =
            new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly SubmitPlayerReportResponse SUCCESS_NO_SANCTION =
            new SubmitPlayerReportResponse
            {
                ReportId = REPORT_ID,
                SanctionApplied = false,
                SanctionType = 0,
                SanctionEndAtUtc = null
            };

        private static readonly SubmitPlayerReportResponse SUCCESS_WITH_SANCTION =
            new SubmitPlayerReportResponse
            {
                ReportId = REPORT_ID,
                SanctionApplied = true,
                SanctionType = SANCTION_TYPE,
                SanctionEndAtUtc = SANCTION_END_UTC
            };

        [TestMethod]
        public void SubmitPlayerReport_WhenSuccess_CallsDependencies_AndReturnsResponse()
        {
            var fakeValidator = new FakeValidator();
            var fakeAuthenticator = new FakeAuthenticator(REPORTER_ACCOUNT_ID);
            var fakeRepository = FakeRepository.Returning(SUCCESS_NO_SANCTION);
            var fakeSanctionHandler = new FakeSanctionHandler();

            ReportCoordinator coordinator = CreateCoordinator(
                fakeValidator,
                fakeAuthenticator,
                fakeRepository,
                fakeSanctionHandler);

            SubmitPlayerReportRequest request = BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Spam);

            SubmitPlayerReportResponse response = coordinator.SubmitPlayerReport(request);

            Assert.IsNotNull(response);
            Assert.AreEqual(REPORT_ID, response.ReportId);

            Assert.IsTrue(fakeValidator.WasValidateRequestCalled);
            Assert.IsTrue(fakeValidator.WasValidateReporterAndTargetCalled);

            Assert.AreEqual(VALID_TOKEN, fakeAuthenticator.LastToken);

            Assert.AreEqual(REPORTER_ACCOUNT_ID, fakeRepository.LastReporterAccountId);
            Assert.AreSame(request, fakeRepository.LastRequest);

            Assert.IsTrue(fakeSanctionHandler.WasCalled);
            Assert.AreSame(request, fakeSanctionHandler.LastRequest);
            Assert.AreSame(response, fakeSanctionHandler.LastResponse);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenRepositoryThrowsTimeout_MapsToTimeoutFault()
        {
            var fakeRepository = FakeRepository.Throwing(new TimeoutException("timeout"));

            ReportCoordinator coordinator = CreateCoordinator(fakeRepository);

            SubmitPlayerReportRequest request = BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Spam);

            FaultException<ServiceFault> fault = FaultAssert.CaptureFault(() => coordinator.SubmitPlayerReport(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.Timeout,
                ReportConstants.MessageKey.Timeout);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenRepositoryThrowsCommunication_MapsToCommunicationFault()
        {
            var fakeRepository = FakeRepository.Throwing(new CommunicationException("comm"));

            ReportCoordinator coordinator = CreateCoordinator(fakeRepository);

            SubmitPlayerReportRequest request = BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Spam);

            FaultException<ServiceFault> fault = FaultAssert.CaptureFault(() => coordinator.SubmitPlayerReport(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.Communication,
                ReportConstants.MessageKey.Communication);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenRepositoryThrowsConfiguration_MapsToConfigurationFault()
        {
            var fakeRepository = FakeRepository.Throwing(new ConfigurationErrorsException("cfg"));

            ReportCoordinator coordinator = CreateCoordinator(fakeRepository);

            SubmitPlayerReportRequest request = BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Spam);

            FaultException<ServiceFault> fault = FaultAssert.CaptureFault(() => coordinator.SubmitPlayerReport(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.Configuration,
                ReportConstants.MessageKey.Configuration);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenRepositoryThrowsUnexpected_MapsToUnexpectedFault()
        {
            var fakeRepository = FakeRepository.Throwing(new InvalidOperationException("boom"));

            ReportCoordinator coordinator = CreateCoordinator(fakeRepository);

            SubmitPlayerReportRequest request = BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Spam);

            FaultException<ServiceFault> fault = FaultAssert.CaptureFault(() => coordinator.SubmitPlayerReport(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.Unexpected,
                ReportConstants.MessageKey.Unexpected);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenRepositoryThrowsServiceFault_RethrowsSameFault()
        {
            FaultException<ServiceFault> expected = ReportFaultFactory.Create(
                ReportConstants.FaultCode.DbError,
                ReportConstants.MessageKey.Unexpected);

            var fakeRepository = FakeRepository.Throwing(expected);

            ReportCoordinator coordinator = CreateCoordinator(fakeRepository);

            SubmitPlayerReportRequest request = BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Spam);

            FaultException<ServiceFault> actual = FaultAssert.CaptureFault(() => coordinator.SubmitPlayerReport(request));

            Assert.AreEqual(expected.Detail.Code, actual.Detail.Code);
            Assert.AreEqual(expected.Detail.Message, actual.Detail.Message);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenSanctionApplied_StillCallsSanctionHandler()
        {
            var fakeRepository = FakeRepository.Returning(SUCCESS_WITH_SANCTION);
            var fakeSanctionHandler = new FakeSanctionHandler();

            ReportCoordinator coordinator = CreateCoordinator(fakeRepository, fakeSanctionHandler);

            SubmitPlayerReportRequest request = BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Harassment);

            SubmitPlayerReportResponse response = coordinator.SubmitPlayerReport(request);

            Assert.IsNotNull(response);
            Assert.IsTrue(response.SanctionApplied);

            Assert.IsTrue(fakeSanctionHandler.WasCalled);
            Assert.AreSame(request, fakeSanctionHandler.LastRequest);
            Assert.AreSame(response, fakeSanctionHandler.LastResponse);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenValidateRequestThrowsFault_Rethrows_AndDoesNotCallAuthRepoOrSanction()
        {
            FaultException<ServiceFault> expected = ReportFaultFactory.Create(
                ReportConstants.FaultCode.RequestNull,
                ReportConstants.MessageKey.RequestNull);

            var fakeValidator = FakeValidator.ThrowingOnValidateRequest(expected);
            var fakeAuthenticator = new FakeAuthenticator(REPORTER_ACCOUNT_ID);
            var fakeRepository = FakeRepository.Returning(SUCCESS_NO_SANCTION);
            var fakeSanctionHandler = new FakeSanctionHandler();

            ReportCoordinator coordinator = CreateCoordinator(
                fakeValidator,
                fakeAuthenticator,
                fakeRepository,
                fakeSanctionHandler);

            FaultException<ServiceFault> actual =
                FaultAssert.CaptureFault(() => coordinator.SubmitPlayerReport(null));

            FaultAssert.AssertFault(actual, expected.Detail.Code, expected.Detail.Message);

            Assert.IsFalse(fakeAuthenticator.WasCalled);
            Assert.IsFalse(fakeRepository.WasCalled);
            Assert.IsFalse(fakeSanctionHandler.WasCalled);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenAuthenticatorThrowsFault_Rethrows_AndDoesNotCallRepoOrSanction()
        {
            FaultException<ServiceFault> expected = ReportFaultFactory.Create(
                ReportConstants.FaultCode.TokenInvalid,
                ReportConstants.MessageKey.TokenInvalid);

            var fakeValidator = new FakeValidator();
            var fakeAuthenticator = FakeAuthenticator.Throwing(expected);
            var fakeRepository = FakeRepository.Returning(SUCCESS_NO_SANCTION);
            var fakeSanctionHandler = new FakeSanctionHandler();

            ReportCoordinator coordinator = CreateCoordinator(
                fakeValidator,
                fakeAuthenticator,
                fakeRepository,
                fakeSanctionHandler);

            SubmitPlayerReportRequest request =
                BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Spam);

            FaultException<ServiceFault> actual =
                FaultAssert.CaptureFault(() => coordinator.SubmitPlayerReport(request));

            FaultAssert.AssertFault(actual, expected.Detail.Code, expected.Detail.Message);

            Assert.IsFalse(fakeRepository.WasCalled);
            Assert.IsFalse(fakeSanctionHandler.WasCalled);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenValidateReporterAndTargetThrowsFault_Rethrows_AndDoesNotCallRepoOrSanction()
        {
            FaultException<ServiceFault> expected = ReportFaultFactory.Create(
                ReportConstants.FaultCode.SelfReport,
                ReportConstants.MessageKey.SelfReport);

            var fakeValidator = FakeValidator.ThrowingOnValidateReporterAndTarget(expected);
            var fakeAuthenticator = new FakeAuthenticator(REPORTER_ACCOUNT_ID);
            var fakeRepository = FakeRepository.Returning(SUCCESS_NO_SANCTION);
            var fakeSanctionHandler = new FakeSanctionHandler();

            ReportCoordinator coordinator = CreateCoordinator(
                fakeValidator,
                fakeAuthenticator,
                fakeRepository,
                fakeSanctionHandler);

            SubmitPlayerReportRequest request =
                BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Spam);

            FaultException<ServiceFault> actual =
                FaultAssert.CaptureFault(() => coordinator.SubmitPlayerReport(request));

            FaultAssert.AssertFault(actual, expected.Detail.Code, expected.Detail.Message);

            Assert.IsFalse(fakeRepository.WasCalled);
            Assert.IsFalse(fakeSanctionHandler.WasCalled);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenSuccess_UsesUserIdReturnedByAuthenticator()
        {
            var fakeValidator = new FakeValidator();
            var fakeAuthenticator = new FakeAuthenticator(ALT_REPORTER_ACCOUNT_ID);
            var fakeRepository = FakeRepository.Returning(SUCCESS_NO_SANCTION);
            var fakeSanctionHandler = new FakeSanctionHandler();

            ReportCoordinator coordinator = CreateCoordinator(
                fakeValidator,
                fakeAuthenticator,
                fakeRepository,
                fakeSanctionHandler);

            SubmitPlayerReportRequest request =
                BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Spam);

            coordinator.SubmitPlayerReport(request);

            Assert.AreEqual(ALT_REPORTER_ACCOUNT_ID, fakeRepository.LastReporterAccountId);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenSuccess_PassesSameRequestInstanceToRepository()
        {
            var fakeRepository = FakeRepository.Returning(SUCCESS_NO_SANCTION);

            ReportCoordinator coordinator = CreateCoordinator(fakeRepository);

            SubmitPlayerReportRequest request =
                BuildRequest(VALID_TOKEN, REPORTED_ACCOUNT_ID, ReportReasonCode.Spam);

            coordinator.SubmitPlayerReport(request);

            Assert.AreSame(request, fakeRepository.LastRequest);
        }

        private static ReportCoordinator CreateCoordinator(IReportRepository repository)
        {
            var fakeValidator = new FakeValidator();
            var fakeAuthenticator = new FakeAuthenticator(REPORTER_ACCOUNT_ID);
            var fakeSanctionHandler = new FakeSanctionHandler();

            return CreateCoordinator(fakeValidator, fakeAuthenticator, repository, fakeSanctionHandler);
        }

        private static ReportCoordinator CreateCoordinator(IReportRepository repository, IReportSanctionHandler sanctionHandler)
        {
            var fakeValidator = new FakeValidator();
            var fakeAuthenticator = new FakeAuthenticator(REPORTER_ACCOUNT_ID);

            return CreateCoordinator(fakeValidator, fakeAuthenticator, repository, sanctionHandler);
        }

        private static ReportCoordinator CreateCoordinator(
            IReportRequestValidator validator,
            IReportTokenAuthenticator authenticator,
            IReportRepository repository,
            IReportSanctionHandler sanctionHandler)
        {
            var dependencies = new ReportCoordinatorDependencies
            {
                RequestValidator = validator,
                TokenAuthenticator = authenticator,
                ReportRepository = repository,
                SanctionHandler = sanctionHandler
            };

            return ReportCoordinator.CreateForTests(dependencies);
        }

        private static SubmitPlayerReportRequest BuildRequest(string token, int reportedAccountId, ReportReasonCode reason)
        {
            return new SubmitPlayerReportRequest
            {
                Token = token,
                ReportedAccountId = reportedAccountId,
                LobbyId = null,
                ReasonCode = reason,
                Comment = null
            };
        }

        private sealed class FakeValidator : IReportRequestValidator
        {
            private readonly FaultException<ServiceFault> validateRequestFault;
            private readonly FaultException<ServiceFault> validateReporterAndTargetFault;

            internal bool WasValidateRequestCalled { get; private set; }

            internal bool WasValidateReporterAndTargetCalled { get; private set; }

            internal FakeValidator()
                : this(null, null)
            {
            }

            private FakeValidator(
                FaultException<ServiceFault> validateRequestFault,
                FaultException<ServiceFault> validateReporterAndTargetFault)
            {
                this.validateRequestFault = validateRequestFault;
                this.validateReporterAndTargetFault = validateReporterAndTargetFault;
            }

            internal static FakeValidator ThrowingOnValidateRequest(FaultException<ServiceFault> fault)
            {
                return new FakeValidator(fault, null);
            }

            internal static FakeValidator ThrowingOnValidateReporterAndTarget(FaultException<ServiceFault> fault)
            {
                return new FakeValidator(null, fault);
            }

            public void ValidateSubmitPlayerReportRequest(SubmitPlayerReportRequest request)
            {
                WasValidateRequestCalled = true;

                if (validateRequestFault != null)
                {
                    throw validateRequestFault;
                }
            }

            public void ValidateReporterAndTarget(int reporterAccountId, int reportedAccountId)
            {
                WasValidateReporterAndTargetCalled = true;

                if (validateReporterAndTargetFault != null)
                {
                    throw validateReporterAndTargetFault;
                }
            }
        }

        private sealed class FakeAuthenticator : IReportTokenAuthenticator
        {
            private readonly int userIdToReturn;
            private readonly FaultException<ServiceFault> faultToThrow;

            internal bool WasCalled { get; private set; }

            internal string LastToken { get; private set; }

            internal FakeAuthenticator(int userIdToReturn)
                : this(userIdToReturn, null)
            {
            }

            private FakeAuthenticator(int userIdToReturn, FaultException<ServiceFault> faultToThrow)
            {
                this.userIdToReturn = userIdToReturn;
                this.faultToThrow = faultToThrow;
            }

            internal static FakeAuthenticator Throwing(FaultException<ServiceFault> fault)
            {
                return new FakeAuthenticator(0, fault);
            }

            public int AuthenticateOrThrow(string token)
            {
                WasCalled = true;
                LastToken = token;

                if (faultToThrow != null)
                {
                    throw faultToThrow;
                }

                return userIdToReturn;
            }
        }

        private sealed class FakeRepository : IReportRepository
        {
            private readonly SubmitPlayerReportResponse responseToReturn;
            private readonly Exception exceptionToThrow;

            internal bool WasCalled { get; private set; }

            internal int LastReporterAccountId { get; private set; }

            internal SubmitPlayerReportRequest LastRequest { get; private set; }

            private FakeRepository(SubmitPlayerReportResponse responseToReturn, Exception exceptionToThrow)
            {
                this.responseToReturn = responseToReturn;
                this.exceptionToThrow = exceptionToThrow;
            }

            internal static FakeRepository Returning(SubmitPlayerReportResponse response)
            {
                return new FakeRepository(response, null);
            }

            internal static FakeRepository Throwing(Exception exception)
            {
                return new FakeRepository(null, exception);
            }

            public SubmitPlayerReportResponse SubmitPlayerReport(int reporterAccountId, SubmitPlayerReportRequest request)
            {
                WasCalled = true;

                LastReporterAccountId = reporterAccountId;
                LastRequest = request;

                if (exceptionToThrow != null)
                {
                    throw exceptionToThrow;
                }

                return responseToReturn;
            }
        }

        private sealed class FakeSanctionHandler : IReportSanctionHandler
        {
            internal bool WasCalled { get; private set; }

            internal int CallCount { get; private set; }

            internal SubmitPlayerReportRequest LastRequest { get; private set; }

            internal SubmitPlayerReportResponse LastResponse { get; private set; }

            public void HandleIfSanctionApplied(SubmitPlayerReportRequest request, SubmitPlayerReportResponse response)
            {
                WasCalled = true;
                CallCount++;

                LastRequest = request;
                LastResponse = response;
            }
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

            internal static void AssertFault(
                FaultException<ServiceFault> fault,
                string expectedCode,
                string expectedMessageKey)
            {
                Assert.IsNotNull(fault);
                Assert.IsNotNull(fault.Detail);

                Assert.AreEqual(expectedCode, fault.Detail.Code);
                Assert.AreEqual(expectedMessageKey, fault.Detail.Message);
            }
        }
    }
}
