using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Workflows;
using ServerTheWeakestRival.Tests.Unit.Fakes;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.Email;
using System.Collections.Generic;
using System.Net.Mail;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using System;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    public sealed class BeginPasswordResetWorkflowTests : AuthTestBase
    {
        private const string EMAIL_DOMAIN = "@test.local";

        private const string DISPLAY_NAME = "Test User";
        private const string PASSWORD = "Password123!";

        private const string WHITESPACE = " ";

        private const int EXPIRES_AT_TOLERANCE_SECONDS = 2;
        private const int CUSTOM_RESEND_COOLDOWN_SECONDS = 5;
        private const string CUSTOM_RESEND_COOLDOWN_SECONDS_STR = "5";

        private const int COOLDOWN_GRACE_SECONDS = 5;

        private const char EMAIL_FILL_CHAR = 'a';
        private const int MAX_EMAIL_LENGTH = AuthServiceConstants.EMAIL_MAX_LENGTH;


        [TestMethod]
        public void Execute_WhenRequestIsNull_ThrowsInvalidRequestEmailRequired()
        {
            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(null));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailIsEmpty_ThrowsInvalidRequestEmailRequired()
        {
            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            var request = new BeginPasswordResetRequest
            {
                Email = WHITESPACE
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_REQUIRED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenEmailNotRegistered_ThrowsEmailNotFound()
        {
            string email = BuildEmail("notfound");

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            var request = new BeginPasswordResetRequest
            {
                Email = email
            };

            ServiceFault fault = FaultAssert.Capture(() => workflow.Execute(request));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_NOT_FOUND, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_NOT_REGISTERED, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenRequestedTooSoon_ThrowsTooSoon()
        {
            string email = BuildEmail("cooldown");
            CreateAccount(email);

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            workflow.Execute(new BeginPasswordResetRequest { Email = email });

            ServiceFault fault = FaultAssert.Capture(() =>
                workflow.Execute(new BeginPasswordResetRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenSuccess_SendsResetEmailAndReturnsResponse()
        {
            string email = BuildEmail("success");
            CreateAccount(email);

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            BeginPasswordResetResponse response = workflow.Execute(new BeginPasswordResetRequest
            {
                Email = "  " + email + "  "
            });

            Assert.AreEqual(email, fakeEmailService.LastResetEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastResetCode));

            Assert.AreEqual(AuthServiceContext.ResendCooldownSeconds, response.ResendAfterSeconds);
            Assert.IsTrue(response.ExpiresAtUtc > DateTime.UtcNow);

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(lookup.Found);
            Assert.IsFalse(lookup.Reset.Used);
        }

        [TestMethod]
        public void Execute_WhenEmailNotRegistered_DoesNotCreateResetRequest()
        {
            string email = BuildEmail("notregistered.nocreate");

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            ServiceFault fault = FaultAssert.Capture(() =>
                workflow.Execute(new BeginPasswordResetRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_EMAIL_NOT_FOUND, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_EMAIL_NOT_REGISTERED, fault.Message);

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            Assert.IsFalse(lookup.Found);
        }

        [TestMethod]
        public void Execute_WhenEmailServiceThrowsSmtpException_ThrowsSmtpFaultAndPersistsReset()
        {
            string email = BuildEmail("smtp.fail");
            CreateAccount(email);

            var smtpException = new SmtpException("Simulated SMTP failure.");
            var throwingEmailService = new ThrowingEmailService(smtpException);
            var throwingDispatcher = new AuthEmailDispatcher(throwingEmailService);

            var workflow = new BeginPasswordResetWorkflow(authRepository, throwingDispatcher);

            ServiceFault fault = FaultAssert.Capture(() =>
                workflow.Execute(new BeginPasswordResetRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_SMTP, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_PASSWORD_RESET_EMAIL_FAILED, fault.Message);

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(lookup.Found);
            Assert.IsFalse(lookup.Reset.Used);
        }

        [TestMethod]
        public void Execute_WhenEmailServiceThrowsNonSmtpException_ThrowsOriginalException()
        {
            string email = BuildEmail("nonsmtp.fail");
            CreateAccount(email);

            var throwingEmailService = new ThrowingEmailService(new InvalidOperationException("Simulated non-SMTP exception."));
            var throwingDispatcher = new AuthEmailDispatcher(throwingEmailService);

            var workflow = new BeginPasswordResetWorkflow(authRepository, throwingDispatcher);

            Assert.ThrowsException<InvalidOperationException>(() =>
                workflow.Execute(new BeginPasswordResetRequest { Email = email }));
        }

        [TestMethod]
        public void Execute_WhenLastResetWasUsedButWithinCooldown_StillThrowsTooSoon()
        {
            string email = BuildEmail("cooldown.used");
            CreateAccount(email);

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            workflow.Execute(new BeginPasswordResetRequest { Email = email });

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(lookup.Found);

            authRepository.MarkResetUsed(lookup.Reset.Id);

            ServiceFault fault = FaultAssert.Capture(() =>
                workflow.Execute(new BeginPasswordResetRequest { Email = email }));

            Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);
        }

        [TestMethod]
        public void Execute_WhenSuccess_ResponseExpiresAtMatchesDbWithinTolerance()
        {
            string email = BuildEmail("expires.match");
            CreateAccount(email);

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            BeginPasswordResetResponse response = workflow.Execute(new BeginPasswordResetRequest
            {
                Email = email
            });

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(lookup.Found);

            Assert.IsTrue(AreUtcClose(response.ExpiresAtUtc, lookup.Reset.ExpiresAtUtc, EXPIRES_AT_TOLERANCE_SECONDS));
        }

        [TestMethod]
        public void Execute_WhenEmailHasSpaces_TrimsAndUsesTrimmedEmail()
        {
            string email = BuildEmail("trim");
            CreateAccount(email);

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            BeginPasswordResetResponse response = workflow.Execute(new BeginPasswordResetRequest
            {
                Email = "  " + email + "  "
            });

            Assert.AreEqual(email, fakeEmailService.LastResetEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastResetCode));
            Assert.AreEqual(AuthServiceContext.ResendCooldownSeconds, response.ResendAfterSeconds);

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(lookup.Found);
        }

        [TestMethod]
        public void Execute_WhenEmailIsUppercase_SucceedsAndUsesUppercaseEmail()
        {
            string email = BuildEmail("upper");
            string upperEmail = email.ToUpperInvariant();

            CreateAccount(upperEmail);

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            BeginPasswordResetResponse response = workflow.Execute(new BeginPasswordResetRequest
            {
                Email = "  " + upperEmail + "  "
            });

            Assert.AreEqual(upperEmail, fakeEmailService.LastResetEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastResetCode));
            Assert.AreEqual(AuthServiceContext.ResendCooldownSeconds, response.ResendAfterSeconds);

            ResetLookupResult lookup = authRepository.ReadLatestReset(upperEmail);
            Assert.IsTrue(lookup.Found);
        }

        [TestMethod]
        public void Execute_WhenCooldownElapsed_AllowsSecondRequest_AndInvalidatesPreviousPendingReset()
        {
            string email = BuildEmail("cooldown.elapsed");
            CreateAccount(email);

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            workflow.Execute(new BeginPasswordResetRequest { Email = email });

            ResetLookupResult firstLookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(firstLookup.Found);

            int firstId = firstLookup.Reset.Id;

            SetResetCreatedAtUtc(firstId, AuthServiceContext.ResendCooldownSeconds + COOLDOWN_GRACE_SECONDS);

            workflow.Execute(new BeginPasswordResetRequest { Email = email });

            ResetLookupResult secondLookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(secondLookup.Found);
            Assert.AreNotEqual(firstId, secondLookup.Reset.Id);

            bool firstUsed = ReadResetUsedFlag(firstId);
            Assert.IsTrue(firstUsed);
        }

        [TestMethod]
        public void Execute_WhenAppSettingOverridesResendCooldown_UsesConfiguredValue()
        {
            string email = BuildEmail("config.cooldown");
            CreateAccount(email);

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            var appSettings = new Dictionary<string, string>
    {
        { AuthServiceConstants.APPSETTING_EMAIL_RESEND_COOLDOWN_SECONDS, CUSTOM_RESEND_COOLDOWN_SECONDS_STR }
    };

            using (new TestConfigurationScope(appSettings))
            {
                BeginPasswordResetResponse response = workflow.Execute(new BeginPasswordResetRequest { Email = email });

                Assert.AreEqual(CUSTOM_RESEND_COOLDOWN_SECONDS, response.ResendAfterSeconds);

                ServiceFault fault = FaultAssert.Capture(() =>
                    workflow.Execute(new BeginPasswordResetRequest { Email = email }));

                Assert.AreEqual(AuthServiceConstants.ERROR_TOO_SOON, fault.Code);
                Assert.AreEqual(AuthServiceConstants.MESSAGE_TOO_SOON, fault.Message);
            }
        }

        [TestMethod]
        public void Execute_WhenEmailIsAtMaxAllowedLength_Succeeds()
        {
            string email = BuildEmailWithExactLength(MAX_EMAIL_LENGTH);

            CreateAccount(email);

            var workflow = new BeginPasswordResetWorkflow(authRepository, emailDispatcher);

            BeginPasswordResetResponse response = workflow.Execute(new BeginPasswordResetRequest { Email = email });

            Assert.AreEqual(email, fakeEmailService.LastResetEmail);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fakeEmailService.LastResetCode));
            Assert.IsTrue(response.ExpiresAtUtc > DateTime.UtcNow);

            ResetLookupResult lookup = authRepository.ReadLatestReset(email);
            Assert.IsTrue(lookup.Found);
        }


        private static bool AreUtcClose(DateTime leftUtc, DateTime rightUtc, int toleranceSeconds)
        {
            double seconds = Math.Abs((leftUtc - rightUtc).TotalSeconds);
            return seconds <= toleranceSeconds;
        }

        private static string BuildEmail(string prefix)
        {
            return string.Concat(
                "tc.beginpasswordreset.",
                prefix,
                ".",
                Guid.NewGuid().ToString("N"),
                EMAIL_DOMAIN);
        }

        private void CreateAccount(string email)
        {
            string passwordHash = passwordService.Hash(PASSWORD);

            var data = new AccountRegistrationData(
                email,
                passwordHash,
                DISPLAY_NAME,
                new ProfileImagePayload(null, null));

            authRepository.CreateAccountAndUser(data);
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

        private static void SetResetCreatedAtUtc(int resetId, int secondsAgo)
        {
            const string sql = @"
UPDATE dbo.PasswordResetRequests
SET CreatedAtUtc = DATEADD(SECOND, -@SecondsAgo, SYSUTCDATETIME())
WHERE Id = @Id;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;
                cmd.Parameters.Add("@SecondsAgo", SqlDbType.Int).Value = secondsAgo;

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static bool ReadResetUsedFlag(int resetId)
        {
            const string sql = @"SELECT Used FROM dbo.PasswordResetRequests WHERE Id = @Id;";

            using (var connection = new SqlConnection(DbTestConfig.GetMainConnectionString()))
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;

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
            private const string APP_CONFIG_FILE_KEY = "APP_CONFIG_FILE";
            private const string NODE_CONFIGURATION = "configuration";
            private const string NODE_APP_SETTINGS = "appSettings";
            private const string NODE_CONNECTION_STRINGS = "connectionStrings";

            private const string ELEMENT_ADD = "add";
            private const string ATTR_KEY = "key";
            private const string ATTR_VALUE = "value";

            private readonly string originalConfigPath;
            private readonly string tempConfigPath;

            public TestConfigurationScope(Dictionary<string, string> appSettings)
            {
                originalConfigPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                tempConfigPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "BeginPasswordResetWorkflowTests.temp.config");

                XDocument doc = XDocument.Load(originalConfigPath);

                if (doc.Root == null || !string.Equals(doc.Root.Name.LocalName, NODE_CONFIGURATION, StringComparison.OrdinalIgnoreCase))
                {
                    doc = new XDocument(new XElement(NODE_CONFIGURATION));
                }

                XElement appSettingsNode = doc.Root.Element(NODE_APP_SETTINGS);
                if (appSettingsNode == null)
                {
                    appSettingsNode = new XElement(NODE_APP_SETTINGS);
                    doc.Root.Add(appSettingsNode);
                }

                if (appSettings != null)
                {
                    foreach (var kv in appSettings)
                    {
                        SetAppSetting(appSettingsNode, kv.Key, kv.Value);
                    }
                }

                doc.Save(tempConfigPath);

                AppDomain.CurrentDomain.SetData(APP_CONFIG_FILE_KEY, tempConfigPath);

                ResetConfigurationManager();
                ConfigurationManager.RefreshSection(NODE_APP_SETTINGS);
                ConfigurationManager.RefreshSection(NODE_CONNECTION_STRINGS);
            }

            public void Dispose()
            {
                AppDomain.CurrentDomain.SetData(APP_CONFIG_FILE_KEY, originalConfigPath);

                ResetConfigurationManager();
                ConfigurationManager.RefreshSection(NODE_APP_SETTINGS);
                ConfigurationManager.RefreshSection(NODE_CONNECTION_STRINGS);

                TryDeleteTempConfig();
            }

            private static void SetAppSetting(XElement appSettingsNode, string key, string value)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return;
                }

                XElement existing = null;

                foreach (var el in appSettingsNode.Elements(ELEMENT_ADD))
                {
                    string existingKey = (string)el.Attribute(ATTR_KEY);
                    if (string.Equals(existingKey, key, StringComparison.Ordinal))
                    {
                        existing = el;
                        break;
                    }
                }

                if (existing == null)
                {
                    existing = new XElement(ELEMENT_ADD);
                    existing.SetAttributeValue(ATTR_KEY, key);
                    appSettingsNode.Add(existing);
                }

                existing.SetAttributeValue(ATTR_VALUE, value ?? string.Empty);
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

            private void TryDeleteTempConfig()
            {
                try
                {
                    if (File.Exists(tempConfigPath))
                    {
                        File.Delete(tempConfigPath);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(
                        string.Format(
                            "BeginPasswordResetWorkflowTests.TestConfigurationScope.TryDeleteTempConfig: Path='{0}'. ExceptionType='{1}'. Message='{2}'.",
                            tempConfigPath ?? string.Empty,
                            ex.GetType().FullName ?? string.Empty,
                            ex.Message ?? string.Empty));
                }
            }
        }

    }
}
