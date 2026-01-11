namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportTokenAuthenticator
    {
        internal int AuthenticateOrThrow(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.TokenInvalid, ReportConstants.MessageKey.TokenInvalid);
            }

            if (!TokenStore.TryGetUserId(token, out int userId) || userId <= 0)
            {
                throw ReportFaultFactory.Create(ReportConstants.FaultCode.TokenInvalid, ReportConstants.MessageKey.TokenInvalid);
            }

            return userId;
        }
    }
}
