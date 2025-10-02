using HtmlToGmi.NewsFeeds;
using RocketForce;

namespace Stargate.Transformers;

public class FeedTransformer : AbstractTextTransformer
{
    private const int MaxDescriptionLength = 300;
    
    public override bool CanTransform(string mimeType)
    {
        return mimeType.StartsWith("application/rss+xml") ||
               mimeType.StartsWith("application/atom+xml") ||
               //some people don't have Mimetypes setup properly, so if its XML, say we can handle it
               //and then test the body. If its not really a RSS/Atom feed we will pass the original body on
               mimeType.StartsWith("text/xml");
    }

    public override SourceResponse Transform(Request request, SourceResponse response)
    {
        var xml = ReadAllText(response);

        if (!IsReallyFeed(xml))
        {
            //reset body
            response.Body = RenderToStream(xml);
            return response;
        }

        var feed = ParseFeed(xml);

        //reset my mime and body
        response.Meta = "text/gemini";
        response.Body = RenderToStream(feed);

        return response;
    }

    private static bool IsReallyFeed(string xml)
    {
        var prefix = xml.Substring(0, Math.Min(xml.Length, 250));
        return prefix.Contains("<rss") || prefix.Contains("<feed");
    }

    private static Feed ParseFeed(string xml)
    {
        var converter = new FeedConverter();
        return converter.Convert(xml);
    }

    private MemoryStream RenderToStream(Feed feed)
    {
        using (var newBody = new MemoryStream(feed.OriginalSize))
        {
            using (var fout = new StreamWriter(newBody))
            {
                fout.WriteLine($"# {feed.SiteName}");
                fout.WriteLine("This RSS/Atom feed has been automatically converted by Stargate 💫🚪.");

                fout.WriteLine($"## {feed.Title}");
                if (feed.Items.FirstOrDefault()?.Enclosure != null) fout.WriteLine("🎵 Audio Feed Detected!");
                if (feed.FeaturedImage != null) fout.WriteLine($"=> {feed.FeaturedImage.AbsoluteUri} Featured Image");
                if (feed.Description.Length > 0) fout.WriteLine($">{feed.Description}");
                fout.WriteLine();
                var counter = 0;
                foreach (var item in feed.Items)
                {
                    counter++;
                    fout.WriteLine($"## {item.Title}");
                    if (item.Published.HasValue) fout.WriteLine("Published: " + item.GetTimeAgo(DateTime.Now));
                    fout.WriteLine($"> {SmartTruncate(item.Description, MaxDescriptionLength)}");
                    if (item.Enclosure != null)
                    {
                        fout.Write($"=> {item.Enclosure.Url} 🎵 Audio File ({item.Enclosure.MediaType})");
                        if (item.Enclosure.Length.HasValue)
                            fout.Write($" {ReadableFileSize(item.Enclosure.Length.Value)}");
                        fout.WriteLine();
                    }
                    else
                    {
                        fout.WriteLine($"=> {item.Url} Read Entry");
                    }
                }

                fout.Flush();
                AppendFooter(fout, feed.OriginalSize, (int)fout.BaseStream.Position);
            }

            return new MemoryStream(newBody.ToArray());
        }
    }

    private MemoryStream RenderToStream(string xml)
    {
        using (var newBody = new MemoryStream(xml.Length))
        {
            using (var fout = new StreamWriter(newBody))
            {
                fout.Write(xml);
            }

            return new MemoryStream(newBody.ToArray());
        }
    }
    
    /// <summary>
    /// Truncates a string on whitespace
    /// </summary>
    /// <param name="text"></param>
    /// <param name="maxLength"></param>
    /// <returns></returns>
    private string SmartTruncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        // Work backwards from maxLength to find whitespace
        int breakPoint = maxLength;
        while (breakPoint > 0 && !char.IsWhiteSpace(text[breakPoint]))
        {
            breakPoint--;
        }

        // If no whitespace was found, just hard cut at maxLength
        if (breakPoint == 0)
            breakPoint = maxLength;

        string truncated = text[..breakPoint].TrimEnd();

        return truncated + "…"; 
    }
}