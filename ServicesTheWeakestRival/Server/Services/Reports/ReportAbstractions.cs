using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal interface IReportRequestValidator
    {
        void ValidateSubmitPlayerReportRequest(SubmitPlayerReportRequest request);

        void ValidateReporterAndTarget(int reporterAccountId, int reportedAccountId);
    }

    internal interface IReportTokenAuthenticator
    {
        int AuthenticateOrThrow(string token);
    }

    internal interface IReportRepository
    {
        SubmitPlayerReportResponse SubmitPlayerReport(int reporterAccountId, SubmitPlayerReportRequest request);
    }

    internal interface IReportSanctionHandler
    {
        void HandleIfSanctionApplied(SubmitPlayerReportRequest request, SubmitPlayerReportResponse response);
    }
}
