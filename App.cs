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

using Stargate.Net;
using HtmlToGmi;

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

        public App(string hostname, int port, X509Certificate2 certificate)
        {
            this.hostname = hostname;
            this.port = port;
            listener = TcpListener.Create(port);

            serverCertificate = certificate;
        }
       
        public void Run()
        {
            if(serverCertificate == null)
            {
                Console.WriteLine("Could not Load Server Key/Certificate. Exiting.");
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
            catch (Exception e)
            {
            }
            finally
            {
                listener.Stop();
            }
        }

        private void DisplayLaunchBanner()
        {
            Console.WriteLine("3... 2... 1...");
            Console.WriteLine(
@" ____  _                        _       
/ ___|| |_ __ _ _ __ __ _  __ _| |_ ___ 
\___ \| __/ _` | '__/ _` |/ _` | __/ _ \
 ___) | || (_| | | | (_| | (_| | ||  __/
|____/ \__\__,_|_|  \__, |\__,_|\__\___|
                    |___/               ");

            Console.WriteLine("Gemini Gateway");
            Console.WriteLine();
            Console.WriteLine($"Hostname:\t{hostname}");
            Console.WriteLine($"Port:\t\t{port}");
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
            catch (AuthenticationException e)
            {
            }
            //Ensure that an exception processing a request doesn't take down the whole server
            catch (Exception e)
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

            try
            {
                // Read a message from the client.
                rawRequest = ReadRequest(sslStream);
            } catch(ApplicationException ex)
            {
                response.BadRequest(ex.Message);
                return;
            }

            var url = ValidateRequest(rawRequest, response);
            if(url == null)
            {
                //we already reported the appropriate status to the client, exit
                return; 
            }

            var request = new Request
            {
                Url = url,
                RemoteIP = remoteIP
            };

            //handle HTTP request
            ProxyRequest(request, response);
        }

        private void ProxyRequest(Request request, Response response)
        {
            HttpFetcher httpFetcher = new HttpFetcher();
            var html = httpFetcher.GetAsString(request.Url);

            HtmlConverter converter = new HtmlConverter()
            {
                AllowDuplicateLinks = true,
                ShouldRenderHyperlinks = true
            };
            var content = converter.Convert(request.Url.AbsoluteUri, html);

            response.Success();
            response.Write(content.Gemtext);
        }


        /// <summary>
        /// Validates the raw gemini request. If not, writes the appropriate errors on the response object and returns null
        /// if valid, returns GeminiUrl object
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

            if(ret.Scheme != "http" && ret.Scheme != "https")
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
