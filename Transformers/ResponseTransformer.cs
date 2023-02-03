using System;
namespace Stargate.Transformers
{
	public class ResponseTransformer
	{
		ITransformer[] transformers =
		{
			new HtmlTransformer(),
		};

		public SourceResponse Transform(Request request, SourceResponse original)
		{
			foreach(var transformer in transformers)
			{
				if(transformer.CanTransform(original.Meta))
				{
					return transformer.Transform(request, original);
				}
			}
			return original;
		}
		
	}
}

