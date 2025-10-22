using SysWeaver.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Concurrent;

[assembly: SysWeaver.ResourceOrder(-100)]


namespace SysWeaver.Net.IconModule
{


    public sealed class IconHttpServerModule : IHttpServerModule, IPerfMonitored
    {

        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(nameof(IconHttpServerModule));

        /// <summary>
        /// The url to use to get a folder icon
        /// </summary>
        public readonly string FolderName;

        /// <summary>
        /// The url to use to get a virtual folder icon
        /// </summary>
        public readonly string VirtualFolderName;
        
        /// <summary>
        /// The url to use to get a API icon
        /// </summary>
        public readonly string ApiFolderName;
        

        /// <summary>
        /// The prefix to use for icons
        /// </summary>
        public readonly string BasePath;

        /// <summary>
        /// The prefix to use to get extension images, ex: ExtPrefix + "pdf.svg" to get the icon for a pdf file.
        /// </summary>
        public readonly string ExtPrefix;
        
        /// <summary>
        /// The prefix to use to get mime type images, ex: MimePrefix + "text/plain.svg" to get the icon for the "text/plain" mime
        /// </summary>
        public readonly string MimePrefix;

        /// <summary>
        /// The prefix to use to get flag images, ex: FlagPrefix + "us.svg" to get the icon for usa
        /// </summary>
        public readonly string FlagPrefix;

        
        public IconHttpServerModule(StaticDataHttpServerModule dataModule, IconHttpServerModuleParams p = null)
        {
            var pp = p ?? new IconHttpServerModuleParams();
            var root = pp.UriRoot;
            PerfMon.Enabled = p.PerMon;
            Data = dataModule;
            var rp = HttpServerTools.CombinePathsAndAddTrailingSlash(root);
            BasePath = rp;
            ExtPrefix = HttpServerTools.CombinePathsAndAddTrailingSlash(root, pp.ExtensionFolder);
            MimePrefix = HttpServerTools.CombinePathsAndAddTrailingSlash(root, pp.MimeFolder);
            FlagPrefix = HttpServerTools.CombinePathsAndAddTrailingSlash(root, pp.FlagFolder);
            var t = GetType();
            var asm = GetType().Assembly;
            DateTime lw = HttpServerTools.StartedTime;
            try
            {
                var qfn = asm.ManifestModule.FullyQualifiedName;
                var fi = new FileInfo(qfn);
                if (fi.Exists)
                    lw = fi.LastWriteTimeUtc;
            }
            catch
            {
            }
            Lwt = HttpServerTools.ToEtag(lw);
            var n = (t.Namespace ?? String.Empty).Length + 6;
            var isDynamic = IsDynamic;
            var dynKeys = DynKeys;
            var resStatic = new Dictionary<string, string>(StringComparer.Ordinal);;
            var resDynamic = new Dictionary<string, TextTemplate>(StringComparer.Ordinal);;
            foreach (var x in asm.GetManifestResourceNames())
            {
                var orgName = x;
                var comp = asm.GetResourceCompression(ref orgName);
                if (!orgName.EndsWith(".svg", StringComparison.Ordinal))
                    continue;
                var r = orgName.Substring(n, orgName.Length - n - 4).FastToLower();
                if (isDynamic.Contains(r))
                {
                    orgName = x;
                    var mem = asm.GetUncompressedResourceData(ref orgName);
                    var text = Encoding.UTF8.GetString(mem.Span);
                    resDynamic.Add(r, new TextTemplate(text, dynKeys));
                    continue;
                }
                if (IsStatic.Contains(r))
                {
                    AddRes(HttpServerTools.CombinePaths(root, r + ".svg"), asm, comp, x);
                    continue;
                }
                if (r.StartsWith("flags.", StringComparison.Ordinal))
                {
                    string cc = r.Substring(6);
                    string fname = FlagPrefix + cc + ".svg";
                    AddRes(fname, asm, comp, x);
                    continue;
                }
                if (r.StartsWith("g_", StringComparison.Ordinal) || r.StartsWith("m_", StringComparison.Ordinal))
                {
                    AddRes(HttpServerTools.CombinePaths(root, r + ".svg"), asm, comp, x);
                    continue;
                }
                {
                    orgName = x;
                    var mem = asm.GetUncompressedResourceData(ref orgName);
                    var text = Encoding.UTF8.GetString(mem.Span);
                    resStatic.Add(r, text);
                }
            }
            foreach (var k in IconMimeMap)
            {
                var kv = k.Value.Replace('/', '_');
                if (resStatic.TryGetValue("mime_" + kv, out var i))
                    resStatic.Add("mime_" + k.Key.Replace('/', '_'), i);
            }
            foreach (var k in IconExtMap)
            {
                var kv = k.Value.Replace('/', '_');
                if (resStatic.TryGetValue("mime_" + kv, out var i))
                    resStatic.Add("ext_" + k.Key.Replace('/', '_'), i);
            }

            StaticResources = resStatic.Freeze();
            DynamicResources = resDynamic.Freeze();

            Base = resDynamic["base"];
            Comp = HttpCompressionPriority.GetSupportedEncoders(pp.Compression);
            DisableComp = pp.Compression == String.Empty;
            ClientCacheDuration = pp.ClientCacheDuration;
            Auth = pp.Auth;
            FolderName = HttpServerTools.CombinePaths(root, "folder.svg");
            VirtualFolderName = HttpServerTools.CombinePaths(root, "virtual.svg");
            ApiFolderName = HttpServerTools.CombinePaths(root, "api.svg");
        }

