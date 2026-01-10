using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.AuthRefactor;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Diagnostics;

namespace ServerTheWeakestRival.Tests.Unit.Services.Auth
{
    [TestClass]
    [DoNotParallelize]
    public sealed class AuthServiceContextTests
    {
        private const int USER_ID = 777;
        private const int USER_ID_NEGATIVE = -1;

        private const int TOKEN_TEST_USER_ID = 1001;
        private const int EXPIRED_MINUTES = 1;

        private const int GUID_N_LENGTH = 32;

        private const int CONFIGURED_CODE_TTL_MINUTES = 15;
        private const int CONFIGURED_RESEND_COOLDOWN_SECONDS = 120;

        private const string PROVIDER_SQLCLIENT = "System.Data.SqlClient";
        private const string DUMMY_CONNECTION_STRING =
            "Server=(localdb)\\MSSQLLocalDB;Database=DummyDb;Trusted_Connection=True;";

        private const string APP_CONFIG_FILE_KEY = "APP_CONFIG_FILE";

        private const string TOKEN_STORE_TYPE_NAME = "ServicesTheWeakestRival.Server.Services.TokenStore";
        private const string TOKEN_STORE_FIELD_CACHE = "Cache";
        private const string TOKEN_STORE_FIELD_ACTIVE_BY_USER = "ActiveTokenByUserId";
        private const string CTX_DELETE_TEMP_CONFIG =
    "AuthServiceContextTests.TestConfigurationScope.TryDeleteTempConfig";

        private const string APPSETTING_CODE_TTL_WITH_WHITESPACE = " 15 ";
        private const string APPSETTING_RESEND_COOLDOWN_WITH_WHITESPACE = " 120 ";

        private const int EXPIRES_AT_UTC_OFFSET_SECONDS = 0;

        private const string CONNECTION_NAME_WHITESPACE = "   ";

