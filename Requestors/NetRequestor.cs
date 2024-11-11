using Stargate.Requestors.Http;

namespace Stargate.Requestors;

/// <summary>
///     Holds all our various IRequestors, selects the appropriate one
/// </summary>
public class NetRequestor : IRequestor
{
    private readonly IRequestor[] requestors = { new HttpRequestor() };

    public SourceResponse Request(Uri url)
    {
        foreach (var requestor in requestors)
            if (requestor.SupportsProtocol(url))
                return requestor.Request(url);
        throw new ApplicationException($"No known requestor for protocol '{url.Scheme}'");
    }

    public bool SupportsProtocol(Uri url)
    {
        foreach (var requestor in requestors)
            if (requestor.SupportsProtocol(url))
                return true;
        return false;
    }
}