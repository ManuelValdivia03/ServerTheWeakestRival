using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Enums;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Auth;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.Reports;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;

namespace ServerTheWeakestRival.Tests.Integration.Services.Reports
{
    [TestClass]
    public sealed class ReportServiceSubmitPlayerReportIntegrationTests
    {
        private const int REPORTS_REQUIRED = 5;
        private const int REPORTS_WINDOW_MINUTES = 15;

        private const int DUPLICATE_COOLDOWN_MINUTES = 1;
        private const int MAX_TEMPORARY_SANCTIONS = 3;
        private const int BAN_ON_SANCTION_NUMBER = 4;
        private const int MODERATION_POLICY_ID = 1;

        private const string SQL_PROVIDER_NAME = "System.Data.SqlClient";

        private const string EMAIL_DOMAIN = "@test.local";
        private const string PASSWORD_STRONG = "Password123!";
        private const string DISPLAY_NAME = "Report Test User";

        private const int TOKEN_TTL_MINUTES = 30;
        private const string TOKEN_PREFIX = "token-reporter-";

        private const int SANCTION_1_DURATION_MINUTES = 10;
        private const int SANCTION_2_DURATION_MINUTES = 60;
        private const int SANCTION_3_DURATION_MINUTES = 1440;
        private const int SANCTION_DURATION_TOLERANCE_SECONDS = 120;

        private const int OUTSIDE_WINDOW_EXTRA_MINUTES = 1;

        private static readonly string SqlMessageKeyPrefix =
            string.Concat(ReportConstants.OperationKeyPrefix.SubmitPlayerReport, ".Sql.");

        private static readonly object RegistrationSyncRoot = new object();

        private AuthRepository authRepository;
        private ReportService service;

        private int reportedAccountId;
        private int[] reporterAccountIds;

        private static readonly object SanctionSchemaSyncRoot = new object();
        private static SanctionSchema cachedSanctionSchema;

