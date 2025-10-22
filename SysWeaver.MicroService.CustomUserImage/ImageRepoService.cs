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
            foreach (var r in p?.Repos.Nullable())
            {
                HashSet<String> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var x in r?.Sizes.Nullable())
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

  
    }
}
