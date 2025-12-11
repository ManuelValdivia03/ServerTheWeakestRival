using System;
using System.ServiceModel;
using ServicesTheWeakestRival.Server.Services;

namespace ConsoleServer
{
    public static class Program
    {
        public static void Main()
        {
            var hosts = new ServiceHost[]
            {
                new ServiceHost(typeof(AuthService)),
                new ServiceHost(typeof(LobbyService)),
                new ServiceHost(typeof(MatchmakingService)),
                new ServiceHost(typeof(GameplayService)),
                new ServiceHost(typeof(StatsService)),
                new ServiceHost(typeof(FriendService)),
                new ServiceHost(typeof(WildcardService)),
            };

            try
            {
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
                foreach (var h in hosts)
                {
                    try { h.Close(); }
                    catch { h.Abort(); }
                }
            }
        }
    }
}
