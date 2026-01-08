using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace ServerTheWeakestRival.Tests.Unit.Infrastructure
{
    internal static class TestConfigBootstrapper
    {
        private const string APP_CONFIG_FILE_KEY = "APP_CONFIG_FILE";

        private const string FILE_SECRETS_NAME = "ConnectionStrings.secrets.config";
        private const string FILE_GENERATED_CONFIG_NAME = "ServerTheWeakestRival.Tests.Runtime.config";

        private const string NODE_CONFIGURATION = "configuration";
        private const string NODE_CONNECTION_STRINGS = "connectionStrings";

        internal static void EnsureLoaded()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string secretsPath = Path.Combine(baseDir, FILE_SECRETS_NAME);

            if (!File.Exists(secretsPath))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings.secrets.config was not found in test bin. Add it as Link and set Copy always.");
            }

            string testConfigPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            if (string.IsNullOrWhiteSpace(testConfigPath) || !File.Exists(testConfigPath))
            {
                throw new InvalidOperationException("Testhost config file was not found.");
            }

            string generatedConfigPath = Path.Combine(baseDir, FILE_GENERATED_CONFIG_NAME);

            XDocument merged = BuildMergedConfig(testConfigPath, secretsPath);
            merged.Save(generatedConfigPath);

            AppDomain.CurrentDomain.SetData(APP_CONFIG_FILE_KEY, generatedConfigPath);

            ResetConfigurationManager();
            ConfigurationManager.RefreshSection(NODE_CONNECTION_STRINGS);
        }

        private static XDocument BuildMergedConfig(string testConfigPath, string secretsPath)
        {
            XDocument testDoc = XDocument.Load(testConfigPath);
            if (testDoc.Root == null || !string.Equals(testDoc.Root.Name.LocalName, NODE_CONFIGURATION, StringComparison.OrdinalIgnoreCase))
            {
                testDoc = new XDocument(new XElement(NODE_CONFIGURATION));
            }

            XDocument secretsDoc = XDocument.Load(secretsPath);
            XElement secretsCs = ExtractConnectionStringsElement(secretsDoc);

            XElement existingCs = testDoc.Root.Element(NODE_CONNECTION_STRINGS);
            if (existingCs != null)
            {
                existingCs.ReplaceWith(new XElement(secretsCs));
            }
            else
            {
                testDoc.Root.AddFirst(new XElement(secretsCs));
            }

            return testDoc;
        }

        private static XElement ExtractConnectionStringsElement(XDocument doc)
        {
            if (doc.Root == null)
            {
                throw new InvalidOperationException("Secrets config is empty.");
            }

            if (string.Equals(doc.Root.Name.LocalName, NODE_CONNECTION_STRINGS, StringComparison.OrdinalIgnoreCase))
            {
                return doc.Root;
            }

            if (string.Equals(doc.Root.Name.LocalName, NODE_CONFIGURATION, StringComparison.OrdinalIgnoreCase))
            {
                XElement cs = doc.Root.Element(NODE_CONNECTION_STRINGS);
                if (cs != null)
                {
                    return cs;
                }
            }

            throw new InvalidOperationException("Secrets config does not contain <connectionStrings>.");
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
