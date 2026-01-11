using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
using System;
using System.Configuration;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportCoordinator
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ReportCoordinator));

        private readonly ReportRequestValidator requestValidator;
        private readonly ReportTokenAuthenticator tokenAuthenticator;
        private readonly ReportRepository reportRepository;
        private readonly ReportSanctionHandler sanctionHandler;

        private ReportCoordinator(ReportCoordinatorDependencies dependencies)
        {
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));

            requestValidator = dependencies.RequestValidator;
            tokenAuthenticator = dependencies.TokenAuthenticator;
            reportRepository = dependencies.ReportRepository;
            sanctionHandler = dependencies.SanctionHandler;
        }

        internal static ReportCoordinator CreateDefault()
        {
            return new ReportCoordinator(ReportCoordinatorDependencies.CreateDefault());
        }

        internal SubmitPlayerReportResponse SubmitPlayerReport(SubmitPlayerReportRequest request)
        {
            requestValidator.ValidateSubmitPlayerReportRequest(request);

            int reporterAccountId = tokenAuthenticator.AuthenticateOrThrow(request.Token);

            requestValidator.ValidateReporterAndTarget(reporterAccountId, request.ReportedAccountId);

            try
            {
                SubmitPlayerReportResponse response = SqlExceptionFaultGuard.Execute(
                    operation: () => reportRepository.SubmitPlayerReport(reporterAccountId, request),
                    operationKeyPrefix: ReportConstants.OperationKeyPrefix.SubmitPlayerReport,
                    technicalErrorCode: ReportConstants.FaultCode.DbError,
                    context: ReportConstants.Context.SqlSubmitPlayerReport,
                    technicalFaultFactory: ReportFaultFactory.CreateTechnicalFault);

                sanctionHandler.HandleIfSanctionApplied(request, response);

                return response;
            }
            catch (FaultException<ServiceFault>)
            {
                throw;
            }
            catch (TimeoutException ex)
            {
                Logger.Error(ReportConstants.Context.TimeoutSubmit, ex);
                throw ReportFaultFactory.Create(
                    ReportConstants.FaultCode.Timeout,
                    ReportConstants.MessageKey.Timeout);
            }
            catch (CommunicationException ex)
            {
                Logger.Error(ReportConstants.Context.CommunicationSubmit, ex);
                throw ReportFaultFactory.Create(
                    ReportConstants.FaultCode.Communication,
                    ReportConstants.MessageKey.Communication);
            }
            catch (ConfigurationErrorsException ex)
            {
                Logger.Error(ReportConstants.Context.ConfigurationSubmit, ex);
                throw ReportFaultFactory.Create(
                    ReportConstants.FaultCode.Configuration,
                    ReportConstants.MessageKey.Configuration);
            }
            catch (Exception ex)
            {
                Logger.Error(ReportConstants.Context.UnexpectedSubmit, ex);
                throw ReportFaultFactory.Create(
                    ReportConstants.FaultCode.Unexpected,
                    ReportConstants.MessageKey.Unexpected);
            }
        }


        private sealed class ReportCoordinatorDependencies
        {
            internal ReportRequestValidator RequestValidator { get; }
            internal ReportTokenAuthenticator TokenAuthenticator { get; }
            internal ReportRepository ReportRepository { get; }
            internal ReportSanctionHandler SanctionHandler { get; }

            private ReportCoordinatorDependencies(
                ReportRequestValidator requestValidator,
                ReportTokenAuthenticator tokenAuthenticator,
                ReportRepository reportRepository,
                ReportSanctionHandler sanctionHandler)
            {
                RequestValidator = requestValidator ?? throw new ArgumentNullException(nameof(requestValidator));
                TokenAuthenticator = tokenAuthenticator ?? throw new ArgumentNullException(nameof(tokenAuthenticator));
                ReportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
                SanctionHandler = sanctionHandler ?? throw new ArgumentNullException(nameof(sanctionHandler));
            }

            internal static ReportCoordinatorDependencies CreateDefault()
            {
                var requestValidator = new ReportRequestValidator();
                var tokenAuthenticator = new ReportTokenAuthenticator();
                var reportRepository = new ReportRepository();

                var userIdResolver = new ReportUserIdResolver();
                var lobbyBroadcaster = new LobbyUpdatedBroadcaster();
                var forcedLogoutNotifier = new ForcedLogoutNotifier();

                var sanctionHandler = new ReportSanctionHandler(
                    new ReportSanctionDependencies(userIdResolver, lobbyBroadcaster, forcedLogoutNotifier));

                return new ReportCoordinatorDependencies(
                    requestValidator,
                    tokenAuthenticator,
                    reportRepository,
                    sanctionHandler);
            }
        }
    }
}
