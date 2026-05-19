using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using WaterTrans.GlyphLoader;

namespace SysWeaver.Media
{
    public sealed class SvgFont
    {

        static SvgFont FromResource(String name)
        {
            var type = typeof(SvgFont);
            var asm = type.Assembly;
            var pre = type.Namespace + ".data.";
            using var s = asm.GetManifestResourceStream(pre + name + ".ttf");
            if (s != null)
            {
                return GetOrCreateCreateFromStream(s, name, true);
            }else
            {
                using var ss = asm.GetManifestResourceStream(pre + name + ".ttf.br");
                using var d = new BrotliStream(ss, CompressionMode.Decompress);
                return GetOrCreateCreateFromStream(d, name, true);
            }
        }

        static readonly object CreateLock = new object();
        
        static SvgFont InternalFont(Func<SvgFont> f, String name, Action<SvgFont> set)
        {
            var v = f();
            if (v != null)
                return v;
            lock (CreateLock)
            {
                v = f();
                if (v != null)
                    return v;
                v = FromResource(name);
                set(v);
            }
            return v;
        }
        static volatile SvgFont InternalAsianNinja;
        static volatile SvgFont InternalAstroSpace;
        static volatile SvgFont InternalAtariClassic;
        static volatile SvgFont InternalMontserratBlack;
        static volatile SvgFont InternalRedemtionRegular;
        static volatile SvgFont InternalRoTwimchRegular;
        static volatile SvgFont InternalVegapunkFree;


        public static SvgFont AsianNinja => InternalFont(() => InternalAsianNinja, nameof(AsianNinja), v => InternalAsianNinja = v);

        public static SvgFont AstroSpace => InternalFont(() => InternalAstroSpace, nameof(AstroSpace), v => InternalAstroSpace = v);
        public static SvgFont AtariClassic => InternalFont(() => InternalAtariClassic, nameof(AtariClassic), v => InternalAtariClassic = v);
        public static SvgFont MontserratBlack => InternalFont(() => InternalMontserratBlack, nameof(MontserratBlack), v => InternalMontserratBlack = v);

        public static SvgFont RedemtionRegular => InternalFont(() => InternalRedemtionRegular, nameof(RedemtionRegular), v => InternalRedemtionRegular = v);
        public static SvgFont RoTwimchRegular => InternalFont(() => InternalRoTwimchRegular, nameof(RoTwimchRegular), v => InternalRoTwimchRegular = v);
        public static SvgFont VegapunkFree => InternalFont(() => InternalVegapunkFree, nameof(VegapunkFree), v => InternalVegapunkFree = v);


        public static SvgFont GetOrCreateCreateFromStream(Stream s, String filename = null, bool leaveOpen = false)
            => new SvgFont(s, filename, leaveOpen);

        static String TryGetFontFile(String family, String style, String weight, params String[] extraFontFolders)
        {
            var ranker = new Search.SimpleTextSearch().CreateRanker(String.Join(' ', family, style, weight));
            var s = new Search.SimpleTextSearch();
            double best = -1;
            String bestFont = null;
            var fe = FontExtensions;
            var folders = new List<String>();
            if (extraFontFolders != null)
                folders.AddRange(extraFontFolders.Where(x => x != null));
            folders.AddRange(Paths.Where(x => x.Value > 0).Select(x => x.Key));
            folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder))
                    continue;
                foreach (var f in Directory.GetFiles(folder))
                {
                    var fi = new FileInfo(f);
                    var ext = fi.Extension.FastToLower();
                    if (!fe.Contains(ext))
                        continue;
                    var fn = fi.Name;
                    fn = fn.Substring(0, fn.Length - ext.Length);
                    var o = ranker.Rank(fn);
                    if (o <= best)
                        continue;
                    best = o;
                    bestFont = f;
                }
            }
            return bestFont;
        }

        static readonly IReadOnlySet<String> FontExtensions = ReadOnlyData.Set(StringComparer.Ordinal,
            ".ttf", ".ttc",
            ".otf", ".otc",
            ".woff2"
            );


        public static SvgFont GetOrCreate(String family, String style, String weight, params String[] extraFontFolders)
        {
            var key = String.Join('\n', family, weight, style);
            var c = Cache;
            if (c.TryGetValue(key, out var f))
                return f;
            lock (c)
            {
                if (c.TryGetValue(key, out f))
                    return f;
                var fn = TryGetFontFile(family, style, weight, extraFontFolders);
                if (fn == null)
                {
                    c.TryAdd(key, null);
                    return null;
                }
                var font = GetOrCreate(fn);
                c.TryAdd(key, font);
                return font;
            }
        }


        public static SvgFont GetOrCreate(String filename)
        {
            var d = new FileInfo(filename);
            if (!d.Exists)
                return null;
            var key = d.FullName;
            var c = Cache;
            if (c.TryGetValue(key, out var f))
                return f;
            lock (c)
            {
                if (c.TryGetValue(key, out f))
                    return f;
                f = new SvgFont(filename);
                c.TryAdd(key, f);
                return f;
            }
        }


        public static void AddFontPath(String folder)
            => Paths.IncValue(folder);

        public static void RemoveFontPath(String folder)
            => Paths.DecValue(folder);

        static readonly ConcurrentCount<String> Paths = new ConcurrentCount<string>(StringComparer.Ordinal);




        public static void ClearCache()
        {
            var c = Cache;
            lock(c)
            {
                c.Clear();
            }
        }


        static readonly ConcurrentDictionary<String, SvgFont> Cache = new ConcurrentDictionary<string, SvgFont>(StringComparer.Ordinal);

        public override string ToString() => Filename.ToQuoted();

        public readonly String Filename;

        SvgFont(String filename)
        {
            Filename = filename;
            using (var s = new FileStream(filename, FileMode.Open, FileAccess.Read))
                TF = new Typeface(s);
        }

        SvgFont(Stream s, String filename = null, bool leaveOpen = false)
        {
            Filename = filename ?? "<Stream>";
            using (var ss = leaveOpen ? null : s)
                TF = new Typeface(s);
        }

        internal readonly Typeface TF;

    }


}