        private static readonly Regex TokenHexRegex =
            new Regex("^[0-9a-f]{32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [TestInitialize]
        public void SetUp()
        {
            TokenStoreTestCleaner.ClearAllTokens();
        }

        [TestCleanup]
        public void TearDown()
        {
            TokenStoreTestCleaner.ClearAllTokens();
        }

        [TestMethod]
        public void IssueToken_WhenUserIdInvalidZero_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() => AuthServiceContext.IssueToken(0));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_USER_ID, fault.Message);
        }

        [TestMethod]
        public void IssueToken_WhenUserIdInvalidNegative_ThrowsInvalidRequest()
        {
            ServiceFault fault = FaultAssert.Capture(() => AuthServiceContext.IssueToken(USER_ID_NEGATIVE));

            Assert.AreEqual(AuthServiceConstants.ERROR_INVALID_REQUEST, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_INVALID_USER_ID, fault.Message);
        }

        [TestMethod]
        public void IssueToken_WhenUserIdValid_ReturnsTokenWithExpectedFields()
        {
            AuthToken token = AuthServiceContext.IssueToken(USER_ID);

            Assert.IsNotNull(token);
            Assert.AreEqual(USER_ID, token.UserId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(token.Token));
            Assert.IsTrue(token.ExpiresAtUtc > DateTime.UtcNow);
        }

        [TestMethod]
        public void IssueToken_WhenUserIdValid_ReturnsTokenInGuidNFormat()
        {
            AuthToken token = AuthServiceContext.IssueToken(USER_ID);

            Assert.IsNotNull(token);
            Assert.AreEqual(GUID_N_LENGTH, token.Token.Length);
            Assert.IsTrue(TokenHexRegex.IsMatch(token.Token));
        }

        [TestMethod]
        public void IssueToken_WhenUserAlreadyHasActiveToken_ThrowsAlreadyLoggedIn()
        {
            AuthServiceContext.IssueToken(USER_ID);

            ServiceFault fault = FaultAssert.Capture(() => AuthServiceContext.IssueToken(USER_ID));

            Assert.AreEqual(AuthServiceConstants.ERROR_ALREADY_LOGGED_IN, fault.Code);
            Assert.AreEqual(AuthServiceConstants.MESSAGE_ALREADY_LOGGED_IN, fault.Message);
        }

        [TestMethod]
        public void IssueToken_WhenTokenRemoved_AllowsIssuingAgain()
        {
            AuthToken token = AuthServiceContext.IssueToken(USER_ID);

            bool removedOk = AuthServiceContext.TryRemoveToken(token.Token, out AuthToken removed);
            Assert.IsTrue(removedOk);
            Assert.IsNotNull(removed);

            AuthToken token2 = AuthServiceContext.IssueToken(USER_ID);
            Assert.IsNotNull(token2);
            Assert.AreEqual(USER_ID, token2.UserId);
            Assert.AreNotEqual(token.Token, token2.Token);
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenIsValid_ReturnsTrueAndUserId()
        {
            AuthToken token = AuthServiceContext.IssueToken(USER_ID);

            bool ok = AuthServiceContext.TryGetUserId(token.Token, out int userId);

            Assert.IsTrue(ok);
            Assert.AreEqual(USER_ID, userId);
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenInvalid_ReturnsFalse()
        {
            string tokenValue = Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);

            bool ok = AuthServiceContext.TryGetUserId(tokenValue, out int userId);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, userId);
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenIsNull_ReturnsFalse()
        {
            bool ok = AuthServiceContext.TryGetUserId(null, out int userId);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, userId);
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenIsEmpty_ReturnsFalse()
        {
            bool ok = AuthServiceContext.TryGetUserId(string.Empty, out int userId);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, userId);
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenIsWhitespace_ReturnsFalse()
        {
            bool ok = AuthServiceContext.TryGetUserId("   ", out int userId);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, userId);
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenIsExpired_ReturnsFalseAndRemovesToken()
        {
            string tokenValue = "expired." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);

            var expiredToken = new AuthToken
            {
                UserId = TOKEN_TEST_USER_ID,
                Token = tokenValue,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-EXPIRED_MINUTES)
            };

            SeedTokenStore(expiredToken, activeTokenValue: tokenValue);

            bool ok = AuthServiceContext.TryGetUserId(tokenValue, out int userId);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, userId);
            Assert.IsFalse(CacheContainsToken(tokenValue));
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenIsNotActiveForUser_ReturnsFalseAndRemovesToken()
        {
            string staleTokenValue = "stale." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);
            string activeTokenValue = "active." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);

            var staleToken = new AuthToken
            {
                UserId = TOKEN_TEST_USER_ID,
                Token = staleTokenValue,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(AuthServiceConstants.TOKEN_TTL_HOURS)
            };

            SeedTokenStore(staleToken, activeTokenValue: activeTokenValue);

            bool ok = AuthServiceContext.TryGetUserId(staleTokenValue, out int userId);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, userId);
            Assert.IsFalse(CacheContainsToken(staleTokenValue));
        }

        [TestMethod]
        public void TryRemoveToken_WhenTokenExists_RemovesAndReturnsTrue()
        {
            AuthToken token = AuthServiceContext.IssueToken(USER_ID);

            bool removedOk = AuthServiceContext.TryRemoveToken(token.Token, out AuthToken removed);

            Assert.IsTrue(removedOk);
            Assert.IsNotNull(removed);
            Assert.AreEqual(token.Token, removed.Token);

            bool okAfter = AuthServiceContext.TryGetUserId(token.Token, out int userIdAfter);
            Assert.IsFalse(okAfter);
            Assert.AreEqual(0, userIdAfter);
        }

        [TestMethod]
        public void TryRemoveToken_WhenTokenDoesNotExist_ReturnsFalse()
        {
            string tokenValue = Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);

            bool removedOk = AuthServiceContext.TryRemoveToken(tokenValue, out AuthToken removed);

            Assert.IsFalse(removedOk);
            Assert.IsNull(removed);
        }

        [TestMethod]
        public void TryRemoveToken_WhenTokenIsNull_ReturnsFalse()
        {
            bool removedOk = AuthServiceContext.TryRemoveToken(null, out AuthToken removed);

            Assert.IsFalse(removedOk);
            Assert.IsNull(removed);
        }

        [TestMethod]
        public void TryRemoveToken_WhenTokenIsWhitespace_ReturnsFalse()
        {
            bool removedOk = AuthServiceContext.TryRemoveToken("   ", out AuthToken removed);

            Assert.IsFalse(removedOk);
            Assert.IsNull(removed);
        }

        [TestMethod]
        public void ResolveConnectionString_WhenMissing_ThrowsConfigError()
        {
            string missingName = "Missing." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);

            using (new TestConfigurationScope(new Dictionary<string, string>(), new Dictionary<string, string>()))
            {
                ServiceFault fault = FaultAssert.Capture(() => AuthServiceContext.ResolveConnectionString(missingName));

                Assert.AreEqual(AuthServiceConstants.ERROR_CONFIG, fault.Code);
                Assert.AreEqual(AuthServiceConstants.MESSAGE_CONFIG_ERROR, fault.Message);
            }
        }

        [TestMethod]
        public void ResolveConnectionString_WhenEmpty_ThrowsConfigError()
        {
            var cs = new Dictionary<string, string>
            {
                { AuthServiceConstants.MAIN_CONNECTION_STRING_NAME, "   " }
            };

            using (new TestConfigurationScope(new Dictionary<string, string>(), cs))
            {
                ServiceFault fault = FaultAssert.Capture(() =>
                    AuthServiceContext.ResolveConnectionString(AuthServiceConstants.MAIN_CONNECTION_STRING_NAME));

                Assert.AreEqual(AuthServiceConstants.ERROR_CONFIG, fault.Code);
                Assert.AreEqual(AuthServiceConstants.MESSAGE_CONFIG_ERROR, fault.Message);
            }
        }

        [TestMethod]
        public void ResolveConnectionString_WhenPresent_ReturnsConnectionString()
        {
            var cs = new Dictionary<string, string>
            {
                { AuthServiceConstants.MAIN_CONNECTION_STRING_NAME, DUMMY_CONNECTION_STRING }
            };

            using (new TestConfigurationScope(new Dictionary<string, string>(), cs))
            {
                string resolved = AuthServiceContext.ResolveConnectionString(AuthServiceConstants.MAIN_CONNECTION_STRING_NAME);

                Assert.AreEqual(DUMMY_CONNECTION_STRING, resolved);
            }
        }

        [TestMethod]
        public void CodeTtlMinutes_WhenAppSettingMissing_ReturnsDefault()
        {
            using (new TestConfigurationScope(new Dictionary<string, string>(), new Dictionary<string, string>()))
            {
                Assert.AreEqual(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES, AuthServiceContext.CodeTtlMinutes);
            }
        }

        [TestMethod]
        public void CodeTtlMinutes_WhenAppSettingInvalid_ReturnsDefault()
        {
            var app = new Dictionary<string, string>
            {
                { AuthServiceConstants.APPSETTING_EMAIL_CODE_TTL_MINUTES, "not-a-number" }
            };

            using (new TestConfigurationScope(app, new Dictionary<string, string>()))
            {
                Assert.AreEqual(AuthServiceConstants.DEFAULT_CODE_TTL_MINUTES, AuthServiceContext.CodeTtlMinutes);
            }
        }

        [TestMethod]
        public void CodeTtlMinutes_WhenAppSettingValid_ReturnsConfiguredValue()
        {
            var app = new Dictionary<string, string>
            {
                { AuthServiceConstants.APPSETTING_EMAIL_CODE_TTL_MINUTES, CONFIGURED_CODE_TTL_MINUTES.ToString() }
            };

            using (new TestConfigurationScope(app, new Dictionary<string, string>()))
            {
                Assert.AreEqual(CONFIGURED_CODE_TTL_MINUTES, AuthServiceContext.CodeTtlMinutes);
            }
        }

        [TestMethod]
        public void ResendCooldownSeconds_WhenAppSettingMissing_ReturnsDefault()
        {
            using (new TestConfigurationScope(new Dictionary<string, string>(), new Dictionary<string, string>()))
            {
                Assert.AreEqual(AuthServiceConstants.DEFAULT_RESEND_COOLDOWN_SECONDS, AuthServiceContext.ResendCooldownSeconds);
            }
        }

        [TestMethod]
        public void ResendCooldownSeconds_WhenAppSettingInvalid_ReturnsDefault()
        {
            var app = new Dictionary<string, string>
            {
                { AuthServiceConstants.APPSETTING_EMAIL_RESEND_COOLDOWN_SECONDS, "invalid" }
            };

            using (new TestConfigurationScope(app, new Dictionary<string, string>()))
            {
                Assert.AreEqual(AuthServiceConstants.DEFAULT_RESEND_COOLDOWN_SECONDS, AuthServiceContext.ResendCooldownSeconds);
            }
        }

        [TestMethod]
        public void ResendCooldownSeconds_WhenAppSettingValid_ReturnsConfiguredValue()
        {
            var app = new Dictionary<string, string>
            {
                { AuthServiceConstants.APPSETTING_EMAIL_RESEND_COOLDOWN_SECONDS, CONFIGURED_RESEND_COOLDOWN_SECONDS.ToString() }
            };

            using (new TestConfigurationScope(app, new Dictionary<string, string>()))
            {
                Assert.AreEqual(CONFIGURED_RESEND_COOLDOWN_SECONDS, AuthServiceContext.ResendCooldownSeconds);
            }
        }

        [TestMethod]
        public void ThrowFault_WhenInvoked_ReturnsFaultWithExpectedDetail()
        {
            const string faultCode = "TEST_CODE";
            const string faultMessage = "Test message.";

            ServiceFault fault = FaultAssert.Capture(() =>
            {
                throw AuthServiceContext.ThrowFault(faultCode, faultMessage);
            });

            Assert.AreEqual(faultCode, fault.Code);
            Assert.AreEqual(faultMessage, fault.Message);
        }

        [TestMethod]
        public void ThrowTechnicalFault_WhenInvoked_ReturnsFaultWithExpectedDetail()
        {
            const string technicalCode = "TECH_CODE";
            const string userMessage = "User safe message.";
            const string context = "AuthServiceContextTests.ThrowTechnicalFault";

            var ex = new InvalidOperationException("boom");

            ServiceFault fault = FaultAssert.Capture(() =>
            {
                throw AuthServiceContext.ThrowTechnicalFault(technicalCode, userMessage, context, ex);
            });

            Assert.AreEqual(technicalCode, fault.Code);
            Assert.AreEqual(userMessage, fault.Message);
        }

        [TestMethod]
        public void ThrowTechnicalFault_WhenContextNull_ReturnsFaultWithExpectedDetail()
        {
            const string technicalCode = "TECH_CODE_NULL_CTX";
            const string userMessage = "User safe message.";
            string context = null;

            var ex = new InvalidOperationException("boom");

            ServiceFault fault = FaultAssert.Capture(() =>
            {
                throw AuthServiceContext.ThrowTechnicalFault(technicalCode, userMessage, context, ex);
            });

            Assert.AreEqual(technicalCode, fault.Code);
            Assert.AreEqual(userMessage, fault.Message);
        }

        [TestMethod]
        public void ResolveConnectionString_WhenNameIsNull_ThrowsConfigError()
        {
            using (new TestConfigurationScope(new Dictionary<string, string>(), new Dictionary<string, string>()))
            {
                ServiceFault fault = FaultAssert.Capture(() =>
                    AuthServiceContext.ResolveConnectionString(null));

                Assert.AreEqual(AuthServiceConstants.ERROR_CONFIG, fault.Code);
                Assert.AreEqual(AuthServiceConstants.MESSAGE_CONFIG_ERROR, fault.Message);
            }
        }

        [TestMethod]
        public void ResolveConnectionString_WhenNameIsWhitespace_ThrowsConfigError()
        {
            using (new TestConfigurationScope(new Dictionary<string, string>(), new Dictionary<string, string>()))
            {
                ServiceFault fault = FaultAssert.Capture(() =>
                    AuthServiceContext.ResolveConnectionString(CONNECTION_NAME_WHITESPACE));

                Assert.AreEqual(AuthServiceConstants.ERROR_CONFIG, fault.Code);
                Assert.AreEqual(AuthServiceConstants.MESSAGE_CONFIG_ERROR, fault.Message);
            }
        }

        [TestMethod]
        public void CodeTtlMinutes_WhenAppSettingHasWhitespace_ReturnsConfiguredValue()
        {
            var app = new Dictionary<string, string>
            {
                { AuthServiceConstants.APPSETTING_EMAIL_CODE_TTL_MINUTES, APPSETTING_CODE_TTL_WITH_WHITESPACE }
            };

            using (new TestConfigurationScope(app, new Dictionary<string, string>()))
            {
                Assert.AreEqual(CONFIGURED_CODE_TTL_MINUTES, AuthServiceContext.CodeTtlMinutes);
            }
        }

        [TestMethod]
        public void ResendCooldownSeconds_WhenAppSettingHasWhitespace_ReturnsConfiguredValue()
        {
            var app = new Dictionary<string, string>
            {
                { AuthServiceConstants.APPSETTING_EMAIL_RESEND_COOLDOWN_SECONDS, APPSETTING_RESEND_COOLDOWN_WITH_WHITESPACE }
            };

            using (new TestConfigurationScope(app, new Dictionary<string, string>()))
            {
                Assert.AreEqual(CONFIGURED_RESEND_COOLDOWN_SECONDS, AuthServiceContext.ResendCooldownSeconds);
            }
        }

        [TestMethod]
        public void TryGetUserId_WhenTokenExpiresAtNow_ReturnsFalseAndRemovesToken()
        {
            string tokenValue = "expired.now." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT);

            var expiredToken = new AuthToken
            {
                UserId = TOKEN_TEST_USER_ID,
                Token = tokenValue,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(EXPIRES_AT_UTC_OFFSET_SECONDS)
            };

            SeedTokenStore(expiredToken, activeTokenValue: tokenValue);

            bool ok = AuthServiceContext.TryGetUserId(tokenValue, out int userId);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, userId);
            Assert.IsFalse(CacheContainsToken(tokenValue));
        }


        private static void SeedTokenStore(AuthToken token, string activeTokenValue)
        {
            if (token == null)
            {
                return;
            }

            Type tokenStoreType = GetTokenStoreTypeOrFail();
            object cache = GetStaticFieldValue(tokenStoreType, TOKEN_STORE_FIELD_CACHE);
            object activeByUser = GetStaticFieldValue(tokenStoreType, TOKEN_STORE_FIELD_ACTIVE_BY_USER);

            InvokeTryAdd(cache, token.Token, token);
            SetIndexerValue(activeByUser, token.UserId, activeTokenValue);
        }

        private static bool CacheContainsToken(string tokenValue)
        {
            Type tokenStoreType = GetTokenStoreTypeOrFail();
            object cache = GetStaticFieldValue(tokenStoreType, TOKEN_STORE_FIELD_CACHE);

            MethodInfo containsKey = cache?.GetType().GetMethod("ContainsKey", BindingFlags.Instance | BindingFlags.Public);
            if (containsKey == null)
            {
                Assert.Fail("ContainsKey method was not found in TokenStore.Cache.");
                return false;
            }

            object result = containsKey.Invoke(cache, new object[] { tokenValue });
            return result is bool b && b;
        }

        private static Type GetTokenStoreTypeOrFail()
        {
            Assembly serverAssembly = typeof(ServicesTheWeakestRival.Server.Services.AuthService).Assembly;

            Type tokenStoreType = serverAssembly.GetType(TOKEN_STORE_TYPE_NAME, throwOnError: false);
            if (tokenStoreType == null)
            {
                Assert.Fail("TokenStore type was not found: " + TOKEN_STORE_TYPE_NAME);
            }

            return tokenStoreType;
        }

        private static object GetStaticFieldValue(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                Assert.Fail("TokenStore field was not found: " + fieldName);
                return null;
            }

            return field.GetValue(null);
        }

        private static void InvokeTryAdd(object dictionary, object key, object value)
        {
            if (dictionary == null)
            {
                Assert.Fail("TokenStore.Cache instance is null.");
                return;
            }

            MethodInfo tryAdd = dictionary.GetType().GetMethod("TryAdd", BindingFlags.Instance | BindingFlags.Public);
            if (tryAdd == null)
            {
                Assert.Fail("TryAdd method was not found in TokenStore.Cache.");
                return;
            }

            tryAdd.Invoke(dictionary, new[] { key, value });
        }

        private static void SetIndexerValue(object dictionary, object key, object value)
        {
            if (dictionary == null)
            {
                Assert.Fail("TokenStore.ActiveTokenByUserId instance is null.");
                return;
            }

            PropertyInfo indexer = dictionary.GetType().GetProperty("Item", BindingFlags.Instance | BindingFlags.Public);
            if (indexer == null)
            {
                Assert.Fail("Indexer property was not found in TokenStore.ActiveTokenByUserId.");
                return;
            }

            indexer.SetValue(dictionary, value, new[] { key });
        }

        private sealed class TestConfigurationScope : IDisposable
        {
            private const string NODE_CONFIGURATION = "configuration";
            private const string NODE_APP_SETTINGS = "appSettings";
            private const string NODE_CONNECTION_STRINGS = "connectionStrings";

            private const string ELEMENT_ADD = "add";
            private const string ATTR_KEY = "key";
            private const string ATTR_VALUE = "value";

            private const string ATTR_NAME = "name";
            private const string ATTR_CONNECTION_STRING = "connectionString";
            private const string ATTR_PROVIDER_NAME = "providerName";

            private readonly string previousAppConfigPath;
            private readonly string tempConfigPath;

            public TestConfigurationScope(
                IDictionary<string, string> appSettings,
                IDictionary<string, string> connectionStrings)
            {
                previousAppConfigPath = AppDomain.CurrentDomain.GetData(APP_CONFIG_FILE_KEY) as string;

                tempConfigPath = Path.Combine(
                    Path.GetTempPath(),
                    "AuthServiceContextTests." + Guid.NewGuid().ToString(AuthServiceConstants.TOKEN_GUID_FORMAT) + ".config");

                XDocument doc = BuildConfigDocument(appSettings, connectionStrings);
                doc.Save(tempConfigPath);

                AppDomain.CurrentDomain.SetData(APP_CONFIG_FILE_KEY, tempConfigPath);

                ResetConfigurationManager();
                ConfigurationManager.RefreshSection(NODE_APP_SETTINGS);
                ConfigurationManager.RefreshSection(NODE_CONNECTION_STRINGS);
            }

            public void Dispose()
            {
                AppDomain.CurrentDomain.SetData(APP_CONFIG_FILE_KEY, previousAppConfigPath);

                ResetConfigurationManager();
                ConfigurationManager.RefreshSection(NODE_APP_SETTINGS);
                ConfigurationManager.RefreshSection(NODE_CONNECTION_STRINGS);

                TryDeleteTempConfig();
            }

            private static XDocument BuildConfigDocument(
                IDictionary<string, string> appSettings,
                IDictionary<string, string> connectionStrings)
            {
                var root = new XElement(NODE_CONFIGURATION);

                root.Add(BuildAppSettings(appSettings ?? new Dictionary<string, string>()));
                root.Add(BuildConnectionStrings(connectionStrings ?? new Dictionary<string, string>()));

                return new XDocument(root);
            }

            private static XElement BuildAppSettings(IDictionary<string, string> appSettings)
            {
                var node = new XElement(NODE_APP_SETTINGS);

                foreach (var kv in appSettings)
                {
                    node.Add(new XElement(
                        ELEMENT_ADD,
                        new XAttribute(ATTR_KEY, kv.Key ?? string.Empty),
                        new XAttribute(ATTR_VALUE, kv.Value ?? string.Empty)));
                }

                return node;
            }

            private static XElement BuildConnectionStrings(IDictionary<string, string> connectionStrings)
            {
                var node = new XElement(NODE_CONNECTION_STRINGS);

                foreach (var kv in connectionStrings)
                {
                    node.Add(new XElement(
                        ELEMENT_ADD,
                        new XAttribute(ATTR_NAME, kv.Key ?? string.Empty),
                        new XAttribute(ATTR_CONNECTION_STRING, kv.Value ?? string.Empty),
                        new XAttribute(ATTR_PROVIDER_NAME, PROVIDER_SQLCLIENT)));
                }

                return node;
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
                            "{0}: Failed to delete temp config. Path='{1}'. ExceptionType='{2}'. Message='{3}'.",
                            CTX_DELETE_TEMP_CONFIG,
                            tempConfigPath ?? string.Empty,
                            ex.GetType().FullName ?? string.Empty,
                            ex.Message ?? string.Empty));
                }
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
        }
    }
}
