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

        public String[] OnlyForPrefixes { get; init; }

        public IconHttpServerModule(StaticDataHttpServerModule dataModule, IconHttpServerModuleParams p = null)
        {
            var pp = p ?? new IconHttpServerModuleParams();
            var root = pp.UriRoot;
            PerfMon.Enabled = pp.PerMon;
            Data = dataModule;
            MimeCacheLookup = MimeCache.GetAlternateLookup<ReadOnlySpan<Char>>();
            ExtCacheLookup = ExtCache.GetAlternateLookup<ReadOnlySpan<Char>>();
            var rp = HttpServerTools.CombinePathsAndAddTrailingSlash(root);
            BasePath = rp;
            ExtPrefix = HttpServerTools.CombinePathsAndAddTrailingSlash(root, pp.ExtensionFolder);
            MimePrefix = HttpServerTools.CombinePathsAndAddTrailingSlash(root, pp.MimeFolder);
            FlagPrefix = HttpServerTools.CombinePathsAndAddTrailingSlash(root, pp.FlagFolder);
            if ((ExtPrefix.Length > 0) && (MimePrefix.Length > 0) && (FlagPrefix.Length > 0))
                OnlyForPrefixes = [ExtPrefix, MimePrefix, FlagPrefix];

            var t = GetType();
            var asm = t.Assembly;
            var n = (t.Namespace ?? String.Empty).Length + 6;
            var isDynamic = IsDynamic;
            var isStatic = IsStatic;
            var dynKeys = DynKeys;
            var resStatic = new Dictionary<string, string>(StringComparer.Ordinal);
            var resDynamic = new Dictionary<string, TextTemplate>(StringComparer.Ordinal);
            //  The lower cased resource names are built on the stack, the strings are only created when they are stored
            Span<Char> buf = stackalloc Char[MaxStackKey];
            //  The decompressed text resources are written to one pooled buffer
            using var ms = new ArrayPoolStream();
            foreach (var x in asm.GetManifestResourceNames())
            {
                //  The same as GetResourceCompression (the name without the compression extension), without the substring
                var name = x.AsSpan();
                ICompType comp = null;
                var f = name.LastIndexOf('.');
                if (f >= 0)
                {
                    comp = CompManager.GetFromExt(name.Slice(f + 1));
                    if (comp != null)
                        name = name.Slice(0, f);
                }
                if (!name.EndsWith(".svg", StringComparison.Ordinal))
                    continue;
                //  The same as FastToLower(n, name.Length - n - 4)
                var rs = name.Slice(n, name.Length - n - 4);
                var rl = rs.Length;
                var r = rl <= MaxStackKey ? buf.Slice(0, rl) : new Char[rl];
                rs.ToLowerInvariant(r);
                if (isDynamic.ContainsSpan(r))
                {
                    resDynamic.Add(new String(r), new TextTemplate(GetText(asm, x, comp, ms), dynKeys));
                    continue;
                }
                if (isStatic.ContainsSpan(r))
                {
                    AddRes(GetUrl(root, r), asm, comp, x);
                    continue;
                }
                if (r.StartsWith("flags.", StringComparison.Ordinal))
                {
                    AddRes(String.Concat(FlagPrefix, r.Slice(6), ".svg"), asm, comp, x);
                    continue;
                }
                if (r.StartsWith("g_", StringComparison.Ordinal) || r.StartsWith("m_", StringComparison.Ordinal))
                {
                    AddRes(GetUrl(root, r), asm, comp, x);
                    continue;
                }
                resStatic.Add(new String(r), GetText(asm, x, comp, ms));
            }
            var resLookup = resStatic.GetAlternateLookup<ReadOnlySpan<Char>>();
            foreach (var k in IconMimeMap)
            {
                if (resLookup.TryGetValue(MakeKey(buf, MimeKeyPrefix, k.Value), out var i))
                    resStatic.Add(String.Concat(MimeKeyPrefix, MakeKey(buf, null, k.Key)), i);
            }
            foreach (var k in IconExtMap)
            {
                if (resLookup.TryGetValue(MakeKey(buf, MimeKeyPrefix, k.Value), out var i))
                    resStatic.Add(String.Concat(ExtKeyPrefix, MakeKey(buf, null, k.Key)), i);
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

        static readonly String LocGen = "Generated " + typeof(IconHttpServerModule).Assembly.GetName().Name;
        static readonly String LocEmbedded = "Embedded " + typeof(IconHttpServerModule).Assembly.GetName().Name;

        void Add(String l, String data)
        {
            Data.AddText(l, LocGen, data, "image/svg+xml" + HttpServerTools.TextMimeSuffix, Encoding.UTF8, ClientCacheDuration, Comp, DisableComp, AsmTime, AsmETag, Auth);
        }

        void AddRes(String l, Assembly asm, ICompDecoder decoder, String data)
        {
            Data.AddMemory(l, LocEmbedded, asm.GetResourceData(data), "image/svg+xml" + HttpServerTools.TextMimeSuffix, ClientCacheDuration, Comp, DisableComp, AsmTime, AsmETag, decoder, Auth);
//            Data.AddStream(l, LocEmbedded, () => asm.GetManifestResourceStream(data), "image/svg+xml" + HttpServerTools.TextMimeSuffix, ClientCacheDuration, HttpServerTools.MaxRequestCache, Comp, DisableComp, Lwt, decoder, Auth);
        }

        static readonly DateTime AsmTime = typeof(IconHttpServerModule).Assembly.GetLastWriteTimerUtc();
        static readonly String AsmETag = HttpServerTools.ToEtag(AsmTime);


        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null) => HttpServerTools.NoEndPoints;

        const double HueSpread = 20;
        const double HueAdd = 360.0 - HueSpread * 0.5;

        /// <summary>
        /// Keys up to this length are built on the stack
        /// </summary>
        const int MaxStackKey = 256;

        const String MimeKeyPrefix = "mime_";
        const String ExtKeyPrefix = "ext_";

        /// <summary>
        /// The text of a resource (decompressed if compressed), the same as Encoding.UTF8.GetString(asm.GetUncompressedResourceData(ref x).Span), using a reused buffer for the decompressed data
        /// </summary>
        static String GetText(Assembly asm, String x, ICompDecoder comp, ArrayPoolStream ms)
        {
            var data = asm.GetResourceData(x);
            if (comp == null)
                return Encoding.UTF8.GetString(data.Span);
            ms.SetLength(0);
            comp.Decompress(data.Span, ms);
            return Encoding.UTF8.GetString(ms.Data, 0, (int)ms.Length);
        }

        /// <summary>
        /// The same as HttpServerTools.CombinePaths(root, r + ".svg"), using one allocation
        /// </summary>
        static String GetUrl(String root, ReadOnlySpan<Char> r)
            => String.IsNullOrEmpty(root) ? String.Concat(r, ".svg") : String.Concat(root, "/", r, ".svg");

        /// <summary>
        /// Write prefix + text.Replace('/', '_') to the buffer (a new array if it doesn't fit)
        /// </summary>
        /// <param name="buf">The buffer to use if the key fits</param>
        /// <param name="prefix">The prefix, may be null</param>
        /// <param name="text">The text</param>
        /// <returns>The key</returns>
        static ReadOnlySpan<Char> MakeKey(Span<Char> buf, String prefix, ReadOnlySpan<Char> text)
        {
            var pl = prefix?.Length ?? 0;
            var l = pl + text.Length;
            var d = l <= buf.Length ? buf.Slice(0, l) : new Char[l];
            prefix.AsSpan().CopyTo(d);
            text.Replace(d.Slice(pl), '/', '_');
            return d;
        }

        /// <summary>
        /// The same as StringTools.GetHashCode for a span
        /// </summary>
        static int GetHashCode(ReadOnlySpan<Char> s)
        {
            int value = 1123456793;
            foreach (var c in s)
            {
                value *= 16411;
                value += (int)c;
            }
            return value;
        }

        /// <summary>
        /// Generates the same numbers as new Random(seed).NextDouble() (the seeded legacy algorithm, Knuth's subtractive generator), using a stack buffer for the state (no allocations)
        /// </summary>
        ref struct SeededRandom
        {
            public SeededRandom(int seed, Span<int> seedArray)
            {
                int subtraction = (seed == int.MinValue) ? int.MaxValue : Math.Abs(seed);
                int mj = 161803398 - subtraction;
                seedArray[55] = mj;
                int mk = 1;
                int ii = 0;
                for (int i = 1; i < 55; i++)
                {
                    if ((ii += 21) >= 55)
                        ii -= 55;
                    seedArray[ii] = mk;
                    mk = mj - mk;
                    if (mk < 0)
                        mk += int.MaxValue;
                    mj = seedArray[ii];
                }
                for (int k = 1; k < 5; k++)
                {
                    for (int i = 1; i < 56; i++)
                    {
                        int n = i + 30;
                        if (n >= 55)
                            n -= 55;
                        seedArray[i] -= seedArray[1 + n];
                        if (seedArray[i] < 0)
                            seedArray[i] += int.MaxValue;
                    }
                }
                SeedArray = seedArray;
                INext = 0;
                INextP = 21;
            }

            readonly Span<int> SeedArray;
            int INext;
            int INextP;

            public double NextDouble()
            {
                int locINext = INext;
                if (++locINext >= 56)
                    locINext = 1;
                int locINextp = INextP;
                if (++locINextp >= 56)
                    locINextp = 1;
                var seedArray = SeedArray;
                int retVal = seedArray[locINext] - seedArray[locINextp];
                if (retVal == int.MaxValue)
                    retVal--;
                if (retVal < 0)
                    retVal += int.MaxValue;
                seedArray[locINext] = retVal;
                INext = locINext;
                INextP = locINextp;
                return retVal * (1.0 / int.MaxValue);
            }
        }

        /// <summary>
        /// Get the icon and colors of a file extension (lower case), the same as the string version before (no strings created, except if a long key doesn't fit on the stack)
        /// </summary>
        void GetFromExt(out String icon, out uint colorBright, out uint colorDark, ReadOnlySpan<Char> ext)
        {
            //  The same as MimeTypeMap.GetMimeType(ext).Item1.Split(';')[0].TrimEnd()
            ReadOnlySpan<Char> mime = MimeTypeMap.GetMimeType(ext).Item1;
            var sc = mime.IndexOf(';');
            if (sc >= 0)
                mime = mime.Slice(0, sc);
            mime = mime.TrimEnd();
            if (IconExtMap.TryGetValueSpan(ext, out var key) && (key != null))
                mime = key;
            if (IconMimeMap.TryGetValueSpan(mime, out key) && (key != null))
                mime = key;


            var mi = mime.IndexOf('+');
            if (mi >= 0)
            {
                GetFromExt(out icon, out colorBright, out colorDark, mime.Slice(mi + 1));
                return;
            }
            using (PerfMon.Track(nameof(GetFromExt)))
            {
                var ti = mime.IndexOf('/');
                var type = ti < 0 ? mime : mime.Slice(0, ti);
                //  Get colors
                var rng = new SeededRandom(GetHashCode(ext), stackalloc int[56]);
                if (MimeHue.TryGetValueSpan(mime, out var hue))
                {
                    hue += (rng.NextDouble() * HueSpread + HueAdd);
                    hue %= 360;
                }
                else
                {
                    if (MimeTypeHue.TryGetValueSpan(type, out hue))
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
                Span<Char> buf = stackalloc Char[MaxStackKey];
                if (!res.TryGetValueSpan(MakeExtKey(buf, ext), out icon))
                {
                    if (!res.TryGetValueSpan(MakeKey(buf, MimeKeyPrefix, mime), out icon))
                    {
                        if (!res.TryGetValueSpan(MakeKey(buf, MimeKeyPrefix, type), out icon))
                        {
                        }
                    }
                }
            }
        }

        /// <summary>
        /// "ext_" + ext (in the buffer if it fits, else a new array)
        /// </summary>
        static ReadOnlySpan<Char> MakeExtKey(Span<Char> buf, ReadOnlySpan<Char> ext)
        {
            const int pl = 4;
            var l = pl + ext.Length;
            var d = l <= buf.Length ? buf.Slice(0, l) : new Char[l];
            ExtKeyPrefix.AsSpan().CopyTo(d);
            ext.CopyTo(d.Slice(pl));
            return d;
        }

        void GetFromMime(out String icon, out uint colorBright, out uint colorDark, String mime)
        {
            using (PerfMon.Track(nameof(GetFromMime)))
            {
                IconMimeMap.TryGetValue(mime, out var key);
                mime = key ?? mime;

                var ti = mime.IndexOf('/');
                var type = ti < 0 ? mime.AsSpan() : mime.AsSpan(0, ti);
                //  Get colors
                var rng = new SeededRandom(StringTools.GetHashCode(mime), stackalloc int[56]);
                if (MimeHue.TryGetValue(mime, out var hue))
                {
                    hue += (rng.NextDouble() * HueSpread + HueAdd);
                    hue %= 360;
                }
                else
                {
                    if (MimeTypeHue.TryGetValueSpan(type, out hue))
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
                Span<Char> buf = stackalloc Char[MaxStackKey];
                if (!res.TryGetValueSpan(MakeKey(buf, MimeKeyPrefix, mime), out icon))
                {
                    if (!res.TryGetValueSpan(MakeKey(buf, MimeKeyPrefix, type), out icon))
                    {
                    }
                }
            }
        }

        /// <summary>
        /// The same as "#" + col.ToString("x6"), using one allocation
        /// </summary>
        static String WebColor(uint col)
        {
            Span<Char> b = stackalloc Char[9];
            b[0] = '#';
            col.TryFormat(b.Slice(1), out var w, "x6");
            return new String(b.Slice(0, w + 1));
        }

        const String IconKey = "<path filter=\"url(#b)\"";

        /// <summary>
        /// Create an icon
        /// </summary>
        /// <param name="lowerText">The text to show, lower case</param>
        /// <param name="colorBright">The bright color</param>
        /// <param name="colorDark">The dark color</param>
        /// <param name="icon">The icon to include (null for none)</param>
        /// <returns>The svg text</returns>
        String MakeIcon(String lowerText, uint colorBright, uint colorDark, String icon)
        {
            using (PerfMon.Track(nameof(MakeIcon)))
            {
                //  The values are looked up by a static function (no dictionary)
                var tl = lowerText.Length;
                var vars = (
                    Bright: WebColor(colorBright),
                    Dark: WebColor(colorDark),
                    Text: lowerText,
                    Scale: tl > 3 ? ((int)Math.Round(240.0 * 3 / tl, MidpointRounding.AwayFromZero)).ToString() : null,
                    Icon: icon != null ? String.Concat(icon, Environment.NewLine, IconKey) : null);
                return Base.Get(vars, static (v, k) => k switch
                {
                    "#dda" => v.Bright,
                    "#aa6" => v.Dark,
                    "TTF" => v.Text,
                    "240" => v.Scale,
                    IconKey => v.Icon,
                    _ => null,
                });
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
            //  Same as l.FastToLower(ep + 1) == "svg" (only 'S' and 's' lower case to 's' etc), without the lower case string
            var ext = l.AsSpan(ep + 1);
            if ((ext.Length != 3) || ((ext[0] | 0x20) != 's') || ((ext[1] | 0x20) != 'v') || ((ext[2] | 0x20) != 'g'))
                return null;
            var c = ExtPrefix;
            if (l.StartsWith(c, StringComparison.Ordinal))
            {
                var cl = c.Length;
                //  The same as GetFromExt(l.FastToLower(cl, l.Length - cl - 4)), the lower cased name is only created if it's not cached
                var icon = GetFromLower(l.AsSpan(cl, l.Length - cl - 4), ExtCacheLookup, false);
                Add(l, icon);
                return data.Handler(context);
            }
            c = MimePrefix;
            if (l.StartsWith(c, StringComparison.Ordinal))
            {
                var cl = c.Length;
                //  The same as GetFromMime(l.FastToLower(cl, l.Length - cl - 4)), the lower cased name is only created if it's not cached
                var icon = GetFromLower(l.AsSpan(cl, l.Length - cl - 4), MimeCacheLookup, true);
                Add(l, icon);
                return data.Handler(context);
            }
            return null;
        }

        /// <summary>
        /// Get the cached icon of the lower cased name, or create it
        /// </summary>
        /// <param name="name">The name (any case)</param>
        /// <param name="cache">The cache to look in</param>
        /// <param name="isMime">True for a mime type, false for a file extension</param>
        /// <returns>The svg text</returns>
        String GetFromLower(ReadOnlySpan<Char> name, ConcurrentDictionary<String, String>.AlternateLookup<ReadOnlySpan<Char>> cache, bool isMime)
        {
            var l = name.Length;
            Span<Char> lower = l <= MaxStackKey ? stackalloc Char[l] : new Char[l];
            name.ToLowerInvariant(lower);
            if (cache.TryGetValue(lower, out var val))
                return val;
            var s = new String(lower);
            return isMime ? CreateFromMime(s) : CreateFromExt(s);
        }

        readonly ConcurrentDictionary<String, String> MimeCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<String, String> ExtCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<String, String>.AlternateLookup<ReadOnlySpan<Char>> MimeCacheLookup;
        readonly ConcurrentDictionary<String, String>.AlternateLookup<ReadOnlySpan<Char>> ExtCacheLookup;

        public String GetFromExt(String ext)
        {
            if (ExtCache.TryGetValue(ext, out var val))
                return val;
            return CreateFromExt(ext);
        }

        String CreateFromExt(String ext)
        {
            GetFromExt(out var icon, out var colorBright, out var colorDark, ext.AsSpan());
            var val = MakeIcon(ext.FastToLower(), colorBright, colorDark, icon);
            ExtCache.TryAdd(ext, val);
            return val;
        }

        public String GetFromMime(String mime)
        {
            if (MimeCache.TryGetValue(mime, out var val))
                return val;
            return CreateFromMime(mime);
        }

        String CreateFromMime(String mime)
        {
            GetFromMime(out var icon, out var colorBright, out var colorDark, mime);
            var mp = mime.IndexOf('/');
            //  The same as FastToLower of the text after the '/' (one string)
            var val = MakeIcon(mp < 0 ? mime.FastToLower() : mime.FastToLower(mp + 1), colorBright, colorDark, icon);
            MimeCache.TryAdd(mime, val);
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


