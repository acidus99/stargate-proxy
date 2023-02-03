using System;
namespace Stargate
{
	public class SourceResponse
	{
		public int StatusCode { get; set; }

		public string Meta { get; set; } = "";

		public Stream Body { get; set; }
	}
}

