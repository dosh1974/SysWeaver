using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{

    sealed class ImageHandler : ICachedTransformer
    {

        public String Info { get; init; }

        public override string ToString() => Info;

        public CachedTransformerBuildStrategies BuildStrategy { get; init; }

        public ImageHandler(CachedTransformerBuildStrategies buildStrategy, params ImageFormat[] formats)
        {
            BuildStrategy = buildStrategy;
            Formats = formats;
            Info = String.Join(", ", formats.Select(x => x.Info));
        }


        readonly ImageFormat[] Formats;

        async ValueTask<FileHttpRequestHandler> WriteCompressed(CachedTransformer service, String baseName, ReadOnlyMemory<Byte> imageData, ImageFormat format)
        {
            String name = String.Concat(baseName, format.Extension, service.CompExt);
            var compType = service.CompType;
            var mime = format.Mime;
            var fi = new FileInfo(name);
            if (fi.Exists)
                return fi.Length == 0 ? null : new FileHttpRequestHandler(mime, fi, CachedTransformer.Options, true, compType, true);

            var tempName = name + CachedTransformer.TempExt;
            try
            {
                await compType.GetCompressed(imageData.Span, CompEncoderLevels.Best).WriteToFileAsync(tempName).ConfigureAwait(false);
                await PathExt.TryMoveFileAsync(tempName, name).ConfigureAwait(false);
                fi = new FileInfo(name);
                if (fi.Exists && (fi.Length > 0))
                    return new FileHttpRequestHandler(mime, fi, CachedTransformer.Options, true, compType, true);
                return null;

            }
            finally
            {
                await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
            }
        }

        async ValueTask<FileHttpRequestHandler> BuildOne(CachedTransformer service, String baseName, MagickImage image, ImageFormat format)
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
                return fi.Length == 0 ? null : new FileHttpRequestHandler(mime, fi, CachedTransformer.Options, true, compType, true);
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
            var tempName = name + CachedTransformer.TempExt;
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
                fi = new FileInfo(tempName);
                await PathExt.TryMoveFileAsync(tempName, name).ConfigureAwait(false);
                fi = new FileInfo(name);
                if (fi.Exists && (fi.Length > 0))
                    return new FileHttpRequestHandler(mime, fi, CachedTransformer.Options, true, compType, true);
                return null;
            }
            finally
            {
                await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
            }
        }


        public async ValueTask<FileHttpRequestHandler[]> Build(CachedTransformer service, string baseName, string inputMime, ReadOnlyMemory<byte> inputData, String inputExt, bool isSupported, ICompDecoder decoder)
        {
            var formats = Formats;
            var fl = formats.Length;
            List<FileHttpRequestHandler> files = new(fl + 1);
            var orgLen = inputData.Length;
            if (decoder != null)
                inputData = decoder.GetDecompressed(inputData.Span);
            using (var image = new MagickImage(inputData.Span))
            {
                for (int i = 0; i < fl; ++i)
                {
                    var fmt = formats[i];
                    FileHttpRequestHandler file;
                    if (fmt.OnlyCompress)
                        file = await WriteCompressed(service, baseName, inputData, fmt).ConfigureAwait(false);
                    else
                        file = await BuildOne(service, baseName, image, fmt).ConfigureAwait(false);
                    if (file != null)
                        files.Add(file);
                }
            }
            if (isSupported)
            {
                await CachedTransformer.SaveOrg(baseName, orgLen).ConfigureAwait(false);
                files.Add(null);
            }
            return CachedTransformer.GetValidSorted(files, orgLen);
        }


        public CachedTransformerEntry Validate(CachedTransformer service, string baseName, String inputMime, bool isSupported)
        {
            var formats = Formats;
            var fl = formats.Length;
            List<FileHttpRequestHandler> files = new(fl + 1);
            var compType = service.CompType;
            var compExt = service.CompExt;
            for (int i = 0; i < fl; ++i)
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
                    files.Add(new FileHttpRequestHandler(format.Mime, fic, CachedTransformer.Options, true, compType, true));
                    continue;
                }
                var mime = format.Mime;
                if (mime.Item2 || format.ForceCompress)
                    name += compExt;
                var fi = new FileInfo(name);
                if (!fi.Exists)
                    return null;
                if (fi.Length == 0)
                    continue;
                files.Add(new FileHttpRequestHandler(format.Mime, fi, CachedTransformer.Options, true, mime.Item2 ? compType : null, true));
            }
            long orgSize = 0;
            if (isSupported)
            {
                orgSize = CachedTransformer.ReadOrg(baseName);
                if (orgSize < 0)
                    return null;
                files.Add(null);
            }
            return new CachedTransformerEntry
            {
                Completed = true,
                OrgSize = orgSize,
                Files = CachedTransformer.GetValidSorted(files, orgSize),
            };
        }
    }


}
