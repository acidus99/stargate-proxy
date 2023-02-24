using System;
using System.Net.Http.Headers;
namespace Stargate
{
	public class SourceResponse
	{
		/// <summary>
		/// Status code to use in the Gemini Response
		/// </summary>
		public int StatusCode { get; set; } = 0;

		/// <summary>
		/// Meta to use for the Gemini Response
		/// </summary>
		public string Meta { get; set; } = "";

		/// <summary>
		/// The content type of the s
		/// </summary>
		public MediaTypeHeaderValue SourceContentType { get; set; }

		public Stream Body { get; set; } = null;
	}
}

