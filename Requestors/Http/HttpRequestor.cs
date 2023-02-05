using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace Stargate.Requestors.Http
{
    public class HttpRequestor : IRequestor
    {
        HttpClient Client;

        public HttpRequestor()
        {
            Client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CheckCertificateRevocationList = false,
                AutomaticDecompression = System.Net.DecompressionMethods.None,
            });

            Client.Timeout = TimeSpan.FromSeconds(20);
            Client.DefaultRequestHeaders.UserAgent.TryParseAdd("GeminiProxy/0.1 (gemini://gemi.dev/) gemini-proxy/0.1");
        }

        public SourceResponse Request(Uri url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var httpResponse = Client.Send(request, HttpCompletionOption.ResponseHeadersRead);
            return TranslateResponse(httpResponse);
        }

        public bool SupportsProtocol(Uri url)
            => (url.Scheme == "http" || url.Scheme == "https");

        public SourceResponse TranslateResponse(HttpResponseMessage http)
        {
            SourceResponse ret;

            switch((int) http.StatusCode)
            {
                case 200:
                    ret = new SourceResponse
                    {
                        StatusCode = 20,
                        //TODO: handle charset, language
                        Meta = http.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                        Body = http.Content.ReadAsStream()
                    };
                    break;

                //prem redirect
                case 301:
                case 308:
                    ret = new SourceResponse
                    {
                        StatusCode = 31,
                        Meta = http.Headers.Location?.AbsoluteUri ?? ""
                    };
                    break;

                case 302:
                case 307:
                    ret = new SourceResponse
                    {
                        StatusCode = 30,
                        Meta = http.Headers.Location?.AbsoluteUri ?? ""
                    };
                    break;

                case 404:
                    ret = new SourceResponse
                    {
                        StatusCode = 51,
                        Meta = "File not found"
                    };
                    break;

                case 410:
                    ret = new SourceResponse
                    {
                        StatusCode =  52,
                        Meta = "Gone"
                    };
                    break;

                default:
                    //default to generic temp error
                    ret = new SourceResponse
                    {
                        StatusCode = 40,
                        Meta = "Generic error. HTTP response code: " + http.StatusCode
                    };
                    break;
            }

            return ret;
        }
    }
}