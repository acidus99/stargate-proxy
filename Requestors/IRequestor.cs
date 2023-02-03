using System;
namespace Stargate.Requestors
{
	public interface IRequestor
	{
		SourceResponse Request(Uri url);

		bool SupportsProtocol(Uri url);
	}
}

