using RocketForce;

namespace Stargate;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.WriteLine("Incorrect parameters");
            Console.WriteLine("stargate [hostname] [port] [cert file] [key file]");
            Environment.Exit(0);
        }

        var host = args[0];
        var port = Convert.ToInt32(args[1]);
        var cert = args[2];
        var key = args[3];

        var proxy = new GeminiProxyServer(host,
            port,
            CertificateUtils.LoadCertificate(cert, key)
        )
        {
            IsMaskingRemoteIPs = false
        };
        Console.WriteLine("Started Proxy");
        proxy.Run();
    }
}