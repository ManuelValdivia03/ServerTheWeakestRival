using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Services;

namespace ConsoleServer
{
    class Program
    {
        static void Main()
        {
            using (var host = new ServiceHost(typeof(AuthService)))
            using (var hostLobby = new ServiceHost(typeof(LobbyService)))
            using (var hostMatchmaking = new ServiceHost(typeof(MatchmakingService)))
            using (var hostGameplay = new ServiceHost(typeof(GameplayService)))
            using (var hostStats = new ServiceHost(typeof(StatsService)))
            {
                host.Open();
                hostLobby.Open();
                hostMatchmaking.Open();
                hostGameplay.Open();
                hostStats.Open();

                Console.WriteLine("Servicios WCF corriendo...");
                Console.ReadLine();
            }
        }
    }
}
