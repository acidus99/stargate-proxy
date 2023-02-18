using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using AngleSharp.Io;
using CodeHollow.FeedReader;

using HtmlToGmi;
using HtmlToGmi.Models;

namespace Stargate.Transformers
{
    public class HtmlTransformer : AbstractTextTransformer
    {
        public override bool CanTransform(string mimeType)
            => mimeType.StartsWith("text/html");

        public override SourceResponse Transform(Request request, SourceResponse response)
        {
            var html = ReadAllText(response);

            HtmlConverter converter = new HtmlConverter()
            {
                AllowDuplicateLinks = true,
                ShouldRenderHyperlinks = true
            };
            var content = converter.Convert(request.Url.AbsoluteUri, html);

            //reset my mime and body
            response.Meta = "text/gemini";
            response.Body = RenderToStream(content, html.Length);

            return response;
        }

        private MemoryStream RenderToStream(ConvertedContent content, int htmlLength)
        {
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
                    if (content.MetaData.OpenGraphImage != null)
                    {
                        fout.WriteLine($"=> {content.MetaData.OpenGraphImage} Featured Imaged");
                    }
                    if (content.MetaData.OpenGraphType == "article")
                    {
                        fout.WriteLine($"=> gemini://gemi.dev/cgi-bin/waffle.cgi/article?{WebUtility.UrlEncode(content.Url.AbsoluteUri)} Article detected. View on 🧇 NewsWaffle?");
                    }
                    fout.WriteLine();

                    fout.Write(content.Gemtext);
                    AppendFooter(fout, htmlLength, content.Gemtext.Length);
                }
                return new MemoryStream(newBody.GetBuffer());
            }
        }
    }
}
