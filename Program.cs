
namespace Stargate
{
    class Program
    {
        static void Main(string[] args)
        {

            if(args.Length != 4)
            {
                Console.WriteLine("Pass cert and key via args");
                Environment.Exit(0);
            }

            var host = args[0];
            var port = Convert.ToInt32(args[1]);
            var cert = args[2];
            var key = args[3];

            App app = new App(host,
                port,
                CertificateUtils.LoadCertificate(cert, key)
                )
            {
                IsMaskingRemoteIPs = false
            };

            app.Run();
        }

    }
}
