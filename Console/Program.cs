using System;
using System.Configuration;
using System.ServiceModel;
using log4net;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services;

namespace ConsoleServer
{
    public static class Program
    {
        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private const string CONTEXT_MAIN = "Program.Main";
        private const string CONTEXT_RECONCILER_DISPOSE = "Program.Reconciler.Dispose";
        private const string CONTEXT_HOST_CLOSE = "Program.ServiceHost.Close";

        private const string MESSAGE_SERVICE_STARTED_FORMAT = "Servicio iniciado: {0}";
        private const string MESSAGE_RUNNING = "Servicios WCF corriendo. Presiona ENTER para salir.";

        private const int EXIT_CODE_SUCCESS = 0;
        private const int EXIT_CODE_FAILURE = 1;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

        public static int Main()
        {
            string connectionString = GetConnectionStringOrThrow();

            var reconciler = new SanctionReconciler(connectionString, Logger);

            ServiceHost[] hosts = CreateHosts();

            try
            {
                reconciler.Start();

                OpenHosts(hosts);

                Console.WriteLine(MESSAGE_RUNNING);
                Console.ReadLine();

                return EXIT_CODE_SUCCESS;
            }
            catch (Exception ex)
            {
                Logger.Error(CONTEXT_MAIN, ex);
                return EXIT_CODE_FAILURE;
            }
            finally
            {
                DisposeReconcilerSafe(reconciler);
                CloseHostsSafe(hosts);
            }
        }

        private static string GetConnectionStringOrThrow()
        {
            ConnectionStringSettings settings =
                ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new ConfigurationErrorsException(
                    string.Format("Missing or empty connection string: {0}", MAIN_CONNECTION_STRING_NAME));
            }

            return settings.ConnectionString;
        }

        private static ServiceHost[] CreateHosts()
        {
            return new[]
            {
                new ServiceHost(typeof(AuthService)),
                new ServiceHost(typeof(LobbyService)),
                new ServiceHost(typeof(MatchmakingService)),
                new ServiceHost(typeof(GameplayService)),
                new ServiceHost(typeof(StatsService)),
                new ServiceHost(typeof(FriendService)),
                new ServiceHost(typeof(WildcardService)),
                new ServiceHost(typeof(ReportService)),
            };
        }

        private static void OpenHosts(ServiceHost[] hosts)
        {
            if (hosts == null) throw new ArgumentNullException(nameof(hosts));

            foreach (ServiceHost host in hosts)
            {
                host.Open();
                Console.WriteLine(MESSAGE_SERVICE_STARTED_FORMAT, host.Description.ServiceType.FullName);
            }
        }

        private static void DisposeReconcilerSafe(SanctionReconciler reconciler)
        {
            if (reconciler == null)
            {
                return;
            }

            try
            {
                reconciler.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error(CONTEXT_RECONCILER_DISPOSE, ex);
            }
        }

        private static void CloseHostsSafe(ServiceHost[] hosts)
        {
            if (hosts == null)
            {
                return;
            }

            foreach (ServiceHost host in hosts)
            {
                try
                {
                    host.Close();
                }
                catch (Exception ex)
                {
                    string hostName = host?.Description?.ServiceType?.FullName ?? "Unknown";
                    Logger.Error(string.Format("{0}. Host={1}", CONTEXT_HOST_CLOSE, hostName), ex);

                    host?.Abort();
                }
            }
        }
    }
}
