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

        private readonly IReportRequestValidator requestValidator;
        private readonly IReportTokenAuthenticator tokenAuthenticator;
        private readonly IReportRepository reportRepository;
        private readonly IReportSanctionHandler sanctionHandler;

        private ReportCoordinator(ReportCoordinatorDependencies dependencies)
        {
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));
            if (dependencies.RequestValidator == null) throw new ArgumentNullException(nameof(dependencies.RequestValidator));
            if (dependencies.TokenAuthenticator == null) throw new ArgumentNullException(nameof(dependencies.TokenAuthenticator));
            if (dependencies.ReportRepository == null) throw new ArgumentNullException(nameof(dependencies.ReportRepository));
            if (dependencies.SanctionHandler == null) throw new ArgumentNullException(nameof(dependencies.SanctionHandler));

            requestValidator = dependencies.RequestValidator;
            tokenAuthenticator = dependencies.TokenAuthenticator;
            reportRepository = dependencies.ReportRepository;
            sanctionHandler = dependencies.SanctionHandler;
        }

        internal static ReportCoordinator CreateDefault()
        {
            return new ReportCoordinator(ReportCoordinatorDependencies.CreateDefaultDependencies());
        }

        internal static ReportCoordinator CreateForTests(ReportCoordinatorDependencies dependencies)
        {
            return new ReportCoordinator(dependencies);
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
                    operationKeyPrefix: ReportConstants.OperationKeyPrefix.SUBMIT_PLAYER_REPORT,
                    technicalErrorCode: ReportConstants.FaultCode.DB_ERROR,
                    context: ReportConstants.Context.SQL_SUBMIT_PLAYER_REPORT,
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
                Logger.Error(ReportConstants.Context.TIMEOUT_SUBMIT, ex);
                throw ReportFaultFactory.Create(
                    ReportConstants.FaultCode.TIMEOUT,
                    ReportConstants.MessageKey.TIMEOUT);
            }
            catch (CommunicationException ex)
            {
                Logger.Error(ReportConstants.Context.COMMUNICATION_SUBMIT, ex);
                throw ReportFaultFactory.Create(
                    ReportConstants.FaultCode.COMMUNICATION,
                    ReportConstants.MessageKey.COMMUNICATION);
            }
            catch (ConfigurationErrorsException ex)
            {
                Logger.Error(ReportConstants.Context.CONFIGURATION_SUBMIT, ex);
                throw ReportFaultFactory.Create(
                    ReportConstants.FaultCode.CONFIGURATION,
                    ReportConstants.MessageKey.CONFIGURATION);
            }
            catch (Exception ex)
            {
                Logger.Error(ReportConstants.Context.UNEXPECTED_SUBMIT, ex);
                throw ReportFaultFactory.Create(
                    ReportConstants.FaultCode.UNEXPECTED,
                    ReportConstants.MessageKey.UNEXPECTED);
            }
        }
    }
}
