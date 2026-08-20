using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Minifier;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{

    public class SvgTransformer : CachedTransformer, ICachedTransformer
    {
        public SvgTransformer(SvgTransformerParams p = null)
            : base(p ?? new SvgTransformerParams())
        {
            p = p ?? new SvgTransformerParams();
            SvgOpts = p.Svg ?? SvgTransformerParams.GetDefault();

            Add("svg", this);
            BuildStrategy = p.BuildDirect ? CachedTransformerBuildStrategies.AlwaysDirect : CachedTransformerBuildStrategies.AlwaysDefer;
        }
        readonly SvgMinifierParams SvgOpts;

        public string Info => "svg minifier";

        public CachedTransformerBuildStrategies BuildStrategy { get; init; }

        public async Task<FileHttpRequestHandler[]> Build(CachedTransformer service, CachedTransformerFile info, ReadOnlyMemory<byte> data, CachedTransformerEntry entry)
        {
            if (info.State.Handler.Decoder != null)
                return null;
            var orgLen = data.Length;
            List<FileHttpRequestHandler> files = new(2);
            var mime = Tuple.Create(info.Mime, false);
            var baseName = info.BaseName;
            var compType = CompType;
            var name = baseName + ".svg" + CompExt;
            var fi = new FileInfo(name);
            if ((!fi.Exists) || (fi.Length <= 0))
            {
                using var _ = service.PerfMon.Track("SvgMinifier");
                var tempName = name + CachedTransformer.TempExt;
                try
                {
                    var enc = Encoding.UTF8;
                    var d = enc.GetStringWithoutBom(data.Span);
                    d = SvgMinifier.Optimize(d, null, SvgOpts);
                    if (d == null)
                        return null;
                    if (compType != null)
                        await compType.GetCompressed(enc.GetBytes(d), CompEncoderLevels.Best).WriteToFileAsync(tempName).ConfigureAwait(false);
                    else
                        await File.WriteAllBytesAsync(tempName, enc.GetBytes(d)).ConfigureAwait(false);
                    await PathExt.TryMoveFileAsync(tempName, name).ConfigureAwait(false);
                    fi = new FileInfo(name);
                    if (fi.Exists && (fi.Length > 0))
                        files.Add(new FileHttpRequestHandler(mime, fi, CachedTransformer.Options, true, compType, true));
                    else
                        return null;
                }
                finally
                {
                    await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
                }
            }
            else
            {
                files.Add(new FileHttpRequestHandler(mime, fi, CachedTransformer.Options, true, compType, true));
            }
            await CachedTransformer.SaveOrg(baseName, orgLen).ConfigureAwait(false);
            files.Add(null);
            return CachedTransformer.GetValidSorted(files, orgLen);




        }

        public CachedTransformerEntry Validate(CachedTransformer service, CachedTransformerFile info)
        {
            var baseName = info.BaseName;
            var compType = CompType;
            var fi = new FileInfo(baseName + ".svg" + CompExt);
            if (!fi.Exists)
                return null;
            var orgSize = CachedTransformer.ReadOrg(baseName);
            if (orgSize < 0)
                return null;
            List<FileHttpRequestHandler> files = new(2);
            var mime = Tuple.Create(info.Mime, false);
            files.Add(new FileHttpRequestHandler(mime, fi, CachedTransformer.Options, true, compType, true));
            files.Add(null);
            return new CachedTransformerEntry
            {
                Completed = true,
                OrgSize = orgSize,
                Files = CachedTransformer.GetValidSorted(files, orgSize),
            };
        }



    }





}
