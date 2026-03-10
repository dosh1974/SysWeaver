using ImageMagick;
using System;
using System.IO;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    public sealed partial class MediaTransformerService
    {
        sealed class ImageHandler : IHandler
        {

            public ImageHandler(params ImageFormat[] formats)
            {
                Formats = formats;
            }

            readonly ImageFormat[] Formats;

            async ValueTask<FileHttpRequestHandler> WriteCompressed(MediaTransformerService service, String baseName, ReadOnlyMemory<Byte> imageData, ImageFormat format)
            {
                String name = String.Concat(baseName, format.Extension, service.CompExt);
                var compType = service.CompType;
                var mime = format.Mime;
                var fi = new FileInfo(name);
                if (fi.Exists)
                    return fi.Length == 0 ? null : new FileHttpRequestHandler(mime, fi, Options, true, compType);

                var tempName = name + ".tmp";
                try
                {
                    await compType.GetCompressed(imageData.Span, CompEncoderLevels.Best).WriteToFileAsync(tempName).ConfigureAwait(false);
                    await PathExt.TryMoveFileAsync(tempName, name).ConfigureAwait(false);
                    fi = new FileInfo(name);
                    if (fi.Exists && (fi.Length > 0))
                        return new FileHttpRequestHandler(mime, fi, Options, true, compType);
                    return null;

                }
                finally
                {
                    await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
                }
            }

            async ValueTask<FileHttpRequestHandler> BuildOne(MediaTransformerService service, String baseName, MagickImage image, ImageFormat format)
            {
                var mime = format.Mime;
                var name = baseName + format.Extension;
                var compExt = service.CompExt;
                ICompType compType = null;
                if (mime.Item2 || format.ForceCompress)
                {
                    name += compExt;
                    compType = service.CompType;
                }
                var fi = new FileInfo(name);
                if (fi.Exists)
                    return fi.Length == 0 ? null :  new FileHttpRequestHandler(mime, fi, Options, true, compType);
                if (!format.HaveAlpha)
                {
                    if (image.HasAlpha)
                    {
                        await File.WriteAllBytesAsync(name, Array.Empty<Byte>()).ConfigureAwait(false);
                        return null;
                    }
                }
                var d = format.Def;
                image.Quality = format.Quality;
                var tempName = name + ".tmp";
                try
                {
                    if (compType != null)
                    {
                        using var s = new ArrayPoolStream();
                        if (d != null)
                        {
                            await image.WriteAsync(s, d).ConfigureAwait(false);
                        }
                        else
                        {
                            await image.WriteAsync(s, format.Format).ConfigureAwait(false);
                        }
                        using var mem = s.GetMemory();
                        await compType.GetCompressed(mem.Memory.Span, CompEncoderLevels.Best).WriteToFileAsync(tempName).ConfigureAwait(false);
                    }
                    else
                    {
                        if (d != null)
                        {
                            await image.WriteAsync(tempName, d).ConfigureAwait(false);
                        }
                        else
                        {
                            await image.WriteAsync(tempName, format.Format).ConfigureAwait(false);
                        }
                    }
                    await PathExt.TryMoveFileAsync(tempName, name).ConfigureAwait(false);
                    fi = new FileInfo(name);
                    if (fi.Exists && (fi.Length > 0))
                        return new FileHttpRequestHandler(mime, fi, Options, true, compType);
                    return null;
                }
                finally
                {
                    await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
                }
            }


            public async ValueTask<FileHttpRequestHandler[]> Build(MediaTransformerService service, string baseName, string inputMime, ReadOnlyMemory<byte> inputData)
            {
                await PathExt.EnsureCanWriteFileAsync(baseName).ConfigureAwait(false);
                var formats = Formats;
                var fl = formats.Length;
                FileHttpRequestHandler[] files = new FileHttpRequestHandler[fl];
                using (var image = new MagickImage(inputData.Span))
                {
                    for (int i = 0; i < fl; ++i)
                    {
                        var fmt = formats[i];
                        if (fmt.OnlyCompress)
                            files[i] = await WriteCompressed(service, baseName, inputData, fmt).ConfigureAwait(false);
                        else
                            files[i] = await BuildOne(service, baseName, image, fmt).ConfigureAwait(false);
                    }
                }
                return GetValidSorted(files);
            }


            public CacheEntry Validate(MediaTransformerService service, string baseName)
            {
                var formats = Formats;
                var fl = formats.Length;
                FileHttpRequestHandler[] files = new FileHttpRequestHandler[fl];
                var compType = service.CompType;
                var compExt = service.CompExt;
                for (int i = 0; i < fl; ++ i)
                {
                    var format = formats[i];
                    var name = baseName + format.Extension;
                    if (format.OnlyCompress)
                    {
                        name += compExt;
                        var fic = new FileInfo(name);
                        if (!fic.Exists)
                            return null;
                        if (fic.Length == 0)
                            continue;
                        files[i] = new FileHttpRequestHandler(format.Mime, fic, Options, true, compType);
                        continue;
                    }
                    var mime = format.Mime;
                    if (mime.Item2)
                        name += compExt;
                    var fi = new FileInfo(name);
                    if (!fi.Exists)
                        return null;
                    if (fi.Length == 0)
                        continue;
                    files[i] = new FileHttpRequestHandler(format.Mime, fi, Options, true, mime.Item2 ? compType : null);
                }
                return new CacheEntry
                {
                    Files = GetValidSorted(files),
                };
            }
        }



    }

}
