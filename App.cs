using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Security.Authentication;
using System.IO;
using System.Threading.Tasks;


using HtmlToGmi;

using Stargate.Requestors;
using Stargate.Logging;
using AngleSharp.Io;

namespace Stargate
{

    public class App
    {
        const int MaxRequestSize = 2048;

        /// <summary>
        /// Should we mask IPs of remote clients
        /// </summary>
        public bool IsMaskingRemoteIPs { get; set; } = true;

        private readonly X509Certificate2 serverCertificate;
        private readonly TcpListener listener;

        private string hostname;
        private int port;

        private Proxy proxy;
        private W3CLogger logger;

        public App(string hostname, int port, X509Certificate2 certificate)
        {
            this.hostname = hostname;
            this.port = port;
            listener = TcpListener.Create(port);

            serverCertificate = certificate;
            proxy = new Proxy();
            logger = new W3CLogger(Console.Out);
        }
       
        public void Run()
        {
            if(serverCertificate == null)
            {
                Console.Error.WriteLine("Could not Load Server Key/Certificate. Exiting.");
                return;
            }

            try
            {
                DisplayLaunchBanner();
                listener.Start();
                while (true)
                {
                    var client = listener.AcceptTcpClient();
                    Task.Run(() => ProcessRequest(client));
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                listener.Stop();
            }
        }

        private void DisplayLaunchBanner()
        {
            Console.WriteLine("#3... 2... 1...");
            Console.WriteLine(
@"#  ____  _                        _       
# / ___|| |_ __ _ _ __ __ _  __ _| |_ ___ 
# \___ \| __/ _` | '__/ _` |/ _` | __/ _ \
#  ___) | || (_| | | | (_| | (_| | ||  __/
# |____/ \__\__,_|_|  \__, |\__,_|\__\___|
#                     |___/               ");

            Console.WriteLine("#Gemini Gateway");
            Console.WriteLine($"#Hostname: {hostname}");
            Console.WriteLine($"#Port: {port}");
        }

        private void ProcessRequest(TcpClient client)
        {
            SslStream sslStream = null;
            try
            {
                sslStream = new SslStream(client.GetStream(), false);
                var received = DateTime.Now;
                var remoteIP = getClientIP(client);
                ProcessRequest(remoteIP, sslStream);
            }
            catch (AuthenticationException)
            {
            }
            //Ensure that an exception processing a request doesn't take down the whole server
            catch (Exception)
            {
            }
            finally
            {
                sslStream.Close();
                client.Close();
            }
        }

        /// <summary>
        /// attempts to get the IP address of the remote client, or mask it
        /// </summary>
        private string getClientIP(TcpClient client)
        {
            if (!IsMaskingRemoteIPs && client.Client.RemoteEndPoint != null && (client.Client.RemoteEndPoint is IPEndPoint))
            {
                return (client.Client.RemoteEndPoint as IPEndPoint).Address.ToString();
            }
            return "-";
        }

        private void ProcessRequest(string remoteIP, SslStream sslStream)
        {
            sslStream.ReadTimeout = 5000;
            sslStream.AuthenticateAsServer(serverCertificate, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            string rawRequest = null;
            var response = new Response(sslStream);

            var received = DateTime.Now;

            try
            {
                // Read a message from the client.
                rawRequest = ReadRequest(sslStream);
            }
            catch (ApplicationException ex)
            {
                response.BadRequest(ex.Message);
                LogInvalidRequest(received, remoteIP, response);
                return;
            }
            
            var url = ValidateRequest(rawRequest, response);
            if(url == null)
            {
                //we already populated the response object and reported the
                //appropriate status to the client, so we can exit
                LogInvalidRequest(received, remoteIP, rawRequest, response);
                return;
            }

            var request = new Request
            {
                Url = url,
                RemoteIP = remoteIP
            };

            //proxy it
            proxy.ProxyRequest(request, response);
            LogAccess(received, request, response);
        }

        public void LogAccess(DateTime received, Request request, Response response)
        {
            var completed = DateTime.Now;

            var record = new AccessRecord
            {
                Date = AccessRecord.FormatDate(received),
                Time = AccessRecord.FormatTime(received),
                RemoteIP = request.RemoteIP,
                Url = request.Url.AbsoluteUri,
                StatusCode = response.StatusCode.ToString(),
                Meta = response.Meta,
                SentBytes = response.Length.ToString(),
                TimeTaken = AccessRecord.ComputeTimeTaken(received, completed)
            };
            logger.LogAccess(record);
        }

        public void LogInvalidRequest(DateTime received, string remoteIP, Response response)
        {
            var completed = DateTime.Now;

            var record = new AccessRecord
            {
                Date = AccessRecord.FormatDate(received),
                Time = AccessRecord.FormatTime(received),
                RemoteIP = remoteIP,
                StatusCode = response.StatusCode.ToString(),
                Meta = response.Meta,
                SentBytes = response.Length.ToString(),
                TimeTaken = AccessRecord.ComputeTimeTaken(received, completed)
            };
            logger.LogAccess(record);
        }

        public void LogInvalidRequest(DateTime received, string remoteIP, string rawRequest, Response response)
        {
            var completed = DateTime.Now;

            var record = new AccessRecord
            {
                Date = AccessRecord.FormatDate(received),
                Time = AccessRecord.FormatTime(received),
                RemoteIP = remoteIP,
                Url = AccessRecord.Sanitize(rawRequest, false),
                StatusCode = response.StatusCode.ToString(),
                Meta = response.Meta,
                SentBytes = response.Length.ToString(),
                TimeTaken = AccessRecord.ComputeTimeTaken(received, completed)
            };
            logger.LogAccess(record);
        }

        /// <summary>
        /// Validates the raw gemini request. If not, writes the appropriate
        /// errors on the response object and returns null. if valid, returns
        /// GeminiUrl object
        /// </summary>
        private Uri ValidateRequest(string rawRequest, Response response)
        {

            Uri ret = null;

            //The order of these checks, and the status codes they return, may seem odd
            //and are organized to pass the gemini-diagnostics check
            //https://github.com/michael-lazar/gemini-diagnostics

            if (rawRequest == null)
            {
                response.BadRequest("Missing URL");
                return null;
            }

            try
            {
                ret = new Uri(rawRequest);
            }
            catch (Exception)
            {
                response.BadRequest("Invalid URL");
                return null;
            }

            //Silly .NET URI will parse "/" as a "file" scheme with a "/" path! crazy
            //and say it is absolute. So explicitly look for :// to determine if absolute 
            if(!rawRequest.Contains("://"))
            {
                response.BadRequest("Relative URLs not allowed");
                return null;
            }

            if(!proxy.SupportsProtocol(ret))
            {
                //refuse to proxy to other protocols
                response.ProxyRefused("protocols");
                return null;
            }

            return ret;
        }


        /// <summary>
        /// Reads the request URL from the client.
        /// This looks complex, but allows for slow clients where the entire URL is not
        /// available in a single read from the buffer
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private string ReadRequest(Stream stream)
        {
            var requestBuffer = new List<byte>(MaxRequestSize);
            byte[] readBuffer = { 0 };

            int readCount = 0;
            while (stream.Read(readBuffer, 0, 1) == 1)
            {
                if (readBuffer[0] == (byte)'\r')
                {
                    //spec requires a \n next
                    stream.Read(readBuffer, 0, 1);
                    if (readBuffer[0] != (byte)'\n')
                    {
                        throw new ApplicationException("Invalid Request. Request line missing LF after CR");
                    }
                    break;
                }
                //keep going if we haven't read too many
                readCount++;
                if (readCount > MaxRequestSize)
                {
                    throw new ApplicationException($"Invalid Request. Did not find CRLF within {MaxRequestSize} bytes of request line");
                }
                requestBuffer.Add(readBuffer[0]);
            }
            //the URL itself should not be longer than the max size minus the trailing CRLF
            if(requestBuffer.Count > MaxRequestSize - 2)
            {
                throw new ApplicationException($"Invalid Request. URL exceeds {MaxRequestSize - 2}");
            }
            //spec requires request use UTF-8
            return Encoding.UTF8.GetString(requestBuffer.ToArray());
        }
    }
}
