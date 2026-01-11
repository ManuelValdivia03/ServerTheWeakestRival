using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;

namespace ServicesTheWeakestRival.Server.Services.Reports
{
    internal sealed class ReportSanctionHandler
    {
        private const int MIN_VALID_ID = 1;

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
            bool shouldHandle = IsSanctionFlowApplicable(request, response);
            if (shouldHandle)
            {
                HandleSanctionAppliedSafe(request, response);
            }
        }

        private static bool IsSanctionFlowApplicable(SubmitPlayerReportRequest request, SubmitPlayerReportResponse response)
        {
            return request != null
                && response != null
                && response.SanctionApplied;
        }

        private void HandleSanctionAppliedSafe(SubmitPlayerReportRequest request, SubmitPlayerReportResponse response)
        {
            try
            {
                int effectiveUserId = ResolveEffectiveUserId(request.ReportedAccountId);

                RevokeAllSessions(effectiveUserId);
                ForceLogoutAndKickFromLobby(effectiveUserId, response);
                BroadcastLobbyUpdatedIfNeeded(request.LobbyId);
                NotifyForcedLogout(effectiveUserId, response);
            }
            catch (Exception ex)
            {
                Logger.Warn(ContextHandle, ex);
            }
        }

        private int ResolveEffectiveUserId(int reportedAccountId)
        {
            int resolvedUserId = userIdResolver.ResolveUserIdFromAccountId(reportedAccountId);

            bool hasResolvedUserId = resolvedUserId >= MIN_VALID_ID;
            if (hasResolvedUserId)
            {
                return resolvedUserId;
            }

            return reportedAccountId;
        }

        private static void RevokeAllSessions(int userId)
        {
            if (userId >= MIN_VALID_ID)
            {
                TokenStore.RevokeAllForUser(userId);
            }
        }

        private static void ForceLogoutAndKickFromLobby(int userId, SubmitPlayerReportResponse response)
        {
            if (userId >= MIN_VALID_ID && response != null)
            {
                LobbyService.ForceLogoutAndKickFromLobby(
                    userId,
                    response.SanctionType,
                    response.SanctionEndAtUtc);
            }
        }

        private void BroadcastLobbyUpdatedIfNeeded(Guid? lobbyId)
        {
            bool hasLobby = lobbyId.HasValue && lobbyId.Value != Guid.Empty;
            if (hasLobby)
            {
                lobbyUpdatedBroadcaster.TryBroadcastLobbyUpdated(lobbyId.Value);
            }
        }

        private void NotifyForcedLogout(int userId, SubmitPlayerReportResponse response)
        {
            if (userId >= MIN_VALID_ID && response != null)
            {
                ForcedLogoutNotification notification = BuildSanctionAppliedNotification(response);
                forcedLogoutNotifier.TrySendForcedLogoutToAccount(userId, notification);
            }
        }

        private static ForcedLogoutNotification BuildSanctionAppliedNotification(SubmitPlayerReportResponse response)
        {
            return new ForcedLogoutNotification
            {
                SanctionType = response.SanctionType,
                SanctionEndAtUtc = response.SanctionEndAtUtc,
                Code = ForcedLogoutCodeSanctionApplied
            };
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
