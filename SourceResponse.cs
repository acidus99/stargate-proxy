using System;
namespace Stargate
{
	public class SourceResponse
	{
		public int StatusCode { get; set; } = 0;

		public string Meta { get; set; } = "";

		public Stream Body { get; set; } = null;
	}
}

