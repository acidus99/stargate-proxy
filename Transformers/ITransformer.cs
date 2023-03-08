using System;

using RocketForce;

namespace Stargate.Transformers
{
	public interface ITransformer
	{
		bool CanTransform(string mimeType);

		SourceResponse Transform(Request request, SourceResponse response);
	}
}

