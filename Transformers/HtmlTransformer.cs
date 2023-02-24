using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;

using HtmlToGmi;
using HtmlToGmi.Encodings;
using HtmlToGmi.Models;


namespace Stargate.Transformers
{

    public class HtmlTransformer : AbstractTextTransformer
    {

        public HtmlTransformer()
        {
            //Ensure that extended codepages are supported
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public override bool CanTransform(string mimeType)
            => mimeType.StartsWith("text/html");

        public override SourceResponse Transform(Request request, SourceResponse response)
        {

            var responseBytes = ReadAllBytes(response);


            HtmlConverter converter = new HtmlConverter()
            {
                AllowDuplicateLinks = true,
                ShouldRenderHyperlinks = true
            };

            ConvertedContent content = null;

            //Do we have a charset in the Content-Type header? If so, that overrides all other charsets
            if (HasCharset(response.SourceContentType))
            {
                string html = "";
                try
                {
                    html = Encoding.GetEncoding(response.SourceContentType.CharSet).GetString(responseBytes);
                }
                catch (ArgumentException)
                {
                    throw new TransformationException($"unknown charset '{response.SourceContentType.CharSet}'");
                }
                content = converter.Convert(request.Url, html);
            }
            else
            {
                //check the HTML and use any charset there
                HtmlDecoder htmlDecoder = new HtmlDecoder();
                try
                {
                    htmlDecoder.Decode(responseBytes);
                }
                catch (ArgumentException)
                {
                    throw new TransformationException($"unknown charset '{htmlDecoder.SpecifiedCharset}'");
                }
                content = converter.Convert(request.Url, htmlDecoder.Document);
            }

            //whatever the charset was, it is now utf-8
            response.Meta = $"text/gemini;charset=utf-8";
            response.Body = RenderToStream(content, responseBytes.Length);

            return response;
        }

        private bool HasCharset(MediaTypeHeaderValue contentType)
            => !string.IsNullOrEmpty(contentType.CharSet);

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