        static readonly IReadOnlySet<String> DynKeys = ReadOnlyData.Set(StringComparer.Ordinal,
            "#dda",
            "#aa6",
            "TTF",
            "240",
            "<path filter=\"url(#b)\""
        );

        static readonly IReadOnlySet<String> IsDynamic = ReadOnlyData.Set(StringComparer.Ordinal,
            "base"
        );

        static readonly IReadOnlySet<String> IsStatic = ReadOnlyData.Set(StringComparer.Ordinal,
            "folder",
            "virtual",
            "api"
        );

        public override string ToString() => String.Concat(
            nameof(StaticResources), ": ", StaticResources?.Count, ", ",
            nameof(DynamicResources), ": ", DynamicResources?.Count);

        readonly IReadOnlyDictionary<String, String> StaticResources;
        readonly IReadOnlyDictionary<String, TextTemplate> DynamicResources;
        readonly StaticDataHttpServerModule Data;
        readonly TextTemplate Base;
        readonly bool DisableComp;
        readonly int? ClientCacheDuration;
        readonly String Auth;
        readonly HttpCompressionPriority Comp;
        readonly String Lwt;


        static readonly String LocGen = "Generated " + typeof(IconHttpServerModule).Assembly.GetName().Name;
        static readonly String LocEmbedded = "Embedded " + typeof(IconHttpServerModule).Assembly.GetName().Name;

        void Add(String l, String data)
        {
            Data.AddText(l, LocGen, data, "image/svg+xml" + HttpServerTools.TextMimeSuffix, Encoding.UTF8, ClientCacheDuration, Comp, DisableComp, Lwt, Auth);
        }

        void AddRes(String l, Assembly asm, ICompDecoder decoder, String data)
        {
            Data.AddStream(l, LocEmbedded, () => asm.GetManifestResourceStream(data), "image/svg+xml" + HttpServerTools.TextMimeSuffix, ClientCacheDuration, HttpServerTools.MaxRequestCache, Comp, DisableComp, Lwt, decoder, Auth);
        }

        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null) => HttpServerTools.NoEndPoints;

        const double HueSpread = 20;
        const double HueAdd = 360.0 - HueSpread * 0.5;

