using System.Reflection;
using RocketForce;
using Mono.Options;

namespace Stargate;

public static class Program
{
    const string DefaultHostname = "localhost";
    const int DefaultPort = 1994;
    const string DefaultCertFileName = "localhost-proxy.crt";
    const string DefaultKeyFileName = "localhost-proxy.key";
    const string LocalSubjectName = "CN=localhost, O=Stargate Proxy Local Certificate, OU=gemi.dev";

    private static int Main(string[] args)
    {
        //Defaults
        string hostname = DefaultHostname;
        int port = DefaultPort;
        string? certFilePath = null;
        string? keyFilePath = null;
        bool showHelp = false;
        bool showVersion = false;

        var options = new OptionSet
        {
            // Short aliases chosen to avoid -h, which is reserved for help
            { "p|port=", "TCP port to listen on (default: 1994)", (int v) => port = v },
            { "h|host=", "Hostname to listen on (default: localhost)", v => hostname = v },
            { "c|cert=", "Path to TLS certificate file (PEM/CRT). If provided, --key is required.", v => certFilePath = v },
            { "k|key=", "Path to private key file (PEM). If provided, --cert is required.", v => keyFilePath = v },
            { "v|version", "Show version and exit", v => showVersion = v != null },
            { "help|?", "Show help and exit", v => showHelp = v != null },
        };

        try
        {
            options.Parse(args);
        }
        catch (OptionException e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            Console.Error.WriteLine("Try `stargate --help` for usage.");
            return 1;
        }

        if (showHelp)
        {
            PrintBanner();
            PrintUsage(options);
            return 0;
        }

        if (showVersion)
        {
            PrintBanner();
            return 0;
        }

        //validate options
        // Validate host
        if (!IsValidHostname(hostname))
        {
            Console.Error.WriteLine("Error: Invalid hostname format.");
            Console.Error.WriteLine("Try `stargate --help` for usage.");
            return 1;
        }

        // Validate port
        if (port < 1 || port > 65535)
        {
            Console.Error.WriteLine("Error: Invalid port number. Must be an integer between 1 and 65535.");
            return 1;
        }

        // Enforce cert/key dependency
        if ((certFilePath is null) ^ (keyFilePath is null))
        {
            Console.Error.WriteLine("Error: --cert and --key must be provided together (or neither).");
            return 1;
        }
        
        //enforce cert and key files exist
        if (certFilePath is not null && !File.Exists(certFilePath))
        {
            Console.Error.WriteLine("Error: The certificate file path does not exist.");
            return 1;
        }

        if (keyFilePath is not null && !File.Exists(keyFilePath))
        {
            Console.Error.WriteLine("Error: The private key file path does not exist.");
            return 1;
        }
        
        //if certs/key not specified, generate them
        if (certFilePath is null)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            certFilePath = Path.Combine(currentDirectory, DefaultCertFileName);
            keyFilePath = Path.Combine(currentDirectory, DefaultKeyFileName);
            
            Console.WriteLine("No certificate and private key specified. Generating new certificate/private key. Created files are:");
            Console.Error.WriteLine($"Certificate file path: \"{certFilePath}\"");
            Console.Error.WriteLine($"Private key file path: \"{keyFilePath}\"");
            if (!CertificateUtils.CreateLocalCertificates(certFilePath, keyFilePath, LocalSubjectName))
            {
                Console.Error.WriteLine("Error: Failed to create certificate and/or private key files.");
                return 1;
            }
        }

        if (!CertificateUtils.TryLoadCertificate(certFilePath, keyFilePath, out var certificate))
        {
            Console.Error.WriteLine("Error: Could not read and parse certificate and/or private key files. Files may be the wrong format, or private key does not match certificate. Files used were:");
            Console.Error.WriteLine($"Certificate file path: \"{certFilePath}\"");
            Console.Error.WriteLine($"Private key file path: \"{keyFilePath}\"");
            return 1;
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
        return 0;
    }

    private static void PrintBanner()
    {
        Console.WriteLine($"Stargate {GetProductVersion()} 💫🚪 Gemini-to-HTTP proxy");
    }
    
    private static void PrintUsage(OptionSet options)
    {
        Console.WriteLine();
        Console.WriteLine("Usage: stargate [options]");
        Console.WriteLine();
        Console.WriteLine("If no --cert/--key are supplied, a temporary self-signed certificate and key are generated in the current directory.");
        Console.WriteLine();
        options.WriteOptionDescriptions(Console.Out);
    }

    static string GetProductVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? "Unknown";
    }

    static bool IsValidHostname(string hostname)
        => Uri.CheckHostName(hostname) == UriHostNameType.Dns;

}