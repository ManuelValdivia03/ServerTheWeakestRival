using System;
using System.Configuration;

namespace ServerTheWeakestRival.Tests.Unit.Infrastructure
{
    internal static class DbTestConfig
    {
        internal const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        internal static string GetMainConnectionString()
        {
            TestConfigBootstrapper.EnsureLoaded();

            ConnectionStringSettings cs = ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];
            if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
            {
                throw new InvalidOperationException(
                    string.Format("Missing connection string '{0}' after bootstrap.", MAIN_CONNECTION_STRING_NAME));
            }

            return cs.ConnectionString;
        }
    }
}
