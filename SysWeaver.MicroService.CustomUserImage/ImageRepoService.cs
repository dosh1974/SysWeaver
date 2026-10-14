using ImageMagick;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver.MicroService
{
    public sealed class ImageRepoService : IFileRepoContainer
    {
        public ImageRepoService(ImageRepoParams p)
        {
            Repos = p?.Repos;
            foreach (var r in (p?.Repos).Nullable())
            {
                if (r == null)
                    continue;
                HashSet<String> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var x in r.Sizes.Nullable())
                {
                    var name = x.Name;
                    if (String.IsNullOrEmpty(name))
                        throw new Exception("Name can't be empty!");
                    if (name.IndexOf("{0}") < 0)
                        throw new Exception("Name must contain a \"{0}\" variable, not found in \"" + name + "\"!");
                    if (name.FastEquals("{0}"))
                        throw new Exception("Name may not be the same as the original (\"{0}\")!");
                    if (!seen.Add(name.FastToLower()))
                        throw new Exception("Can't have the same name for different sizes, found \"" + name + "\" more than once!");
                }
            }
        }

        public IFileRepo[] Repos { get; init; }

        internal static readonly IReadOnlyDictionary<String, ImageExt> SaveFormats = new Dictionary<String, ImageExt>(StringComparer.Ordinal)
        {
            { ".png", new ImageExt(MagickFormat.Png, ".png") },
            { ".jpg", new ImageExt(MagickFormat.Jpeg, ".jpg") },
        }.Freeze();

        static readonly IReadOnlySet<String> SupportedSourceFiles = ReadOnlyData.Set(StringComparer.Ordinal,
            ".png",
            ".tif",
            ".tiff",
            ".jpg",
            ".jpeg",
            ".webp",
            ".avif"
        );

        internal static readonly String[] SupportedSourceExts = SupportedSourceFiles.ToArray();

        /// <summary>
        /// Check if the format of an uploaded source image (as reported by ImageMagick) is supported
        /// </summary>
        /// <param name="format">The format of the image data</param>
        /// <returns>True if the format is supported</returns>
        internal static bool IsSupportedSourceFormat(MagickFormat format)
        {
            switch (format)
            {
                case MagickFormat.Png:
                case MagickFormat.Jpeg:
                //  ImageMagick reports TIFF data as Tiff (Tiff64 for BigTIFF), never as Tif
                case MagickFormat.Tif:
                case MagickFormat.Tiff:
                case MagickFormat.Tiff64:
                case MagickFormat.WebP:
                case MagickFormat.Avif:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Decode an uploaded source image, the data is only decoded once (the format is read from the decoded image).
        /// Data that fails to decode is pinged (<see cref="MagickImageInfo"/>), so the results are the same as testing the format of the image info before decoding:
        /// an unsupported format is refused (ex: a truncated gif), else the exception of the decode (or the ping) is thrown.
        /// </summary>
        /// <param name="data">The image data</param>
        /// <returns>The decoded image, null if the data isn't in a supported format (<see cref="IsSupportedSourceFormat(MagickFormat)"/>)</returns>
        internal static MagickImage ReadSupportedSource(ReadOnlyMemory<Byte> data)
        {
            MagickImage img;
            try
            {
                img = new MagickImage(data.Span);
            }
            catch
            {
                if (!IsSupportedSourceFormat(new MagickImageInfo(data.Span).Format))
                    return null;
                throw;
            }
            if (IsSupportedSourceFormat(img.Format))
                return img;
            img.Dispose();
            return null;
        }

    }
}
