using ImageMagick;
using ImageMagick.Formats;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{

    public sealed class ImageTransformer 
    {

        public ImageTransformer(CachedTransformer t, ImageTransformerParams p = null)
        {
            if (t == null)
                throw new ArgumentException(String.Concat("A required instance of type \"", typeof(CachedTransformer).FullName, "\" was not supplied!"), nameof(t));
            p = p ?? new ImageTransformerParams();
            if (p.Optimize)
            {
                t.Add("image/png",
                        new ImageHandler(
                            CachedTransformerBuildStrategies.AlwaysDefer,
                            [
                                new ImageFormat(MagickFormat.Avif, ".avif", 90),
                                new ImageFormat(MagickFormat.WebP, ".webp", 95),
                                new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                            ]));
                t.Add("image/bmp", 
                        new ImageHandler(
                                CachedTransformerBuildStrategies.AlwaysDefer, 
                                [
                                    new ImageFormat(MagickFormat.Avif, ".avif", 90),
                                    new ImageFormat(MagickFormat.WebP, ".webp", 95),
                                    new ImageFormat(MagickFormat.Png, ".png"),
                                    new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                                ]));
                t.Add("image/tiff", 
                    new ImageHandler(
                        CachedTransformerBuildStrategies.AlwaysDefer, 
                        [
                            new ImageFormat(MagickFormat.Avif, ".avif", 90),
                            new ImageFormat(MagickFormat.WebP, ".webp", 95),
                            new ImageFormat(MagickFormat.Png, ".png"),
                            new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                        ]));
                t.Add("image/jpeg", 
                    new ImageHandler(
                        CachedTransformerBuildStrategies.AlwaysDefer, 
                        [
                            new ImageFormat(".jpg"),
                            new ImageFormat(MagickFormat.Avif, ".avif", 50),
                            new ImageFormat(MagickFormat.WebP, ".webp", 60),
                        ]));
                t.Add("image/pjpeg",
                    new ImageHandler(
                        CachedTransformerBuildStrategies.AlwaysDefer,
                        [
                            new ImageFormat(".jfif"),
                            new ImageFormat(MagickFormat.Avif, ".avif", 95),
                            new ImageFormat(MagickFormat.WebP, ".webp", 100),
                        ]));
            }
            if (p.SupportNew)
            {
                t.Add("image/webp", 
                    new ImageHandler(
                        CachedTransformerBuildStrategies.CheckAccept, 
                        [
                            new ImageFormat(MagickFormat.Avif, ".avif", 90),
                            new ImageFormat(MagickFormat.Png, ".png"),
                            new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                        ]));
                t.Add("image/avif", 
                    new ImageHandler(
                        CachedTransformerBuildStrategies.CheckAccept, 
                        [
                            new ImageFormat(MagickFormat.WebP, ".webp", 95),
                            new ImageFormat(MagickFormat.Png, ".png"),
                            new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                        ]));
            }
            var s = p.Support;
            if (s != null)
            {
                foreach (var x in s)
                {
                    t.Add(x, new ImageHandler(
                        CachedTransformerBuildStrategies.AlwaysDirect, 
                        [
                            new ImageFormat(MagickFormat.Avif, ".avif", 90),
                            new ImageFormat(MagickFormat.WebP, ".webp", 95),
                            new ImageFormat(MagickFormat.Png, ".png"),
                            new ImageFormat(MagickFormat.Jpeg, ".jpg", 95, null, false, true),
                        ]));
                }
            }
        }

    }

}
