using System.Net;
using System.Net.Http.Headers;
using System.Text;
using HtmlToGmi;
using HtmlToGmi.Encodings;
using HtmlToGmi.Models;
using RocketForce;
using Stargate.Helpers;

namespace Stargate.Transformers;

public class HtmlTransformer : AbstractTextTransformer
{
    public HtmlTransformer()
    {
        //Ensure that extended codepages are supported
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public override bool CanTransform(string mimeType)
    {
        return mimeType.StartsWith("text/html");
    }

    public override SourceResponse Transform(Request request, SourceResponse response)
    {
        var responseBytes = ReadAllBytes(response);

        var converter = new HtmlConverter
        {
            AllowDuplicateLinks = true,
            ShouldRenderHyperlinks = true
        };

        ConvertedContent content = null;

        string? normalizedCharset = MimeHelper.NormalizeCharset(response.SourceContentType);
        
        if(normalizedCharset != null)
        {
            var html = "";
            try
            {
                html = Encoding.GetEncoding(normalizedCharset).GetString(responseBytes);
            }
            catch (ArgumentException)
            {
                throw new TransformationException($"Unknown/Unsupport charset '{response.SourceContentType.CharSet}' in source HTTP Content-Type.");
            }

            content = converter.Convert(request.Url, html);
        }
        else
        {
            //check the HTML and use any charset there
            var htmlDecoder = new HtmlDecoder();
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
        response.Meta = "text/gemini;charset=utf-8";
        response.Body = RenderToStream(content, responseBytes.Length);

        return response;
    }

    private MemoryStream RenderToStream(ConvertedContent content, int htmlLength)
    {
        using (var newBody = new MemoryStream(content.Gemtext.Length + 200))
        {
            using (var fout = new StreamWriter(newBody))
            {
                if (content.MetaData.HasTitle) fout.WriteLine($"# {content.MetaData.Title}");
                if (content.MetaData.FeedUrl != null)
                    fout.WriteLine($"=> {content.MetaData.FeedUrl} RSS/Atom feed detected");
                if (content.MetaData.OpenGraphImage != null)
                    fout.WriteLine($"=> {content.MetaData.OpenGraphImage} Featured Image");
                if (content.MetaData.OpenGraphType == "article")
                    fout.WriteLine(
                        $"=> gemini://gemi.dev/cgi-bin/waffle.cgi/article?{WebUtility.UrlEncode(content.Url.AbsoluteUri)} Article detected. View on 🧇 NewsWaffle?");
                fout.WriteLine();

                fout.Write(content.Gemtext);
                AppendFooter(fout, htmlLength, content.Gemtext.Length);
            }

            return new MemoryStream(newBody.ToArray());
        }
    }
}