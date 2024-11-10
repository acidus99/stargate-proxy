using System;

using ImageMagick;
using RocketForce;

namespace Stargate.Transformers
{
	public class ImageTransformer : ITransformer
	{
        /// <summary>
        /// if an image is larger than this in width or height, it will be resized, preserving aspect ratio
        /// </summary>
        public uint MaxDimension { get; set; } = 800;

        public bool CanTransform(string mimeType)
            => mimeType.StartsWith("image/");

        public SourceResponse Transform(Request request, SourceResponse response)
        {
            MediaContent media = TransformImage(response);

            response.Meta = media.MimeType;
            response.Body = new MemoryStream(media.Data);
            return response;
        }

        private MediaContent TransformImage(SourceResponse response)
        {
            using (var image = new MagickImage(response.Body))
            {

                //if it's not raster, make it raster
                if (image.Format == MagickFormat.Svg)
                {
                    image.Format = MagickFormat.Png;
                }

                //force a white background for transparent images
                if (!image.IsOpaque)
                {
                    //add a white background to transparent images to
                    //make them visible on clients with a dark theme
                    image.BackgroundColor = new MagickColor("white");
                    image.Alpha(AlphaOption.Remove);
                }

                //change the format if it's not something that most clients will natively render
                if (!IsCommonFormat(image.Format))
                {
                    image.Format = MagickFormat.Jpeg;
                    //explicitly set quality
                    image.Quality = 75;
                }

                if (image.Width > MaxDimension || image.Height > MaxDimension)
                {
                    var geo = new MagickGeometry(MaxDimension, MaxDimension);
                    image.Resize(geo);
                }

                //strip out any meta data
                image.Strip();

                // Retrieve the MagickFormatInfo for the image's format
                var formatInfo = MagickFormatInfo.Create(image.Format);
                // Get the MIME type as a string
                string mimeType = formatInfo?.MimeType ?? "image.jpeg";

                return new MediaContent
                {
                    Data = image.ToByteArray(),
                    MimeType = mimeType
                };
            }
        }

        private bool IsCommonFormat(MagickFormat format)
            => format == MagickFormat.Png ||
                format == MagickFormat.Jpeg ||
                format == MagickFormat.Gif;
        public class MediaContent
        {
            public byte[] Data { get; set; }
            public string MimeType { get; set; }
        }

}
}

