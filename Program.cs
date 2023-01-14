
namespace Stargate
{
    class Program
    {
        static void Main(string[] args)
        {

            if(args.Length != 2)
            {
                Console.WriteLine("Pass cert and key via args");
                Environment.Exit(0);
            }

            var cert = args[0];
            var key = args[1];

            App app = new App("localhost",
                1994,
                CertificateUtils.LoadCertificate(cert, key)
                );

            app.Run();
        }

    }
}
