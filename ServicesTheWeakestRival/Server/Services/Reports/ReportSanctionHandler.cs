using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportSanctionHandler
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ReportSanctionHandler));

        private const string ContextHandle = "ReportSanctionHandler.HandleIfSanctionApplied";

        private const string ForcedLogoutCodeSanctionApplied = "SANCTION_APPLIED";

        private readonly ReportUserIdResolver userIdResolver;
        private readonly LobbyUpdatedBroadcaster lobbyUpdatedBroadcaster;
        private readonly ForcedLogoutNotifier forcedLogoutNotifier;

        internal ReportSanctionHandler(ReportSanctionDependencies dependencies)
        {
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));

            userIdResolver = dependencies.UserIdResolver;
            lobbyUpdatedBroadcaster = dependencies.LobbyUpdatedBroadcaster;
            forcedLogoutNotifier = dependencies.ForcedLogoutNotifier;
        }

        internal void HandleIfSanctionApplied(SubmitPlayerReportRequest request, SubmitPlayerReportResponse response)
        {
            if (request == null || response == null || !response.SanctionApplied)
            {
                return;
            }

            try
            {
                int resolvedUserId = userIdResolver.ResolveUserIdFromAccountId(request.ReportedAccountId);
                int effectiveUserId = resolvedUserId > 0 ? resolvedUserId : request.ReportedAccountId;

                TokenStore.RevokeAllForUser(effectiveUserId);

                LobbyService.ForceLogoutAndKickFromLobby(
                    effectiveUserId,
                    response.SanctionType,
                    response.SanctionEndAtUtc);

                if (request.LobbyId.HasValue && request.LobbyId.Value != Guid.Empty)
                {
                    lobbyUpdatedBroadcaster.TryBroadcastLobbyUpdated(request.LobbyId.Value);
                }

                var notification = new ForcedLogoutNotification
                {
                    SanctionType = response.SanctionType,
                    SanctionEndAtUtc = response.SanctionEndAtUtc,
                    Code = ForcedLogoutCodeSanctionApplied
                };

                forcedLogoutNotifier.TrySendForcedLogoutToAccount(effectiveUserId, notification);
            }
            catch (Exception ex)
            {
                Logger.Warn(ContextHandle, ex);
            }
        }
    }

    internal sealed class ReportSanctionDependencies
    {
        internal ReportUserIdResolver UserIdResolver { get; }
        internal LobbyUpdatedBroadcaster LobbyUpdatedBroadcaster { get; }
        internal ForcedLogoutNotifier ForcedLogoutNotifier { get; }

        internal ReportSanctionDependencies(
            ReportUserIdResolver userIdResolver,
            LobbyUpdatedBroadcaster lobbyUpdatedBroadcaster,
            ForcedLogoutNotifier forcedLogoutNotifier)
        {
            UserIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            LobbyUpdatedBroadcaster = lobbyUpdatedBroadcaster ?? throw new ArgumentNullException(nameof(lobbyUpdatedBroadcaster));
            ForcedLogoutNotifier = forcedLogoutNotifier ?? throw new ArgumentNullException(nameof(forcedLogoutNotifier));
        }
    }
}
