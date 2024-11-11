using System.Security.Cryptography.X509Certificates;
using RocketForce;
using Stargate.Requestors;
using Stargate.Transformers;

namespace Stargate;

public class GeminiProxyServer : AbstractGeminiApp
{
    private readonly IRequestor netRequestor;
    private readonly ResponseTransformer transformer;

    public GeminiProxyServer(string hostname, int port, X509Certificate2 certificate)
        : base(hostname, port, certificate)
    {
        netRequestor = new NetRequestor();
        transformer = new ResponseTransformer();
    }

    public override void ProcessRequest(Request request, Response response)
    {
        //TODO: check caching layer
        try
        {
            //proxy the request
            var sourceResponse = netRequestor.Request(request.Url);

            //make any transforms to the response
            sourceResponse = transformer.Transform(request, sourceResponse);

            //write the response back to the client
            response.WriteStatusLine(sourceResponse.StatusCode, sourceResponse.Meta);
            if (sourceResponse.Body != null) response.CopyFrom(sourceResponse.Body);
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            response.Error(msg);
        }
    }

    protected override bool IsValidRequest(Uri url, Response response)
    {
        //only valid if we support the protocols
        if (!SupportsProtocol(url))
        {
            //refuse to proxy to other protocols
            response.ProxyRefused("protocols");
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Does this proxy know how to handle the protocol for a URL?
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    private bool SupportsProtocol(Uri url)
    {
        return netRequestor.SupportsProtocol(url);
    }
}