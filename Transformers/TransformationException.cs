using System;
namespace Stargate.Transformers
{
	public class TransformationException : ApplicationException
	{
		public TransformationException(string msg) :
			base(msg)
		{
		}
	}
}

