using System.Net.Security;
using System.Text;
namespace Stargate
{
    public class Response
    {
        public int StatusCode { get; private set; }
        public string Meta { get; private set; }
        /// <summary>
        /// number of bytes sent to the client
        /// </summary>
        public int Length { get; private set; }

        readonly SslStream fout;

        public Response(SslStream respStream)
        {
            fout = respStream;
            StatusCode = 0;
        }

        public void Success(string mimeType = "text/gemini")
            => WriteStatusLine(20, mimeType);

        public void Redirect(string url)
            => WriteStatusLine(30, url);

        public void Missing(string msg)
            => WriteStatusLine(51, msg);

        public void ProxyRefused(string msg)
            => WriteStatusLine(53, $"Will not proxy requests for other {msg}");

        public void BadRequest(string msg)
            => WriteStatusLine(59, msg);

        private void WriteStatusLine(int statusCode, string msg)
        {
            StatusCode = statusCode;
            Meta = msg;
            Write($"{statusCode} {msg}\r\n");
        }

        public void Write(byte[] data)
        {
            Length += data.Length;
            fout.Write(data);
        }

        public void Write(string text)
            => Write(text, Encoding.UTF8);

        public void Write(string text, Encoding encoding)
            => Write(encoding.GetBytes(text));

        public void WriteLine(string text = "")
            => Write(text + "\n", Encoding.UTF8);
    }
}
