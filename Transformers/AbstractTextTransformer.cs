using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Stargate.Transformers
{
    /// <summary>
    /// Generic text transformer class with common helper methods
    /// </summary>
	public abstract class AbstractTextTransformer : ITransformer
	{
        private static readonly Regex whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        //Interface methods
        public abstract bool CanTransform(string mimeType);
        public abstract SourceResponse Transform(Request request, SourceResponse response);

        /// <summary>
        /// Reads all the text from the source stream *AND CLOSES IT*
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        protected string ReadAllText(SourceResponse response)
        {
            using (var sr = new StreamReader(response.Body))
                return sr.ReadToEnd();
        }

        /// <summary>
        /// Reads all the bytes from the source stream *AND CLOSES IT*
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        protected byte[] ReadAllBytes(SourceResponse response)
        {
            using (var ms = new MemoryStream()) {
                response.Body.CopyTo(ms);
                return ms.GetBuffer();
            }
        }

        protected void AppendFooter(TextWriter body, int htmlSize, int gmiSize)
        {
            body.WriteLine();
            body.WriteLine();
            body.WriteLine("------");
            body.WriteLine("Teleported and converted via Stargate 💫🚪");
            var emoji = (htmlSize > gmiSize) ? "🤮" : "😳🤬";
            body.WriteLine($"Size: {ReadableFileSize(gmiSize)}. {Savings(gmiSize, htmlSize)} smaller than original: {ReadableFileSize(htmlSize)} {emoji}");
            body.WriteLine("=> mailto:acidus@gemi.dev Made with ❤️ by Acidus");
        }

        protected string Savings(int newSize, int originalSize)
            => string.Format("{0:0.00}%", (1.0d - (Convert.ToDouble(newSize) / Convert.ToDouble(originalSize))) * 100.0d);

        protected string ReadableFileSize(double size, int unit = 0)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

            while (size >= 1024)
            {
                size /= 1024;
                ++unit;
            }

            return string.Format("{0:0.0#} {1}", size, units[unit]);
        }


        /// <summary>
        /// normalizes a string found in an HTML/RSS/ATOM element
        /// - HTML decodes it
        /// - strips any remaining HTML tags
        /// - converts \n, \t, and \r tabs to space
        /// - collapses runs of whitespace into a single space
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        protected string Normalize(string s)
        {
            if (s == null)
            {
                return "";
            }

            //decode
            s = WebUtility.HtmlDecode(s);
            //strip tags
            s = Regex.Replace(s, @"<[^>]*>", "");
            if (s.Contains('\t'))
            {
                s.Replace('\t', ' ');
            }
            return RemoveNewlines(s);
        }

        protected string RemoveNewlines(string text)
        {
            if (text.Length > 0 && (text.Contains('\n') || text.Contains('\r')))
            {
                text = text.Replace('\r', ' ');
                text = text.Replace('\n', ' ');
                text = whitespace.Replace(text, " ");
            }
            return text;
        }

        /// <summary>
        /// Attempts to create an absolute URL from a string
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        protected static Uri CreateUrl(string s)
        {
            try
            {
                Uri u = new Uri(s);
                if (!u.IsAbsoluteUri || u.Scheme == "file")
                {
                    return null;
                }
                return u;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

