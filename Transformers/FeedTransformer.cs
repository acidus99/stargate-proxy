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

        private FeedSummary ParseFeed(string xml)
        {
            var feed = FeedReader.ReadFromString(xml);
            FeedSummary ret = new FeedSummary
            {
                Description = Normalize(feed.Description),
                FeaturedImage = CreateUrl(feed.ImageUrl),
                OriginalSize = xml.Length,
                Title = Normalize(feed.Title),
                SiteName = Normalize(feed.Copyright)
            };

            ret.Items = feed.Items
                .Select(x => Convert(x))
                .Where(x => (x.Url != null))
                .ToList();

            return ret;
        }

        private MemoryStream RenderToStream(FeedSummary feed)
        {
            using (var newBody = new MemoryStream(feed.OriginalSize))
            {
                using (var fout = new StreamWriter(newBody))
                {
                    fout.WriteLine($"# {feed.SiteName}");
                    fout.WriteLine("This RSS/Atom feed has been automatically converted.");

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
                        if(item.HasTimeAgo)
                        {
                            fout.WriteLine("Published: " + item.TimeAgo);
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

        private class FeedSummary
        {
            public string Description;
            public Uri FeaturedImage;
            public int OriginalSize;
            public string Title;
            public string SiteName;

            public List<FeedLink> Items;
        }

        private class FeedLink
        {
            public string Title { get; set; }

            public string Description { get; set; }

            public Uri Url { get; set; }

            public string TimeAgo { get; set; }

            public bool HasTimeAgo => !string.IsNullOrEmpty(TimeAgo);

            public FeedItemEnclosure Enclosure { get; set; }

        }

        private FeedLink Convert(FeedItem item)
            => new FeedLink
            {
                Title = Normalize(item.Title),
                Description = Normalize(item.Description),
                Url = CreateUrl(item.Link),
                TimeAgo = FormatTimeAgo(item.PublishingDate),
                Enclosure = GetEnclosure(item)
            };

        private FeedItemEnclosure GetEnclosure(FeedItem item)
            => (item.SpecificItem is Rss20FeedItem) ?
                ((Rss20FeedItem)item.SpecificItem).Enclosure :
                null;

        private string FormatTimeAgo(DateTime? feedDateTime)
        {
            if(!feedDateTime.HasValue)
            {
                return "";
            }

            var s = DateTime.Now.Subtract(feedDateTime.Value);
            int dayDiff = (int)s.TotalDays;

            int secDiff = (int)s.TotalSeconds;

            if (dayDiff == 0)
            {
                if (secDiff < 60)
                {
                    return "just now";
                }
                if (secDiff < 120)
                {
                    return "1 minute ago";
                }
                if (secDiff < 3600)
                {
                    return $"{Math.Floor((double)secDiff / 60)} minutes ago";
                }
                if (secDiff < 7200)
                {
                    return "1 hour ago";
                }
                if (secDiff < 86400)
                {
                    return $"{Math.Floor((double)secDiff / 3600)} hours ago";
                }
            }
            if (dayDiff == 1)
            {
                return "yesterday";
            }
            return string.Format("{0} days ago", dayDiff);
        }
    }
}
