using System;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using AngleSharp.Io;
using System.Reflection.Metadata;
using System.Diagnostics.Metrics;

namespace Stargate.Transformers
{
    public class RssTransformer : AbstractTextTransformer
    {
        public override bool CanTransform(string mimeType)
            => mimeType.StartsWith("application/rss+xml");

        public override SourceResponse Transform(Request request, SourceResponse response)
        {
            var xml = ReadAllText(response);

            var feed = FeedReader.ReadFromString(xml);
            FeedSummary metaData = new FeedSummary
            {
                Description = Normalize(feed.Description),
                FeaturedImage = CreateUrl(feed.ImageUrl),
                OriginalSize = xml.Length,
                Title = Normalize(feed.Title),
                SiteName = Normalize(feed.Copyright)
            };
            
            //reset my mime and body
            response.Meta = "text/gemini";
            response.Body = RenderToStream(metaData, feed);

            return response;
        }

        private MemoryStream RenderToStream(FeedSummary metaData, Feed feed)
        {
            using (var newBody = new MemoryStream(metaData.OriginalSize))
            {
                using (var fout = new StreamWriter(newBody))
                {
                    fout.WriteLine($"# {metaData.SiteName}");
                    fout.WriteLine("This RSS feed has been automatically converted.");

                    fout.WriteLine($"## {metaData.Title}");
                    if (metaData.FeaturedImage != null)
                    {
                        fout.WriteLine($"=> {metaData.FeaturedImage.AbsoluteUri} Featured Image");
                    }
                    if (metaData.Description.Length > 0)
                    {
                        fout.WriteLine($">{metaData.Description}");
                    }
                    fout.WriteLine();
                    int counter = 0;
                    foreach(var item in feed.Items)
                    {
                        counter++;
                        fout.WriteLine($"## {item.Title}");
                        if(!string.IsNullOrEmpty(item.PublishingDateString))
                        {
                            fout.WriteLine("Published: " + item.PublishingDateString);
                        }
                        fout.WriteLine($"> {Normalize(item.Description)}");
                        fout.WriteLine($"=> {item.Link} Read Entry");
                    }
                    fout.Flush();

                    AppendFooter(fout, metaData.OriginalSize, (int) fout.BaseStream.Position);
                }
                return new MemoryStream(newBody.GetBuffer());
            }
        }

        private class FeedSummary
        {
            public string Description;
            public Uri FeaturedImage;
            public int OriginalSize;
            public string Title;
            public string SiteName;
        }
    }
}