        [TestInitialize]
        public void TestInitialize()
        {
            DbTestCleaner.CleanupAll();
            TokenStoreTestCleaner.ClearAllTokens();

            EnsureReportConnectionStringIsAvailable();

            authRepository = new AuthRepository(() => DbTestConfig.GetMainConnectionString());

            EnsureModerationPolicy();
            EnsureReportReasons();
            EnsureSanctionEscalationPolicy();

            RegisterReportableAccounts();

            service = new ReportService();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            TokenStoreTestCleaner.ClearAllTokens();
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenUnderThreshold_DoesNotApplySanction()
        {
            int reportsToSend = REPORTS_REQUIRED - 1;

            for (int i = 0; i < reportsToSend; i++)
            {
                int reporterId = reporterAccountIds[i];
                string token = TOKEN_PREFIX + reporterId;

                AddToken(token, reporterId);

                SubmitPlayerReportResponse response = service.SubmitPlayerReport(
                    BuildRequest(token, reportedAccountId, ReportReasonCode.Spam));

                Assert.IsTrue(response.ReportId > 0);
                Assert.IsFalse(response.SanctionApplied);
            }
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenFifthReportWithinWindow_AppliesSanction()
        {
            for (int i = 0; i < REPORTS_REQUIRED; i++)
            {
                int reporterId = reporterAccountIds[i];
                string token = TOKEN_PREFIX + reporterId;

                AddToken(token, reporterId);

                SubmitPlayerReportResponse response = service.SubmitPlayerReport(
                    BuildRequest(token, reportedAccountId, ReportReasonCode.Harassment));

                Assert.IsTrue(response.ReportId > 0);

                bool isLastReport = i == REPORTS_REQUIRED - 1;
                if (!isLastReport)
                {
                    Assert.IsFalse(response.SanctionApplied);
                }
                else
                {
                    Assert.IsTrue(response.SanctionApplied);
                }
            }
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenTokenIsUnknown_ThrowsTokenInvalid()
        {
            SubmitPlayerReportRequest request =
                BuildRequest("token-unknown", reportedAccountId, ReportReasonCode.Spam);

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => service.SubmitPlayerReport(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.TokenInvalid,
                ReportConstants.MessageKey.TokenInvalid);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenTokenIsExpired_ThrowsTokenInvalid()
        {
            const string expiredTokenValue = "token-expired";

            var token = new AuthToken
            {
                Token = expiredTokenValue,
                UserId = reporterAccountIds[0],
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
            };

            TokenStore.StoreToken(token);

            SubmitPlayerReportRequest request =
                BuildRequest(expiredTokenValue, reportedAccountId, ReportReasonCode.Spam);

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => service.SubmitPlayerReport(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.TokenInvalid,
                ReportConstants.MessageKey.TokenInvalid);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenReasonIsInvalid_ThrowsInvalidReason()
        {
            int reporterId = reporterAccountIds[0];

            string token = TOKEN_PREFIX + reporterId;
            AddToken(token, reporterId);

            var request = new SubmitPlayerReportRequest
            {
                Token = token,
                ReportedAccountId = reportedAccountId,
                LobbyId = null,
                ReasonCode = (ReportReasonCode)250,
                Comment = null
            };

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => service.SubmitPlayerReport(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.InvalidReason,
                ReportConstants.MessageKey.InvalidReason);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenCommentTooLong_ThrowsCommentTooLong()
        {
            const int extraChars = 1;

            int reporterId = reporterAccountIds[0];

            string token = TOKEN_PREFIX + reporterId;
            AddToken(token, reporterId);

            var request = new SubmitPlayerReportRequest
            {
                Token = token,
                ReportedAccountId = reportedAccountId,
                LobbyId = null,
                ReasonCode = ReportReasonCode.Spam,
                Comment = new string('a', ReportConstants.Sql.CommentMaxLength + extraChars)
            };

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => service.SubmitPlayerReport(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.CommentTooLong,
                ReportConstants.MessageKey.CommentTooLong);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenSelfReport_ThrowsSelfReport()
        {
            int sameAccountId = reporterAccountIds[0];

            string token = TOKEN_PREFIX + sameAccountId;
            AddToken(token, sameAccountId);

            SubmitPlayerReportRequest request =
                BuildRequest(token, sameAccountId, ReportReasonCode.Spam);

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => service.SubmitPlayerReport(request));

            FaultAssert.AssertFault(
                fault,
                ReportConstants.FaultCode.SelfReport,
                ReportConstants.MessageKey.SelfReport);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenSuccess_PersistsPlayerReportRow()
        {
            int reporterId = reporterAccountIds[0];
            string token = TOKEN_PREFIX + reporterId;

            AddToken(token, reporterId);

            SubmitPlayerReportResponse response = service.SubmitPlayerReport(
                BuildRequest(token, reportedAccountId, ReportReasonCode.Spam));

            Assert.IsTrue(response.ReportId > 0);

            PlayerReportLookupResult lookup = ReadPlayerReport(response.ReportId);
            Assert.IsTrue(lookup.Found);

            Assert.AreEqual(reporterId, lookup.ReporterAccountId);
            Assert.AreEqual(reportedAccountId, lookup.ReportedAccountId);
            Assert.AreEqual((byte)ReportReasonCode.Spam, lookup.ReasonCode);
            Assert.AreEqual(string.Empty, lookup.Comment);
            Assert.IsFalse(lookup.LobbyId.HasValue);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenCommentProvided_PersistsComment()
        {
            const string comment = "test comment";

            int reporterId = reporterAccountIds[0];
            string token = TOKEN_PREFIX + reporterId;

            AddToken(token, reporterId);

            var request = new SubmitPlayerReportRequest
            {
                Token = token,
                ReportedAccountId = reportedAccountId,
                LobbyId = null,
                ReasonCode = ReportReasonCode.Other,
                Comment = comment
            };

            SubmitPlayerReportResponse response = service.SubmitPlayerReport(request);

            Assert.IsTrue(response.ReportId > 0);

            PlayerReportLookupResult lookup = ReadPlayerReport(response.ReportId);
            Assert.IsTrue(lookup.Found);

            Assert.IsFalse(lookup.LobbyId.HasValue);
            Assert.AreEqual(comment, lookup.Comment);
            Assert.AreEqual((byte)ReportReasonCode.Other, lookup.ReasonCode);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenOneReportIsOutsideWindow_DoesNotApplySanction()
        {
            int reporter0 = reporterAccountIds[0];
            string token0 = TOKEN_PREFIX + reporter0;
            AddToken(token0, reporter0);

            SubmitPlayerReportResponse first = service.SubmitPlayerReport(
                BuildRequest(token0, reportedAccountId, ReportReasonCode.Harassment));

            Assert.IsTrue(first.ReportId > 0);

            DateTime outsideWindowUtc =
                DateTime.UtcNow.AddMinutes(-(REPORTS_WINDOW_MINUTES + OUTSIDE_WINDOW_EXTRA_MINUTES));

            UpdatePlayerReportCreatedAtUtc(first.ReportId, outsideWindowUtc);

            SubmitPlayerReportResponse last = null;

            for (int i = 1; i < REPORTS_REQUIRED; i++)
            {
                int reporterId = reporterAccountIds[i];
                string token = TOKEN_PREFIX + reporterId;

                AddToken(token, reporterId);

                last = service.SubmitPlayerReport(
                    BuildRequest(token, reportedAccountId, ReportReasonCode.Harassment));
            }

            Assert.IsNotNull(last);
            Assert.IsFalse(last.SanctionApplied);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenSameReporterReportsSameTargetWithinCooldown_ThrowsDbSqlFault()
        {
            int reporterId = reporterAccountIds[0];
            string token = TOKEN_PREFIX + reporterId;

            AddToken(token, reporterId);

            SubmitPlayerReportResponse first = service.SubmitPlayerReport(
                BuildRequest(token, reportedAccountId, ReportReasonCode.Spam));

            Assert.IsTrue(first.ReportId > 0);

            FaultException<ServiceFault> fault =
                FaultAssert.CaptureFault(() => service.SubmitPlayerReport(
                    BuildRequest(token, reportedAccountId, ReportReasonCode.Spam)));

            AssertDbSqlFault(fault);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenNoSanctionApplied_PersistsNullAppliedSanctionId()
        {
            int reporterId = reporterAccountIds[0];
            string token = TOKEN_PREFIX + reporterId;

            AddToken(token, reporterId);

            SubmitPlayerReportResponse response = service.SubmitPlayerReport(
                BuildRequest(token, reportedAccountId, ReportReasonCode.Spam));

            Assert.IsTrue(response.ReportId > 0);
            Assert.IsFalse(response.SanctionApplied);

            PlayerReportRow row = ReadPlayerReportRow(response.ReportId);
            Assert.IsTrue(row.Found);

            Assert.IsFalse(row.AppliedSanctionId.HasValue);
        }

        [TestMethod]
        public void SubmitPlayerReport_WhenSanctionApplied_PersistsAppliedSanctionId()
        {
            SubmitPlayerReportResponse last =
                SendReportsAndReturnLastResponse(reporterAccountIds, ReportReasonCode.Harassment);

            Assert.IsNotNull(last);
            Assert.IsTrue(last.ReportId > 0);
            Assert.IsTrue(last.SanctionApplied);

            PlayerReportRow row = ReadPlayerReportRow(last.ReportId);
            Assert.IsTrue(row.Found);

            Assert.IsTrue(row.AppliedSanctionId.HasValue);
            Assert.IsTrue(row.AppliedSanctionId.Value > 0);
        }

        private static SubmitPlayerReportRequest BuildRequest(string token, int reportedId, ReportReasonCode reason)
        {
            return new SubmitPlayerReportRequest
            {
                Token = token,
                ReportedAccountId = reportedId,
                LobbyId = null,
                ReasonCode = reason,
                Comment = null
            };
        }

        private void RegisterReportableAccounts()
        {
            lock (RegistrationSyncRoot)
            {
                reportedAccountId = CreateAccountAndGetAccountId("reported");

                reporterAccountIds = new int[REPORTS_REQUIRED];
                for (int i = 0; i < REPORTS_REQUIRED; i++)
                {
                    reporterAccountIds[i] = CreateAccountAndGetAccountId("reporter");
                }

                Assert.IsTrue(reportedAccountId > 0);
                Assert.IsNotNull(reporterAccountIds);
                Assert.AreEqual(REPORTS_REQUIRED, reporterAccountIds.Length);
            }
        }

        private int CreateAccountAndGetAccountId(string prefix)
        {
            string email = BuildEmail(prefix);

            string passwordHash = PasswordService.Hash(PASSWORD_STRONG);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(null, null));

            authRepository.CreateAccountAndUser(data);

            int accountId = ReadAccountIdByEmail(email);
            Assert.IsTrue(accountId > 0);

            return accountId;
        }

        private static string BuildEmail(string prefix)
        {
            return string.Concat(
                "tc.report.",
                prefix,
                ".",
                Guid.NewGuid().ToString("N"),
                EMAIL_DOMAIN);
        }

        private static int ReadAccountIdByEmail(string email)
        {
            const string sql = @"
SELECT TOP (1) account_id
FROM dbo.Accounts
WHERE email = @Email;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = email ?? string.Empty;

                connection.Open();

                object value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
        }

        private static void AddToken(string tokenValue, int accountId)
        {
            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                Assert.Fail("tokenValue must be non-empty.");
            }

            var token = new AuthToken
            {
                Token = tokenValue,
                UserId = accountId,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(TOKEN_TTL_MINUTES)
            };

            TokenStore.StoreToken(token);
        }

        private static void EnsureModerationPolicy()
        {
            const string sql = @"
IF EXISTS (SELECT 1 FROM dbo.moderation_policy WHERE policy_id = @policy_id)
BEGIN
    UPDATE dbo.moderation_policy
    SET reports_required = @reports_required,
        reports_window_minutes = @reports_window_minutes,
        duplicate_cooldown_minutes = @duplicate_cooldown_minutes,
        max_temporary_sanctions = @max_temporary_sanctions,
        ban_on_sanction_number = @ban_on_sanction_number
    WHERE policy_id = @policy_id;
END
ELSE
BEGIN
    INSERT INTO dbo.moderation_policy
    (
        policy_id,
        reports_required,
        reports_window_minutes,
        duplicate_cooldown_minutes,
        max_temporary_sanctions,
        ban_on_sanction_number
    )
    VALUES
    (
        @policy_id,
        @reports_required,
        @reports_window_minutes,
        @duplicate_cooldown_minutes,
        @max_temporary_sanctions,
        @ban_on_sanction_number
    );
END
";

            ExecuteNonQuery(
                sql,
                cmd =>
                {
                    cmd.Parameters.Add("@policy_id", SqlDbType.Int).Value = MODERATION_POLICY_ID;

                    cmd.Parameters.Add("@reports_required", SqlDbType.Int).Value = REPORTS_REQUIRED;
                    cmd.Parameters.Add("@reports_window_minutes", SqlDbType.Int).Value = REPORTS_WINDOW_MINUTES;
                    cmd.Parameters.Add("@duplicate_cooldown_minutes", SqlDbType.Int).Value = DUPLICATE_COOLDOWN_MINUTES;
                    cmd.Parameters.Add("@max_temporary_sanctions", SqlDbType.Int).Value = MAX_TEMPORARY_SANCTIONS;
                    cmd.Parameters.Add("@ban_on_sanction_number", SqlDbType.Int).Value = BAN_ON_SANCTION_NUMBER;
                });
        }

        private static void EnsureReportReasons()
        {
            UpsertReason((byte)ReportReasonCode.Harassment, "ReportReason.Harassment");
            UpsertReason((byte)ReportReasonCode.Cheating, "ReportReason.Cheating");
            UpsertReason((byte)ReportReasonCode.Spam, "ReportReason.Spam");
            UpsertReason((byte)ReportReasonCode.InappropriateName, "ReportReason.InappropriateName");
            UpsertReason((byte)ReportReasonCode.Other, "ReportReason.Other");
        }

        private static void UpsertReason(byte code, string key)
        {
            const string sql = @"
IF EXISTS (SELECT 1 FROM dbo.report_reason WHERE reason_code = @reason_code)
BEGIN
    UPDATE dbo.report_reason
    SET reason_key = @reason_key
    WHERE reason_code = @reason_code;
END
ELSE
BEGIN
    INSERT INTO dbo.report_reason (reason_code, reason_key)
    VALUES (@reason_code, @reason_key);
END
";

            ExecuteNonQuery(
                sql,
                cmd =>
                {
                    cmd.Parameters.Add("@reason_code", SqlDbType.TinyInt).Value = code;
                    cmd.Parameters.Add("@reason_key", SqlDbType.NVarChar, 60).Value = key ?? string.Empty;
                });
        }

        private static void EnsureSanctionEscalationPolicy()
        {
            UpsertEscalation(sanctionNumber: 1, durationMinutes: SANCTION_1_DURATION_MINUTES);
            UpsertEscalation(sanctionNumber: 2, durationMinutes: SANCTION_2_DURATION_MINUTES);
            UpsertEscalation(sanctionNumber: 3, durationMinutes: SANCTION_3_DURATION_MINUTES);
        }

        private static void UpsertEscalation(int sanctionNumber, int durationMinutes)
        {
            const string sql = @"
IF EXISTS (SELECT 1 FROM dbo.sanction_escalation_policy WHERE sanction_number = @sanction_number)
BEGIN
    UPDATE dbo.sanction_escalation_policy
    SET duration_minutes = @duration_minutes
    WHERE sanction_number = @sanction_number;
END
ELSE
BEGIN
    INSERT INTO dbo.sanction_escalation_policy (sanction_number, duration_minutes)
    VALUES (@sanction_number, @duration_minutes);
END
";

            ExecuteNonQuery(
                sql,
                cmd =>
                {
                    cmd.Parameters.Add("@sanction_number", SqlDbType.Int).Value = sanctionNumber;
                    cmd.Parameters.Add("@duration_minutes", SqlDbType.Int).Value = durationMinutes;
                });
        }

        private static void ExecuteNonQuery(string sql, Action<SqlCommand> parameterize)
        {
            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var command = new SqlCommand(sql, connection))
            {
                command.CommandType = CommandType.Text;
                parameterize?.Invoke(command);

                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static void EnsureReportConnectionStringIsAvailable()
        {
            string mainConnectionString = DbTestConfig.GetMainConnectionString();

            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var section = (ConnectionStringsSection)config.GetSection("connectionStrings");

            ConnectionStringSettings current =
                section.ConnectionStrings[ReportConstants.Sql.MainConnectionStringName];

            if (current == null)
            {
                section.ConnectionStrings.Add(
                    new ConnectionStringSettings(
                        ReportConstants.Sql.MainConnectionStringName,
                        mainConnectionString,
                        SQL_PROVIDER_NAME));
            }
            else
            {
                current.ConnectionString = mainConnectionString;
                current.ProviderName = SQL_PROVIDER_NAME;
            }

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("connectionStrings");
        }

        private sealed class PlayerReportLookupResult
        {
            internal bool Found { get; set; }
            internal int ReporterAccountId { get; set; }
            internal int ReportedAccountId { get; set; }
            internal byte ReasonCode { get; set; }
            internal Guid? LobbyId { get; set; }
            internal string Comment { get; set; }
        }

        private static PlayerReportLookupResult ReadPlayerReport(long reportId)
        {
            const string sql = @"
SELECT reporter_account_id,
       reported_account_id,
       lobby_id,
       reason_code,
       comment
FROM dbo.player_report
WHERE report_id = @ReportId;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@ReportId", SqlDbType.BigInt).Value = reportId;

                connection.Open();

                using (var reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return new PlayerReportLookupResult { Found = false, Comment = string.Empty };
                    }

                    return new PlayerReportLookupResult
                    {
                        Found = true,
                        ReporterAccountId = reader.GetInt32(0),
                        ReportedAccountId = reader.GetInt32(1),
                        LobbyId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2),
                        ReasonCode = reader.GetByte(3),
                        Comment = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                    };
                }
            }
        }

        private static void UpdatePlayerReportCreatedAtUtc(long reportId, DateTime createdAtUtc)
        {
            const string sql = @"
UPDATE dbo.player_report
SET created_at_utc = @CreatedAtUtc
WHERE report_id = @ReportId;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@CreatedAtUtc", SqlDbType.DateTime2).Value = createdAtUtc;
                cmd.Parameters.Add("@ReportId", SqlDbType.BigInt).Value = reportId;

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static void AssertDbSqlFault(FaultException<ServiceFault> fault)
        {
            Assert.IsNotNull(fault);
            Assert.IsNotNull(fault.Detail);

            Assert.AreEqual(ReportConstants.FaultCode.DbError, fault.Detail.Code);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fault.Detail.Message));

            bool hasPrefix = fault.Detail.Message.StartsWith(SqlMessageKeyPrefix, StringComparison.Ordinal);
            Assert.IsTrue(hasPrefix);
        }

        private sealed class PlayerReportRow
        {
            internal bool Found { get; set; }
            internal long? AppliedSanctionId { get; set; }
        }

        private static PlayerReportRow ReadPlayerReportRow(long reportId)
        {
            const string sql = @"
SELECT applied_sanction_id
FROM dbo.player_report
WHERE report_id = @ReportId;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@ReportId", SqlDbType.BigInt).Value = reportId;

                connection.Open();

                object value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    bool exists = PlayerReportExists(reportId);

                    return new PlayerReportRow
                    {
                        Found = exists,
                        AppliedSanctionId = null
                    };
                }

                return new PlayerReportRow
                {
                    Found = true,
                    AppliedSanctionId = Convert.ToInt64(value)
                };
            }
        }

        private static bool PlayerReportExists(long reportId)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.player_report
WHERE report_id = @ReportId;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@ReportId", SqlDbType.BigInt).Value = reportId;

                connection.Open();

                object value = cmd.ExecuteScalar();
                int count = value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);

                return count > 0;
            }
        }

        private SubmitPlayerReportResponse SendReportsAndReturnLastResponse(int[] reporters, ReportReasonCode reason)
        {
            SubmitPlayerReportResponse last = null;

            for (int i = 0; i < reporters.Length; i++)
            {
                int reporterId = reporters[i];
                string token = TOKEN_PREFIX + reporterId;

                AddToken(token, reporterId);

                last = service.SubmitPlayerReport(BuildRequest(token, reportedAccountId, reason));
                Assert.IsTrue(last.ReportId > 0);
            }

            return last;
        }

        private sealed class AppliedSanctionTiming
        {
            internal bool Found { get; set; }
            internal DateTime CreatedAtUtc { get; set; }
            internal DateTime? EndAtUtc { get; set; }
        }

        private sealed class SanctionSchema
        {
            internal string TableName { get; set; }
            internal string IdColumnName { get; set; }
            internal string CreatedAtUtcColumnName { get; set; }
            internal string EndAtUtcColumnName { get; set; }
        }

        private static SanctionSchema GetSanctionSchema()
        {
            lock (SanctionSchemaSyncRoot)
            {
                if (cachedSanctionSchema != null)
                {
                    return cachedSanctionSchema;
                }

                cachedSanctionSchema = ResolveSanctionSchema();

                Assert.IsNotNull(cachedSanctionSchema);
                Assert.IsFalse(string.IsNullOrWhiteSpace(cachedSanctionSchema.TableName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(cachedSanctionSchema.IdColumnName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(cachedSanctionSchema.CreatedAtUtcColumnName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(cachedSanctionSchema.EndAtUtcColumnName));

                return cachedSanctionSchema;
            }
        }

        private static SanctionSchema ResolveSanctionSchema()
        {
            string[] idCandidates = { "applied_sanction_id", "sanction_id" };

            string[] createdAtCandidates =
            {
                "created_at_utc",
                "createdAtUtc",
                "created_at",
                "createdAt"
            };

            string[] endAtCandidates =
            {
                "end_at_utc",
                "endAtUtc",
                "ends_at_utc",
                "endsAtUtc",
                "end_at",
                "endAt"
            };

            string tableName = FindFirstTableWithColumns(idCandidates[0], endAtCandidates);
            string idColumn = idCandidates[0];

            if (string.IsNullOrWhiteSpace(tableName))
            {
                tableName = FindFirstTableWithColumns(idCandidates[1], endAtCandidates);
                idColumn = idCandidates[1];
            }

            Assert.IsFalse(string.IsNullOrWhiteSpace(tableName), "No se encontró tabla de sanciones aplicadas en dbo.*");

            string createdAtCol = FindPreferredColumn(tableName, createdAtCandidates);
            string endAtCol = FindPreferredColumn(tableName, endAtCandidates);

            Assert.IsFalse(string.IsNullOrWhiteSpace(createdAtCol), "No se encontró columna created_at utc para la sanción.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(endAtCol), "No se encontró columna end_at utc para la sanción.");

            return new SanctionSchema
            {
                TableName = tableName,
                IdColumnName = idColumn,
                CreatedAtUtcColumnName = createdAtCol,
                EndAtUtcColumnName = endAtCol
            };
        }

        private static string FindFirstTableWithColumns(string idColumnName, string[] endAtCandidates)
        {
            if (string.IsNullOrWhiteSpace(idColumnName) || endAtCandidates == null || endAtCandidates.Length == 0)
            {
                return string.Empty;
            }

            // Busca una tabla dbo.* que tenga:
            // - idColumnName (applied_sanction_id o sanction_id)
            // - alguna columna "end_at_*"
            // Esto evita caer por error en dbo.player_report (normalmente no tiene end_at_utc).
            string sql = @"
SELECT TOP (1) t.name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = 'dbo'
AND EXISTS
(
    SELECT 1
    FROM sys.columns c
    WHERE c.object_id = t.object_id
      AND c.name = @IdColumn
)
AND EXISTS
(
    SELECT 1
    FROM sys.columns c
    WHERE c.object_id = t.object_id
      AND c.name IN ({0})
)
ORDER BY t.name;";

            string inList = BuildInList(endAtCandidates.Length);
            sql = string.Format(sql, inList);

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@IdColumn", SqlDbType.NVarChar, 128).Value = idColumnName;

                for (int i = 0; i < endAtCandidates.Length; i++)
                {
                    cmd.Parameters.Add("@End" + i, SqlDbType.NVarChar, 128).Value = endAtCandidates[i];
                }

                connection.Open();

                object value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? string.Empty : Convert.ToString(value);
            }
        }

        private static string BuildInList(int count)
        {
            // @End0,@End1,@End2...
            if (count <= 0)
            {
                return "@End0";
            }

            string result = string.Empty;

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    result = string.Concat(result, ",");
                }

                result = string.Concat(result, "@End", i);
            }

            return result;
        }

        private static string FindPreferredColumn(string tableName, string[] candidates)
        {
            if (string.IsNullOrWhiteSpace(tableName) || candidates == null || candidates.Length == 0)
            {
                return string.Empty;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (!string.IsNullOrWhiteSpace(candidate) && ColumnExists(tableName, candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            const string sql = @"
SELECT COUNT(1)
FROM sys.columns
WHERE object_id = OBJECT_ID(@FullTableName)
  AND name = @ColumnName;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@FullTableName", SqlDbType.NVarChar, 260).Value =
                    string.Concat("dbo.", tableName ?? string.Empty);

                cmd.Parameters.Add("@ColumnName", SqlDbType.NVarChar, 128).Value = columnName ?? string.Empty;

                connection.Open();

                object value = cmd.ExecuteScalar();
                int count = value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);

                return count > 0;
            }
        }

        private static class FaultAssert
        {
            internal static FaultException<ServiceFault> CaptureFault(Action action)
            {
                try
                {
                    action();
                    Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
                    return null;
                }
                catch (FaultException<ServiceFault> ex)
                {
                    return ex;
                }
            }

            internal static void AssertFault(
                FaultException<ServiceFault> fault,
                string expectedCode,
                string expectedMessageKey)
            {
                Assert.IsNotNull(fault);
                Assert.IsNotNull(fault.Detail);

                Assert.AreEqual(expectedCode, fault.Detail.Code);
                Assert.AreEqual(expectedMessageKey, fault.Detail.Message);
            }
        }
    }
}
