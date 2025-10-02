using System.Net;
using System.Text;
using Stargate.Helpers;

namespace Stargate.Requestors.Http;

public class HttpRequestor : IRequestor
{
    private readonly HttpClient client;

    public HttpRequestor()
    {
        client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CheckCertificateRevocationList = false,
            AutomaticDecompression = DecompressionMethods.All
        });

        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.TryParseAdd("GeminiProxy/0.1 (gemini://gemi.dev/) gemini-proxy/0.1");
    }

    public SourceResponse Request(Uri url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var httpResponse = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
        return TranslateResponse(httpResponse);
    }

    public bool SupportsProtocol(Uri url)
    {
        return url.Scheme == "http" || url.Scheme == "https";
    }

    public SourceResponse TranslateResponse(HttpResponseMessage http)
    {
        SourceResponse ret;

        switch ((int)http.StatusCode)
        {
            case 200:
                ret = Process200(http);
                break;

            //prem redirect
            case 301:
            case 308:
                ret = new SourceResponse
                {
                    StatusCode = 31,
                    Meta = ResolveRedirect(http.RequestMessage.RequestUri, http.Headers.Location)
                };
                break;

            case 302:
            case 307:
                ret = new SourceResponse
                {
                    StatusCode = 30,
                    Meta = ResolveRedirect(http.RequestMessage.RequestUri, http.Headers.Location)
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
                    StatusCode = 52,
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

    //Parses a 200 response and validates of a charset (if present) can be parsed
    //this allows us to return a 40 now if the charset is bad
    private SourceResponse Process200(HttpResponseMessage http)
    {
        var ret = new SourceResponse
        {
            StatusCode = 20,
            Meta = http.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",

            SourceContentType = http.Content.Headers.ContentType,
            Body = http.Content.ReadAsStream()
        };
        
        string? normalizeCharset = MimeHelper.NormalizeCharset(http.Content.Headers.ContentType);
        
        //sanity check the charset and return an error if we can't support it
        if (normalizeCharset != null)
        {
            try
            {
                Encoding.GetEncoding(normalizeCharset);
            }
            catch (ArgumentException)
            {
                ret.StatusCode = 40;
                ret.Meta = $"Error: Remote Content-Type uses an invalid/unsupported charset `{normalizeCharset}`";
            }
        }

        return ret;
    }
    
    private string ResolveRedirect(Uri requestUrl, Uri redirectUrl)
    {
        if (redirectUrl == null) return "";
        var resolvedUrl = new Uri(requestUrl, redirectUrl);
        return resolvedUrl.AbsoluteUri;
    }
}