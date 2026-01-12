namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportTokenAuthenticator : IReportTokenAuthenticator
    {
        public int AuthenticateOrThrow(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.TOKEN_INVALID, ReportConstants.MessageKey.TOKEN_INVALID);
            }

            if (!TokenStore.TryGetUserId(token, out int userId) || userId <= 0)
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.TOKEN_INVALID, ReportConstants.MessageKey.TOKEN_INVALID);
            }

            return userId;
        }
    }
}
