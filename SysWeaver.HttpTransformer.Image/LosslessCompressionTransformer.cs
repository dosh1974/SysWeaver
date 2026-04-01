using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{
    public sealed class LosslessCompressionTransformer : ICachedTransformer
    {
        public LosslessCompressionTransformer(CachedTransformer t, LosslessCompressionTransformerParams p = null)
        {
            if (t == null)
                throw new ArgumentException(String.Concat("A required instance of type \"", typeof(CachedTransformer).FullName, "\" was not supplied!"), nameof(t));
            p = p ?? new LosslessCompressionTransformerParams();
            var tr = this;
            foreach (var x in MimeTypeMap.AllMimeEntries)
            {
                if (!x.Compressed)
                    continue;
                t.Add(x.Mime, tr);
            }
            var f = p.Methods;
            var fl = f.Length;
            List<ICompType> methods = new (fl);
            for (int i = 0; i < fl; ++ i)
            {
                var m = f[i];
                var comp = CompManager.GetFromHttp(m) ?? CompManager.GetFromExt(m);
                if (comp == null)
                {
                    if (p.ThrowOnMissing)
                        throw new Exception("Couldn't find a compression method named \"" + m + "\"");
                    continue;
                }
                methods.Add(comp);
            }
            Methods = methods.ToArray();
            MethodsExtensions = methods.Select(x => "." + (x.FileExtensions.FirstOrDefault() ?? x.HttpCode)).ToArray();
            Info = String.Join(", ", methods.Select(x => x.Name));
            BuildStrategy = p.BuildDirect ? CachedTransformerBuildStrategies.AlwaysDirect: CachedTransformerBuildStrategies.AlwaysDefer;
        }

        readonly ICompType[] Methods;
        readonly String[] MethodsExtensions;

        public string Info { get; init; }

        public CachedTransformerBuildStrategies BuildStrategy { get; init; }

        public CachedTransformerEntry Validate(CachedTransformer service, string baseName, String inputMime, bool isSupported)
        {
            var methods = Methods;
            var exts = MethodsExtensions;
            var l = exts.Length;
            List<FileHttpRequestHandler> files = new (l + 1);
            var mime = Tuple.Create(inputMime, false);
            for (int i = 0; i < l; ++ i)
            {
                var ext = exts[i];
                var n = baseName + ext;
                var fi = new FileInfo(n);
                if (!fi.Exists)
                    return null;
                if (fi.Length <= 0)
                    continue;
                files.Add(new FileHttpRequestHandler(mime, fi, CachedTransformer.Options, true, methods[i], true));
            }
            var orgSize = CachedTransformer.ReadOrg(baseName);
            if (orgSize < 0)
                return null;
            files.Add(null);
            return new CachedTransformerEntry
            {
                Completed = true,
                OrgSize = orgSize,
                Files = CachedTransformer.GetValidSorted(files, orgSize),
            };
        }

        public async ValueTask<FileHttpRequestHandler[]> Build(CachedTransformer service, string baseName, string inputMime, ReadOnlyMemory<byte> inputData, string inputExt, bool isSupported, ICompDecoder decoder)
        {
            var methods = Methods;
            var exts = MethodsExtensions;
            var l = exts.Length;
            var orgLen = inputData.Length;
            if (decoder != null)
                inputData = decoder.GetDecompressed(inputData.Span);
            List<FileHttpRequestHandler> files = new(l + 1);
            var mime = Tuple.Create(inputMime, false);
            for (int i = 0; i < l; ++i)
            {
                var ext = exts[i];
                var compType = methods[i];
                var name = baseName + ext;
                var fi = new FileInfo(name);
                if (fi.Exists)
                {
                    if (fi.Length > 0)
                        files.Add(new FileHttpRequestHandler(mime, fi, CachedTransformer.Options, true, compType, true));
                    continue;
                }
                if (compType.HttpCode.FastEquals(decoder?.HttpCode))
                {
                    await File.WriteAllBytesAsync(name, Array.Empty<Byte>()).ConfigureAwait(false);
                    continue;
                }
                var tempName = name + CachedTransformer.TempExt;
                try
                {
                    await compType.GetCompressed(inputData.Span, CompEncoderLevels.Best).WriteToFileAsync(tempName).ConfigureAwait(false);
                    await PathExt.TryMoveFileAsync(tempName, name).ConfigureAwait(false);
                    fi = new FileInfo(name);
                    if (fi.Exists && (fi.Length > 0))
                        files.Add(new FileHttpRequestHandler(mime, fi, CachedTransformer.Options, true, compType, true));
                }
                finally
                {
                    await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
                }
            }
            await CachedTransformer.SaveOrg(baseName, orgLen).ConfigureAwait(false);
            files.Add(null);
            return CachedTransformer.GetValidSorted(files, orgLen);
        }
    }

}
