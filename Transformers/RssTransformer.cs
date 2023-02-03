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
    public class RssTransformer : ITransformer
    {
        private static readonly Regex whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public bool CanTransform(string mimeType)
            => mimeType.StartsWith("application/rss+xml");

        public SourceResponse Transform(Request request, SourceResponse response)
        {
            var sr = new StreamReader(response.Body);
            var xml = sr.ReadToEnd();
            sr.Close();

            var feed = FeedReader.ReadFromString(xml);
            FeedSummary metaData = new FeedSummary
            {
                Description = Normalize(feed.Description),
                FeaturedImage = Create(feed.ImageUrl),
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

        private void AppendFooter(TextWriter body, int htmlSize, int gmiSize)
        {
            body.WriteLine();
            body.WriteLine();
            body.WriteLine("------");
            body.WriteLine("Teleported and converted via Stargate 💫🚪");
            body.WriteLine($"Size: {ReadableFileSize(gmiSize)}. {Savings(gmiSize, htmlSize)} smaller than original: {ReadableFileSize(htmlSize)} 🤮");
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

        /// <summary>
        /// normalizes a string found in HTML
        /// - HTML decodes it
        /// - strips any remaining HTML tags
        /// - converts \n, \t, and \r tabs to space
        /// - collapses runs of whitespace into a single space
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static string Normalize(string s)
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

        private static string RemoveNewlines(string text)
        {
            if (text.Length > 0 && (text.Contains('\n') || text.Contains('\r')))
            {
                text = text.Replace('\r', ' ');
                text = text.Replace('\n', ' ');
                text = whitespace.Replace(text, " ");
            }
            return text;
        }

        public static Uri Create(string s)
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
            catch (Exception ex)
            {
                return null;
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

