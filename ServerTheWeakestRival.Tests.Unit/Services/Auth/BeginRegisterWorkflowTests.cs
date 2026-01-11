using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Fakes;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Email;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using System.Net.Mail;
using ServicesTheWeakestRival.Server.Services.Auth;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class BeginRegisterWorkflowTests : AuthTestBase
    {
        private const string DISPLAY_NAME = "Test User";
        private const string PASSWORD = "Password123!";
        private const string EMAIL_DOMAIN = "@test.local";

        private const string WHITESPACE = " ";

        private const int EXPIRES_AT_TOLERANCE_SECONDS = 2;

        private const int CUSTOM_RESEND_COOLDOWN_SECONDS = 5;
        private const string CUSTOM_RESEND_COOLDOWN_SECONDS_STR = "5";

        private const int COOLDOWN_GRACE_SECONDS = 5;

        private const char EMAIL_FILL_CHAR = 'a';
        private const int MAX_EMAIL_LENGTH = AuthServiceConstants.EMAIL_MAX_LENGTH;

        [TestMethod]
        public void Execute_WhenRequestIsNull_ThrowsInvalidRequest()
        {
            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailIsWhitespace_ThrowsInvalidRequest()
        {
            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            ServiceFault fault = FaultAssert.Capture(() =>
                workflow.Execute(new BeginRegisterRequest { Email = WHITESPACE }));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailAlreadyRegistered_ThrowsEmailTaken()
        {
            string email = BuildEmail("taken");
            CreateAccount(email);

            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            ServiceFault fault = FaultAssert.Capture(() =>
                workflow.Execute(new BeginRegisterRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_TAKEN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_TAKEN, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailAlreadyRegistered_DoesNotCreateVerification()
        {
            string email = BuildEmail("taken.nocreate");
            CreateAccount(email);

            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            ServiceFault fault = FaultAssert.Capture(() =>
                workflow.Execute(new BeginRegisterRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_TAKEN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_TAKEN, fault.Message);

            VerificationLookupResult lookup = authRepository.ReadLatestVerification(email);
            Assert.IsFalse(lookup.Found);
        }

        [TestMethod]
        public void Execute_WhenRequestedTooSoon_ThrowsTooSoon()
        {
            string email = BuildEmail("cooldown");
            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            workflow.Execute(new BeginRegisterRequest { Email = email });

            ServiceFault fault = FaultAssert.Capture(() =>
                workflow.Execute(new BeginRegisterRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailHasSpaces_TrimsAndUsesTrimmedEmail()
        {
            string email = BuildEmail("trim");
            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            BeginRegisterResponse response = workflow.Execute(new BeginRegisterRequest
            {
                Email = "  " + email + "  "
            });

            Assert.AreEqual(email, fakeEmailService.LastVerificationEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastVerificationCode));
            Assert.AreEqual(AuthServiceContext.ResendCooldownSeconds, response.ResendAfterSeconds);

            VerificationLookupResult lookup = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(lookup.Found);
        }

        [TestMethod]
        public void Execute_WhenSuccess_SendsVerificationEmailAndReturnsResponse()
        {
            string email = BuildEmail("ok");
            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            BeginRegisterResponse response = workflow.Execute(new BeginRegisterRequest { Email = email });

            Assert.AreEqual(email, fakeEmailService.LastVerificationEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastVerificationCode));
            Assert.AreEqual(AuthServiceContext.ResendCooldownSeconds, response.ResendAfterSeconds);
            Assert.IsTrue(response.ExpiresAtUtc > DateTime.UtcNow);
        }

        [TestMethod]
        public void Execute_WhenSuccess_ResponseExpiresAtMatchesDbWithinTolerance()
        {
            string email = BuildEmail("expires.match");
            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            BeginRegisterResponse response = workflow.Execute(new BeginRegisterRequest { Email = email });

            VerificationLookupResult lookup = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(lookup.Found);

            Assert.IsTrue(AreUtcClose(response.ExpiresAtUtc, lookup.Verification.ExpiresAtUtc, EXPIRES_AT_TOLERANCE_SECONDS));
        }

        [TestMethod]
        public void Execute_WhenEmailServiceThrowsSmtpException_ThrowsSmtpFaultAndPersistsVerification()
        {
            string email = BuildEmail("smtp.fail");

            var smtpException = new SmtpException("Simulated SMTP failure.");
            var throwingEmailService = new ThrowingEmailService(smtpException);
            var throwingDispatcher = new AuthEmailDispatcher(throwingEmailService);

            var workflow = new BeginRegisterWorkflow(authRepository, throwingDispatcher);

            ServiceFault fault = FaultAssert.Capture(() =>
                workflow.Execute(new BeginRegisterRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_SMTP, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_VERIFICATION_EMAIL_FAILED, fault.Message);

            VerificationLookupResult lookup = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(lookup.Found);
            Assert.IsFalse(lookup.Verification.Used);
        }

        [TestMethod]
        public void Execute_WhenEmailServiceThrowsNonSmtpException_ThrowsOriginalException()
        {
            string email = BuildEmail("nonsmtp.fail");

            var throwingEmailService = new ThrowingEmailService(
                new InvalidOperationException("Simulated non-SMTP exception."));
            var throwingDispatcher = new AuthEmailDispatcher(throwingEmailService);

            var workflow = new BeginRegisterWorkflow(authRepository, throwingDispatcher);

            Assert.ThrowsException<InvalidOperationException>(() =>
                workflow.Execute(new BeginRegisterRequest { Email = email }));
        }

        [TestMethod]
        public void Execute_WhenCooldownElapsed_AllowsSecondRequest_AndInvalidatesPreviousPendingVerification()
        {
            string email = BuildEmail("cooldown.elapsed");

            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            workflow.Execute(new BeginRegisterRequest { Email = email });

            VerificationLookupResult firstLookup = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(firstLookup.Found);

            int firstId = firstLookup.Verification.Id;

            SetVerificationCreatedAtUtc(firstId, AuthServiceContext.ResendCooldownSeconds + COOLDOWN_GRACE_SECONDS);

            workflow.Execute(new BeginRegisterRequest { Email = email });

            VerificationLookupResult secondLookup = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(secondLookup.Found);
            Assert.AreNotEqual(firstId, secondLookup.Verification.Id);

            bool firstUsed = ReadVerificationUsedFlag(firstId);
            Assert.IsTrue(firstUsed);
        }

        [TestMethod]
        public void Execute_WhenAppSettingOverridesResendCooldown_UsesConfiguredValue()
        {
            string email = BuildEmail("config.cooldown");
            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            var appSettings = new Dictionary<string, string>
            {
                { AuthServiceConstants.APPSETTING_EMAIL_RESEND_COOLDOWN_SECONDS, CUSTOM_RESEND_COOLDOWN_SECONDS_STR }
            };

            using (new TestConfigurationScope(appSettings))
            {
                BeginRegisterResponse response = workflow.Execute(new BeginRegisterRequest { Email = email });

                Assert.AreEqual(CUSTOM_RESEND_COOLDOWN_SECONDS, response.ResendAfterSeconds);

                ServiceFault fault = FaultAssert.Capture(() =>
                    workflow.Execute(new BeginRegisterRequest { Email = email }));

                Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
                Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);
            }
        }

        [TestMethod]
        public void Execute_WhenEmailIsAtMaxAllowedLength_Succeeds()
        {
            string email = BuildEmailWithExactLength(MAX_EMAIL_LENGTH);

            var workflow = new BeginRegisterWorkflow(authRepository, emailDispatcher);

            BeginRegisterResponse response = workflow.Execute(new BeginRegisterRequest { Email = email });

            Assert.AreEqual(email, fakeEmailService.LastVerificationEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastVerificationCode));
            Assert.IsTrue(response.ExpiresAtUtc > DateTime.UtcNow);

            VerificationLookupResult lookup = authRepository.ReadLatestVerification(email);
            Assert.IsTrue(lookup.Found);
        }

        private static string BuildEmail(string prefix)
        {
            return string.Concat(
                "tc.beginregister.",
                prefix,
                ".",
                Guid.NewGuid().ToString("N"),
                EMAIL_DOMAIN);
        }

        private void CreateAccount(string email)
        {
            string passwordHash = PasswordService.Hash(PASSWORD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(null, null));

            authRepository.CreateAccountAndUser(data);
        }

        private static bool AreUtcClose(DateTime leftUtc, DateTime rightUtc, int toleranceSeconds)
        {
            double seconds = Math.Abs((leftUtc - rightUtc).TotalSeconds);
            return seconds <= toleranceSeconds;
        }

        private static string BuildEmailWithExactLength(int totalLength)
        {
            int localLength = totalLength - EMAIL_DOMAIN.Length;
            if (localLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalLength));
            }

            return new string(EMAIL_FILL_CHAR, localLength) + EMAIL_DOMAIN;
        }

        private static void SetVerificationCreatedAtUtc(int verificationId, int secondsAgo)
        {
            const string sql = @"
UPDATE dbo.EmailVerifications
SET created_at_utc = DATEADD(SECOND, -@SecondsAgo, SYSUTCDATETIME())
WHERE verification_id = @Id;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                cmd.Parameters.Add("@SecondsAgo", SqlDbType.Int).Value = secondsAgo;

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static bool ReadVerificationUsedFlag(int verificationId)
        {
            const string sql = @"SELECT used FROM dbo.EmailVerifications WHERE verification_id = @Id;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;

                connection.Open();
                object value = cmd.ExecuteScalar();

                if (value == null || value == DBNull.Value)
                {
                    return false;
                }

                return Convert.ToBoolean(value);
            }
        }

        private sealed class TestConfigurationScope : IDisposable
        {
            private const string AppConfigFileKey = "APP_CONFIG_FILE";

            private const string NodeConfiguration = "configuration";
            private const string NodeAppSettings = "appSettings";
            private const string NodeAdd = "add";

            private const string AttrKey = "key";
            private const string AttrValue = "value";

            private const string RuntimeConfigFileName = "BeginRegisterWorkflowTests.Runtime.config";

            private readonly string previousConfigPath;
            private readonly string generatedConfigPath;

            public TestConfigurationScope(IDictionary<string, string> overrides)
            {
                if (overrides == null)
                {
                    throw new ArgumentNullException(nameof(overrides));
                }

                previousConfigPath = GetCurrentConfigPathOrFallback();
                generatedConfigPath = BuildRuntimeConfigPath();

                XDocument merged = BuildMergedConfig(previousConfigPath, overrides);
                merged.Save(generatedConfigPath);

                AppDomain.CurrentDomain.SetData(AppConfigFileKey, generatedConfigPath);

                ResetConfigurationManager();
                ConfigurationManager.RefreshSection(NodeAppSettings);
                ConfigurationManager.RefreshSection("connectionStrings");
            }

            public void Dispose()
            {
                AppDomain.CurrentDomain.SetData(AppConfigFileKey, previousConfigPath);

                ResetConfigurationManager();
                ConfigurationManager.RefreshSection(NodeAppSettings);
                ConfigurationManager.RefreshSection("connectionStrings");

                TryDeleteGeneratedFile();
            }

            private static string GetCurrentConfigPathOrFallback()
            {
                object current = AppDomain.CurrentDomain.GetData(AppConfigFileKey);

                if (current is string currentPath && !string.IsNullOrWhiteSpace(currentPath) && File.Exists(currentPath))
                {
                    return currentPath;
                }

                string fallback = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                return fallback ?? string.Empty;
            }

            private static string BuildRuntimeConfigPath()
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(baseDir, RuntimeConfigFileName);
            }

            private static XDocument BuildMergedConfig(string baseConfigPath, IDictionary<string, string> overrides)
            {
                XDocument doc = LoadOrCreateConfig(baseConfigPath);

                XElement root = doc.Root ?? new XElement(NodeConfiguration);
                if (doc.Root == null)
                {
                    doc.Add(root);
                }

                XElement appSettings = root.Element(NodeAppSettings);
                if (appSettings == null)
                {
                    appSettings = new XElement(NodeAppSettings);
                    root.AddFirst(appSettings);
                }

                foreach (var kv in overrides)
                {
                    UpsertAppSetting(appSettings, kv.Key, kv.Value);
                }

                return doc;
            }

            private static XDocument LoadOrCreateConfig(string path)
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return XDocument.Load(path);
                }

                return new XDocument(new XElement(NodeConfiguration));
            }

            private static void UpsertAppSetting(XElement appSettings, string key, string value)
            {
                if (appSettings == null)
                {
                    throw new ArgumentNullException(nameof(appSettings));
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    return;
                }

                string safeValue = value ?? string.Empty;

                XElement existing = null;
                foreach (var el in appSettings.Elements(NodeAdd))
                {
                    string k = (string)el.Attribute(AttrKey);
                    if (string.Equals(k, key, StringComparison.Ordinal))
                    {
                        existing = el;
                        break;
                    }
                }

                if (existing == null)
                {
                    appSettings.Add(new XElement(
                        NodeAdd,
                        new XAttribute(AttrKey, key),
                        new XAttribute(AttrValue, safeValue)));
                    return;
                }

                existing.SetAttributeValue(AttrValue, safeValue);
            }

            private static void ResetConfigurationManager()
            {
                Type cmType = typeof(ConfigurationManager);

                FieldInfo initStateField = cmType.GetField("s_initState", BindingFlags.NonPublic | BindingFlags.Static);
                FieldInfo configSystemField = cmType.GetField("s_configSystem", BindingFlags.NonPublic | BindingFlags.Static);
                FieldInfo configPathsField = cmType.GetField("s_configPaths", BindingFlags.NonPublic | BindingFlags.Static);

                if (initStateField != null)
                {
                    initStateField.SetValue(null, 0);
                }

                if (configSystemField != null)
                {
                    configSystemField.SetValue(null, null);
                }

                if (configPathsField != null)
                {
                    configPathsField.SetValue(null, null);
                }
            }

            private void TryDeleteGeneratedFile()
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(generatedConfigPath) && File.Exists(generatedConfigPath))
                    {
                        File.Delete(generatedConfigPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }
    }
}
