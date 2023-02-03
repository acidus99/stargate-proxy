using System;

using ImageMagick;

namespace Stargate.Transformers
{
	public class ImageTransformer : ITransformer
	{
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

                //if its not raster, make it raster
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

                //change the format if its not something that most clients will natively render
                //if (!IsCommonFormat(image.Format))
                //{
                //    image.Format = MagickFormat.Jpeg;
                //    //explicitly set quality
                //    image.Quality = 75;
                //}

                //strip out any meta data
                image.Strip();

                //TODO: change size here

                return new MediaContent
                {
                    Data = image.ToByteArray(),
                    MimeType = image.FormatInfo.MimeType
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

