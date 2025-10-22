using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SysWeaver.Compression;
using SysWeaver.Media;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{


    public sealed class AvatarService : IDefaultUserImageHandler, IPerfMonitored, IHaveStats, IHttpServerModule
    {
        public AvatarService(AvatarParams p)
        {
            p = p ?? new AvatarParams();
            var folder = p.DiscFolder;
            AsyncHandler = Handle;
            Cache = new FastMemCache<int, ValueTask<IHttpRequestHandler>>(TimeSpan.FromSeconds(Math.Max(1, p.CacheSeconds)));
            DiscFolder = folder;
            if (String.IsNullOrEmpty(folder))
            {
                var pm = PerfMon;
                var t = typeof(AvatarService).Assembly;
                Files = t.GetManifestResourceNames().Convert(n =>
                {
                    using var __ = pm.Track("ReadEmbedded");
                    var nn = n;
                    var data = t.GetUncompressedResourceData(ref nn);
                    return SvgCanvas.Create(Encoding.UTF8.GetString(data.Span));
                });
            }
            else
            {
                ReadFiles();
            }
        }
        readonly String DiscFolder;


        const int PrefixLen = 8;

        public String[] OnlyForPrefixes { get; } = ["Avatars/"];


        public Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler { get; init; }


        ValueTask<IHttpRequestHandler> Handle(HttpServerRequest context)
        {
            var lname = context.LocalUrl;
            var e = lname.LastIndexOf('.');
            if (e <= PrefixLen)
                return NullRet;
            ++e;
            if (lname[e] != 's')
                return NullRet;
            ++e;
            if (lname[e] != 'v')
                return NullRet;
            ++e;
            if (lname[e] != 'g')
                return NullRet;
            lname = lname.Substring(PrefixLen, e - (PrefixLen + 3));
            if (!int.TryParse(lname, out var seed))
                return NullRet;
            return Get(seed);
        }

        static readonly ValueTask<IHttpRequestHandler> NullRet = ValueTask.FromResult<IHttpRequestHandler>(null);



        void ReadFiles()
        {
            var folder = DiscFolder;
            if (String.IsNullOrEmpty(folder))
                return;
            if (!Directory.Exists(folder))
                return;
            var pm = PerfMon;
            using var _ = pm.Track(nameof(ReadFiles));
            var t = Directory.GetFiles(folder, "*.svg", SearchOption.TopDirectoryOnly);
            Interlocked.Exchange(ref Files, t.Convert(n =>
            {
                using var __ = pm.Track("ReadFile");
                return SvgCanvas.Load(new FileStream(n, FileMode.Open), "0.##", false);
            }));
        }

        /// <summary>
        /// Reload all avatar images from disc
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        public bool ReloadFiles()
        {
            ReadFiles();
            return true;
        }

        volatile SvgCanvas[] Files;

        /// <summary>
        /// Get a request handler to some icon
        /// </summary>
        /// <param name="seed">The seed of the icon to get</param>
        /// <returns>null if no files are found</returns>
        public ValueTask<IHttpRequestHandler> Get(int seed)
            => Cache.GetOrUpdate(seed, Generate);

        static readonly ValueTask<IHttpRequestHandler> NullHandler = ValueTask.FromResult<IHttpRequestHandler>(null);

        ValueTask<IHttpRequestHandler> Generate(int seed)
        {
            using var __ = PerfMon.Track(nameof(Generate));
            var files = Files;
            if (files == null)
                return NullHandler;
            var fl = files.Length;
            var rng = new Random(seed);
            HashColors.GetRandom(out var h, out var s, rng.Next());
            var svg = new SvgScene(256, 256);

            var bp = new SvgNgonParams();
            bp.Face.FillColor = HashColors.GetWeb(h, s, 0.7);
            bp.Face.StrokeColor = HashColors.GetWeb(h, s, 0.95);
            bp.Face.StrokeWidth = 2;
            bp.OffsetX = 8;
            bp.OffsetY = 8;
            bp.Size = 256 - 8 * 2;
            bp.Extrude = null;
            bp.Shadow = null;
            if (rng.Next(4) != 0)
            {
                bp.AngleOffset = rng.Next(18) * 20;
                svg.AddNGon(5 + rng.Next(4), bp);
            }
            else
            {
                var rad = rng.Next(60) + 4;
                var path = SvgPath.GetRoundedRect(bp.Size, bp.Size, rad, 3, bp.OffsetX, bp.OffsetY);
                svg.AddPath(path, bp);
            }

            var svgText = svg.ToSvg();
            var canvas = SvgCanvas.Create(svgText);

            var bg = canvas.Svg.Elements().FirstOrDefault(e => !"style".FastEquals(e.Name.LocalName));
            canvas.CreateDropShadowClass("bg", "#000", 3, 1, 0, 0);
            canvas.CreateDropShadowClass("ic", "#000", 4, 1, 2, 3);
            canvas.AddFilter(bg, "bg");

            var el = canvas.Add(files[rng.Next(fl)], 32, 32, 256 - 64, 256 - 64);

            canvas.AddFilter(el, "ic");



            if ((rng.Next() & 1) == 0)
            {
                var a = el.Attribute("transform")?.Value;
                var scalePos = a.IndexOf("scale(");
                double scale = 1;
                if (scalePos >= 0)
                {
                    scalePos += 6;
                    var scaleEnd = a.IndexOf(')', scalePos);
                    scale = double.Parse(a.Substring(scalePos, scaleEnd - scalePos), SvgCanvas.Ci);
                }
                var st = scale.ToString(SvgCanvas.Ci).TrimEnd('0').TrimEnd('.');
                el.SetAttributeValue("transform", String.Concat("translate(224 32) scale(-", st, ' ', st, ')'));
            }
            svgText = canvas.ToSvgString();
            var enc = Encoding.UTF8;
            var svgData = enc.GetBytes(svgText);
            var cmp = CompBrotliNET.Instance;
            var svgMem = cmp.GetCompressed(svgData.AsSpan(), CompEncoderLevels.Best);
            var svgHandler = new StaticMemoryHttpRequestHandler("icon.svg", "Generated", svgMem, MimeTypeMap.Svg, SvgCompression, 30, 15, null, cmp, Array.Empty<String>());
            return ValueTask.FromResult<IHttpRequestHandler>(svgHandler);
        }


        readonly FastMemCache<int, ValueTask<IHttpRequestHandler>> Cache;

        public ValueTask<IHttpRequestHandler> Get(string userGuid, int size)
        {
            var seed = (int)QuickHash.Hash(userGuid);
            return Get(seed);
        }

        public IEnumerable<Stats> GetStats()
        {
            var g = nameof(AvatarService);
            foreach (var x in Cache.GetStats(g))
                yield return x;
            yield return new Stats(g, "Images", Files?.Length ?? 0, "The number of images files");
        }

        static readonly HttpCompressionPriority SvgCompression = HttpCompressionPriority.GetSupportedEncoders();

        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(AvatarService));
    }
}