        void GetFromExt(out String icon, out uint colorBright, out uint colorDark, String ext)
        {
            var mime = MimeTypeMap.GetMimeType(ext).Item1.Split(';')[0].TrimEnd();
            IconExtMap.TryGetValue(ext, out var key);
            mime = key ?? mime;
            IconMimeMap.TryGetValue(mime, out key);
            mime = key ?? mime;


            var mi = mime.IndexOf('+');
            if (mi >= 0)
            {
                GetFromExt(out icon, out colorBright, out colorDark, mime.Substring(mi + 1));
                return;
            }
            using (PerfMon.Track(nameof(GetFromExt)))
            {
                var ti = mime.IndexOf('/');
                var type = ti < 0 ? mime : mime.Substring(0, ti);
                //  Get colors
                var rng = new Random(StringTools.GetHashCode(ext));
                if (MimeHue.TryGetValue(mime, out var hue))
                {
                    hue += (rng.NextDouble() * HueSpread + HueAdd);
                    hue %= 360;
                }
                else
                {
                    if (MimeTypeHue.TryGetValue(type, out hue))
                    {
                        hue += (rng.NextDouble() * HueSpread + HueAdd);
                        hue %= 360;
                    }
                    else
                    {
                        hue = rng.NextDouble() * 360;
                    }
                }
                var sat = rng.NextDouble() * 0.55 + 0.4;
                colorBright = ColorTools.HsvToRgb(hue, sat, 0.8);
                colorDark = ColorTools.HsvToRgb(hue, sat, 0.6);
                //  Get icon
                var res = StaticResources;
                if (!res.TryGetValue("ext_" + ext, out icon))
                {
                    if (!res.TryGetValue("mime_" + mime.Replace('/', '_'), out icon))
                    {
                        if (!res.TryGetValue("mime_" + type, out icon))
                        {
                        }
                    }
                }
            }
        }

        void GetFromMime(out String icon, out uint colorBright, out uint colorDark, String mime)
        {
            using (PerfMon.Track(nameof(GetFromMime)))
            {
                IconMimeMap.TryGetValue(mime, out var key);
                mime = key ?? mime;

                var ti = mime.IndexOf('/');
                var type = ti < 0 ? mime : mime.Substring(0, ti);
                //  Get colors
                var rng = new Random(StringTools.GetHashCode(mime));
                if (MimeHue.TryGetValue(mime, out var hue))
                {
                    hue += (rng.NextDouble() * HueSpread + HueAdd);
                    hue %= 360;
                }
                else
                {
                    if (MimeTypeHue.TryGetValue(type, out hue))
                    {
                        hue += (rng.NextDouble() * HueSpread + HueAdd);
                        hue %= 360;
                    }
                    else
                    {
                        hue = rng.NextDouble() * 360;
                    }
                }
                var sat = rng.NextDouble() * 0.55 + 0.4;
                colorBright = ColorTools.HsvToRgb(hue, sat, 0.8);
                colorDark = ColorTools.HsvToRgb(hue, sat, 0.5);
                //  Get icon
                var res = StaticResources;
                if (!res.TryGetValue("mime_" + mime.Replace('/', '_'), out icon))
                {
                    if (!res.TryGetValue("mime_" + type, out icon))
                    {
                    }
                }
            }
        }

        static String WebColor(uint col) => "#" + col.ToString("x6");

