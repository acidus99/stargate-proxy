using System.Security.Cryptography.X509Certificates;
using RocketForce;

namespace Stargate;

internal class Program
{
    private static void Main(string[] args)
    {
        const string DefaultCertFileName = "localhost-proxy.crt";
        const string DefaultKeyFileName = "localhost-proxy.key";
        const string LocalSubjectName = "$CN=localhost, O=Stargate Proxy Local Certificate, OU=gemi.dev";

        string hostname;
        int port;
        X509Certificate2 certificate;
        
        Console.WriteLine("Stargate 💫🚪 Gemini-to-HTTP proxy");
        
        if (args.Length == 0)
        {
            hostname = "localhost";
            port = 1994;
            
            var currentDirectory = Directory.GetCurrentDirectory();
            string certFilePath = Path.Combine(currentDirectory, DefaultCertFileName);
            string keyFilePath = Path.Combine(currentDirectory, DefaultKeyFileName);
            
            //see if we already have an existing cert
            if (!CertificateUtils.TryLoadCertificate(certFilePath, keyFilePath, out certificate))
            {
                Console.WriteLine("No valid certificate found. Generating new certificate/private key.");
                
                //nope, generate them
                if (!CertificateUtils.CreateLocalCertificates(certFilePath, keyFilePath, LocalSubjectName))
                {
                    Console.WriteLine("Failed to generated certificate. Perhaps a write permissions issue?");
                    return;
                }

                if (!CertificateUtils.TryLoadCertificate(certFilePath, keyFilePath, out certificate))
                {
                    Console.WriteLine("Could not load generated certificate/key. Perhaps a write permissions issue?");
                    return;
                }
            }
            //good to go
        } else if (args.Length == 4)
        {

            hostname = args[0];
            string portArg = args[1];
            string certFilePath = args[2];
            string keyFilePath = args[3];

            if (!IsValidHostname(hostname))
            {
                Console.WriteLine("Invalid hostname format.");
                PrintUsage();
                return;
            }

            if (!IsValidPort(portArg, out port))
            {
                Console.WriteLine("Invalid port number. Must be an integer between 1 and 65535.");
                PrintUsage();
                return;
            }

            if (!File.Exists(certFilePath))
            {
                Console.WriteLine("Certificate file does not exist.");
                PrintUsage();
                return;
            }

            if (!File.Exists(keyFilePath))
            {
                Console.WriteLine("Key file does not exist.");
                PrintUsage();
                return;
            }

            if (!CertificateUtils.TryLoadCertificate(certFilePath, keyFilePath, out certificate))
            {
                Console.WriteLine("Could not load certifcate. Files may be the wrong format, or private key does not match certificate.");
                PrintUsage();
                return;
            }
            //good to go
        }
        else
        {
            Console.WriteLine("Invalid arguments");
            PrintUsage();
            return;
        }

        var proxy = new GeminiProxyServer(hostname,
            port,
            certificate
        )
        {
            IsMaskingRemoteIPs = false
        };
        Console.WriteLine($"Proxy started on {hostname}:{port}");
        proxy.Run();
    }
    
    public static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine();
        Console.WriteLine("1. Auto-generate a self-signed certificate and start the proxy:");
        Console.WriteLine("   ./stargate");
        Console.WriteLine("   This will auto-generate a self-signed certificate and start the proxy running on localhost on port 1994.");
        Console.WriteLine();

        Console.WriteLine("2. Provide a hostname, port, certificate file, and private key file to start the proxy:");
        Console.WriteLine("   ./stargate <hostname> <port> <certificate_file_path> <private_key_file_path>");
        Console.WriteLine("   - <hostname>: The domain name or IP address the proxy should bind to.");
        Console.WriteLine("   - <port>: The port on which the proxy should listen.");
        Console.WriteLine("   - <certificate_file_path>: The path to the PEM certificate file.");
        Console.WriteLine("   - <private_key_file_path>: The path to the PEM private key file.");
        Console.WriteLine();
    }
    
    static bool IsValidHostname(string hostname)
        =>Uri.CheckHostName(hostname) == UriHostNameType.Dns;
    
    static bool IsValidPort(string portArg, out int port)
        => int.TryParse(portArg, out port) && port > 0 && port <= 65535;
}