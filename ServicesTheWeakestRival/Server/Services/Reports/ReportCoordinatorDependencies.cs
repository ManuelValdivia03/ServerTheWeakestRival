using System;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportCoordinatorDependencies
    {
        internal IReportRequestValidator RequestValidator { get; set; }

        internal IReportTokenAuthenticator TokenAuthenticator { get; set; }

        internal IReportRepository ReportRepository { get; set; }

        internal IReportSanctionHandler SanctionHandler { get; set; }

        internal static ReportCoordinatorDependencies CreateDefaultDependencies()
        {
            var requestValidator = new ReportRequestValidator();
            var tokenAuthenticator = new ReportTokenAuthenticator();
            var reportRepository = new ReportRepository();

            var userIdResolver = new ReportUserIdResolver();
            var lobbyBroadcaster = new LobbyUpdatedBroadcaster();
            var forcedLogoutNotifier = new ForcedLogoutNotifier();

            var sanctionHandler = new ReportSanctionHandler(
                new ReportSanctionDependencies(userIdResolver, lobbyBroadcaster, forcedLogoutNotifier));

            return new ReportCoordinatorDependencies
            {
                RequestValidator = requestValidator,
                TokenAuthenticator = tokenAuthenticator,
                ReportRepository = reportRepository,
                SanctionHandler = sanctionHandler
            };
        }
    }
}
