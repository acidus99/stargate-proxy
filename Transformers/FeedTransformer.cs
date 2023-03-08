using System;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

using HtmlToGmi.NewsFeeds;
using RocketForce;

namespace Stargate.Transformers
{
    public class FeedTransformer : AbstractTextTransformer
    {
        public override bool CanTransform(string mimeType)
            => mimeType.StartsWith("application/rss+xml") ||
                mimeType.StartsWith("application/atom+xml") ||
                //some people don't have Mimetypes setup properly, so if its XML, say we can handle it
                //and then test the body. If its not really a RSS/Atom feed we will pass the original body on
                mimeType.StartsWith("text/xml");

        public override SourceResponse Transform(Request request, SourceResponse response)
        {
            var xml = ReadAllText(response);

            if(!IsReallyFeed(xml))
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

        private bool IsReallyFeed(string xml)
        {
            var prefix = xml.Substring(0, Math.Min(xml.Length, 250));
            return prefix.Contains("<rss") || prefix.Contains("<feed");
        }

        private Feed ParseFeed(string xml)
        {
            FeedConverter converter = new FeedConverter();
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
                    if (feed.Items.FirstOrDefault()?.Enclosure != null)
                    {
                        fout.WriteLine("🎵 Audio Feed Detected!");
                    }
                    if (feed.FeaturedImage != null)
                    {
                        fout.WriteLine($"=> {feed.FeaturedImage.AbsoluteUri} Featured Image");
                    }
                    if (feed.Description.Length > 0)
                    {
                        fout.WriteLine($">{feed.Description}");
                    }
                    fout.WriteLine();
                    int counter = 0;
                    foreach(var item in feed.Items)
                    {
                        counter++;
                        fout.WriteLine($"## {item.Title}");
                        if(item.Published.HasValue)
                        {
                            fout.WriteLine("Published: " + item.GetTimeAgo(DateTime.Now));
                        }
                        fout.WriteLine($"> {item.Description}");
                        if (item.Enclosure != null)
                        {
                            fout.Write($"=> {item.Enclosure.Url} 🎵 Audio File ({item.Enclosure.MediaType})");
                            if(item.Enclosure.Length.HasValue)
                            {
                                fout.Write($" {ReadableFileSize(item.Enclosure.Length.Value)}");
                            }
                            fout.WriteLine();
                        }
                        else
                        {
                            fout.WriteLine($"=> {item.Url} Read Entry");
                        }

                    }
                    fout.Flush();
                    AppendFooter(fout, feed.OriginalSize, (int) fout.BaseStream.Position);
                }
                return new MemoryStream(newBody.GetBuffer());
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
                return new MemoryStream(newBody.GetBuffer());
            }
        }

        
    }
}
