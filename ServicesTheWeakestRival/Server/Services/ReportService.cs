using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services.Reports;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = false, InstanceContextMode = InstanceContextMode.PerCall,
        ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class ReportService : IReportService
    {
        private readonly ReportRequestLogic _reportRequestLogic;

        public ReportService()
        {
            _reportRequestLogic = new ReportRequestLogic();
        }

        public SubmitPlayerReportResponse SubmitPlayerReport(SubmitPlayerReportRequest request)
        {
            return _reportRequestLogic.SubmitPlayerReport(request);
        }
    }
}
