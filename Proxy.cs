using System;
using HtmlToGmi;

using Stargate.Requestors;
using Stargate.Transformers;

namespace Stargate
{
	public class Proxy
	{
        private IRequestor netRequestor = new NetRequestor();

        private ResponseTransformer transformer = new ResponseTransformer();

        /// <summary>
        /// Does this proxy know how to handle the protocol for a URL?
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public bool SupportsProtocol(Uri url)
            => netRequestor.SupportsProtocol(url);

        /// <summary>
        /// Proxy the request, by fetching via the source protocol, transforming it if need be, and sending it back to the client
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        public void ProxyRequest(Request request, Response response)
        {
            //TODO: check caching layer
            try
            {
                //proxy the request
                SourceResponse sourceResponse = netRequestor.Request(request.Url);

                //make any transforms to the response
                sourceResponse = transformer.Transform(request, sourceResponse);

                //write the response back to the client
                response.WriteStatusLine(sourceResponse.StatusCode, sourceResponse.Meta);
                if (sourceResponse.Body != null)
                {
                    response.CopyFrom(sourceResponse.Body);
                }
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                response.Error(msg);
            }
        }
    }
}

