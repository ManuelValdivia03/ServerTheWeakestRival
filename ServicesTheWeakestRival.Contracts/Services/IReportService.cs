using ServicesTheWeakestRival.Contracts.Data;
using System.ServiceModel;

namespace ServicesTheWeakestRival.Contracts.Services
{
    [ServiceContract]
    public interface IReportService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        SubmitPlayerReportResponse SubmitPlayerReport(SubmitPlayerReportRequest request);
    }
}
