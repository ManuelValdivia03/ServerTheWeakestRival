using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using ServicesTheWeakestRival;

class Program
{
    static void Main()
    {
        var baseAddress = new Uri("net.tcp://localhost:8080/WeakestRival/");

        using (var host = new ServiceHost(typeof(GameService), baseAddress))
        {
            var binding = new NetTcpBinding(SecurityMode.None)
            {
                MaxReceivedMessageSize = 4 * 1024 * 1024
            };

            host.AddServiceEndpoint(typeof(IGameService), binding, "GameService");

            var smb = new ServiceMetadataBehavior();
            host.Description.Behaviors.Add(smb);
            host.AddServiceEndpoint(ServiceMetadataBehavior.MexContractName,
                                    MetadataExchangeBindings.CreateMexTcpBinding(),
                                    "mex");

            host.Open();
            Console.WriteLine("WCF arriba en net.tcp://localhost:8080/WeakestRival/GameService");
            Console.WriteLine("Presiona Enter para salir.");
            Console.ReadLine();
            host.Close();
        }
    }
}