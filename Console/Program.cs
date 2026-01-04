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

        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

        public static void Main()
        {
            string connectionString =
                ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME].ConnectionString;

            var reconciler = new SanctionReconciler(connectionString, Logger);

            var hosts = new ServiceHost[]
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

            try
            {
                reconciler.Start();

                foreach (var h in hosts)
                {
                    h.Open();
                    Console.WriteLine("Servicio iniciado: " + h.Description.ServiceType.FullName);
                }

                Console.WriteLine("Servicios WCF corriendo. Presiona ENTER para salir.");
                Console.ReadLine();
            }
            finally
            {
                try { reconciler.Dispose(); }
                catch { }

                foreach (var h in hosts)
                {
                    try { h.Close(); }
                    catch { h.Abort(); }
                }
            }
        }
    }
}