        String MakeIcon(String text, uint colorBright, uint colorDark, String icon)
        {
            using (PerfMon.Track(nameof(MakeIcon)))
            {
                Dictionary<String, String> vars = new(StringComparer.Ordinal)
                {
                    { "#dda", WebColor(colorBright) },
                    { "#aa6", WebColor(colorDark) },
                    { "TTF", text.FastToLower() },
                };
                var tl = text.Length;
                if (tl > 3)
                {
                    var scale = (int)Math.Round(240.0 * 3 / tl, MidpointRounding.AwayFromZero);
                    vars["240"] = scale.ToString();
                }
                if (icon != null)
                {
                    const String key = "<path filter=\"url(#b)\"";
                    vars[key] = icon + Environment.NewLine + key;
                }
                return Base.Get(vars);
            }
        }

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var l = context.LocalUrl;
            var data = Data;
            if (data.Contains(l))
                return null;
            var ep = l.LastIndexOf('.');
            if (ep < 0)
                return null;
            var ext = l.Substring(ep + 1).FastToLower();
            if (ext != "svg")
                return null;
            var c = ExtPrefix;
            if (l.StartsWith(c, StringComparison.Ordinal))
            {
                var cl = c.Length;
                var dataExt = l.Substring(cl, l.Length - cl - 4).FastToLower();
                var icon = GetFromExt(dataExt);
                Add(l, icon);
                return data.Handler(context);
            }
            c = MimePrefix;
            if (l.StartsWith(c, StringComparison.Ordinal))
            {
                var cl = c.Length;
                var mime = l.Substring(cl, l.Length - cl - 4).FastToLower();
                var icon = GetFromMime(mime);
                Add(l, icon);
                return data.Handler(context);
            }
            return null;
        }


        readonly ConcurrentDictionary<String, String> MimeCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<String, String> ExtCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public String GetFromExt(String ext)
        {
            var cache = ExtCache;
            if (cache.TryGetValue(ext, out var val))
                return val;
            GetFromExt(out var icon, out var colorBright, out var colorDark, ext);
            val = MakeIcon(ext, colorBright, colorDark, icon);
            cache.TryAdd(ext, val);
            return val;
        }
        
        public String GetFromMime(String mime)
        {
            var cache = MimeCache;
            if (cache.TryGetValue(mime, out var val))
                return val;
            GetFromMime(out var icon, out var colorBright, out var colorDark, mime);
            var mp = mime.IndexOf('/');
            String text = mp < 0 ? mime : mime.Substring(mp + 1);
            val = MakeIcon(text, colorBright, colorDark, icon);
            cache.TryAdd(mime, val);
            return val;
        }


        #region Tweaks

        /// <summary>
        /// List of mime replacements (change a mime to another that mapps to a better icon)
        /// </summary>
        static readonly IReadOnlyDictionary<String, String> IconMimeMap = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            { "application/json", "application/javascript" },
            { "application/x-tar", "application/zip" },
            { "application/x-gtar", "application/zip" },
            { "application/x-gzip", "application/zip" },
            { "application/x-rar-compressed", "application/zip" },
            { "application/x-7z-compressed", "application/zip" },
            { "application/pdf", "text/pdf" },
            { "application/xml", "text/xml" },

        }.Freeze();

        /// <summary>
        /// List of file extension to mime mappings (override default mapping)
        /// </summary>
        static readonly IReadOnlyDictionary<String, String> IconExtMap = new Dictionary<String, String>(StringComparer.Ordinal)
        {

            { "br", "application/zip" },
            { "gzip", "application/zip" },
            { "csproj", "text/xml" },
            { "shproj", "text/xml" },
            { "sln", "text/plain" },
            { "php", "text/plain" },
            { "lz4", "application/zip" },
            { "ods", "text" },
            { "rtf", "text" },
            { "psd", "image" },


            {  "ttf", "font" },
            {  "otf", "font" },
            {  "sfnt", "font" },
            {  "woff", "font" },
            {  "woff2", "font" },
        }.Freeze();

        /// <summary>
        /// Pick a specific hue for a given mime
        /// </summary>
        static readonly IReadOnlyDictionary<String, double> MimeHue = new Dictionary<String, double>(StringComparer.Ordinal)
        {
            { "application/zip", 60 },
            { "text/pdf", 0 },
        }.Freeze();

        /// <summary>
        /// Pick a specific hue for a given mime type
        /// </summary>
        static readonly IReadOnlyDictionary<String, double> MimeTypeHue = new Dictionary<String, double>(StringComparer.Ordinal)
        {
            { "application", 0 },
            { "audio", 60 },
            { "font", 180 },
            { "image", 120 },
            { "text", 240 },
            { "video", 300 },
        }.Freeze();

        #endregion//Tweaks


    }
}


