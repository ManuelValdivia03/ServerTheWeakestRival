using System;
using System.ServiceModel;

namespace ConsoleServer
{
    class Program
    {
        static void Main()
        {
            var hosts = new ServiceHost[]
            {
                new ServiceHost(typeof(ServicesTheWeakestRival.Server.Services.AuthService)),
                new ServiceHost(typeof(ServicesTheWeakestRival.Server.Services.LobbyService)),
                new ServiceHost(typeof(ServicesTheWeakestRival.Server.Services.MatchmakingService)),
                new ServiceHost(typeof(ServicesTheWeakestRival.Server.Services.GameplayService)),
                new ServiceHost(typeof(ServicesTheWeakestRival.Server.Services.StatsService)),
            };


            foreach (var h in hosts) h.Open();
            foreach (var h in hosts) Console.WriteLine(h.Description.ServiceType.FullName);

            Console.WriteLine("Servicios WCF corriendo en http://localhost:8082/");
            Console.ReadLine();

            foreach (var h in hosts) h.Close();
        }
    }
}
