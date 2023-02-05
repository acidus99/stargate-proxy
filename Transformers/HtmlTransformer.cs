using System;
using System.IO;
using System.IO.Compression;
using HtmlToGmi;

namespace Stargate.Transformers
{
    public class HtmlTransformer : ITransformer
    {
        public bool CanTransform(string mimeType)
            => mimeType.StartsWith("text/html");

        public SourceResponse Transform(Request request, SourceResponse response)
        {
            var sr = new StreamReader(response.Body);
            var html = sr.ReadToEnd();
            sr.Close();

            HtmlConverter converter = new HtmlConverter()
            {
                AllowDuplicateLinks = true,
                ShouldRenderHyperlinks = true
            };
            var content = converter.Convert(request.Url.AbsoluteUri, html);


            //reset my mime and body
            response.Meta = "text/gemini";

            using (var newBody = new MemoryStream(content.Gemtext.Length + 200))
            {
                using (var fout = new StreamWriter(newBody))
                {
                    if (content.MetaData.HasTitle)
                    {
                        fout.WriteLine($"# {content.MetaData.Title}");
                    }
                    if (content.MetaData.FeedUrl != null)
                    {
                        fout.WriteLine($"=> {content.MetaData.FeedUrl} RSS/Atom feed detected");
                    }
                    fout.WriteLine();

                    fout.Write(content.Gemtext);
                    AppendFooter(fout, html.Length, content.Gemtext.Length);
                }
                response.Body = new MemoryStream(newBody.GetBuffer());
            }

            return response;
        }

        private void AppendFooter(TextWriter body, int htmlSize, int gmiSize)
        {
            body.WriteLine();
            body.WriteLine();
            body.WriteLine("------");
            body.WriteLine("Teleported and converted via Stargate 💫🚪");
            var emoji = (htmlSize > gmiSize) ? "🤮" : "😳🤬";
            body.WriteLine($"Size: {ReadableFileSize(gmiSize)}. {Savings(gmiSize, htmlSize)} smaller than original: {ReadableFileSize(htmlSize)} {emoji}");
            body.WriteLine("=> mailto:acidus@gemi.dev Made with ❤️ by Acidus");
        }

        private string Savings(int newSize, int originalSize)
            => string.Format("{0:0.00}%", (1.0d - (Convert.ToDouble(newSize) / Convert.ToDouble(originalSize))) * 100.0d);

        private string ReadableFileSize(double size, int unit = 0)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

            while (size >= 1024)
            {
                size /= 1024;
                ++unit;
            }

            return string.Format("{0:0.0#} {1}", size, units[unit]);
        }
    }
}

