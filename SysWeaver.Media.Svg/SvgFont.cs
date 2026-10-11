using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using WaterTrans.GlyphLoader;

namespace SysWeaver.Media
{
    /// <summary>
    /// A font (TrueType / OpenType) used to convert text to svg paths (see <see cref="SvgPath.GetSvgTextPaths(string, SvgFont, double, int)"/>).
    /// Some free fonts are embedded (the static properties), other fonts are loaded from files and cached.
    /// </summary>
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

        /// <summary>
        /// Get the font file data of an embedded font
        /// </summary>
        /// <param name="name">The name of the embedded font, ex: "MontserratBlack"</param>
        /// <returns>The font file data</returns>
        internal static Byte[] GetEmbeddedData(String name)
        {
            var type = typeof(SvgFont);
            var asm = type.Assembly;
            var pre = type.Namespace + ".data.";
            using var ms = new MemoryStream();
            using (var s = asm.GetManifestResourceStream(pre + name + ".ttf"))
            {
                if (s != null)
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            using var ss = asm.GetManifestResourceStream(pre + name + ".ttf.br");
            using var d = new BrotliStream(ss, CompressionMode.Decompress);
            d.CopyTo(ms);
            return ms.ToArray();
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


        /// <summary>
        /// The embedded "Asian Ninja" font (loaded on first use, the same instance is returned)
        /// </summary>
        public static SvgFont AsianNinja => InternalFont(() => InternalAsianNinja, nameof(AsianNinja), v => InternalAsianNinja = v);

        /// <summary>
        /// The embedded "Astro Space" font (loaded on first use, the same instance is returned)
        /// </summary>
        public static SvgFont AstroSpace => InternalFont(() => InternalAstroSpace, nameof(AstroSpace), v => InternalAstroSpace = v);
        /// <summary>
        /// The embedded "Atari Classic" font (loaded on first use, the same instance is returned)
        /// </summary>
        public static SvgFont AtariClassic => InternalFont(() => InternalAtariClassic, nameof(AtariClassic), v => InternalAtariClassic = v);
        /// <summary>
        /// The embedded "Montserrat Black" font (loaded on first use, the same instance is returned), the fallback font of <see cref="SvgCanvas.RasterizeText"/>
        /// </summary>
        public static SvgFont MontserratBlack => InternalFont(() => InternalMontserratBlack, nameof(MontserratBlack), v => InternalMontserratBlack = v);

        /// <summary>
        /// The embedded "Redemtion Regular" font (loaded on first use, the same instance is returned)
        /// </summary>
        public static SvgFont RedemtionRegular => InternalFont(() => InternalRedemtionRegular, nameof(RedemtionRegular), v => InternalRedemtionRegular = v);
        /// <summary>
        /// The embedded "RoTwimch Regular" font (loaded on first use, the same instance is returned)
        /// </summary>
        public static SvgFont RoTwimchRegular => InternalFont(() => InternalRoTwimchRegular, nameof(RoTwimchRegular), v => InternalRoTwimchRegular = v);
        /// <summary>
        /// The embedded "Vegapunk Free" font (loaded on first use, the same instance is returned)
        /// </summary>
        public static SvgFont VegapunkFree => InternalFont(() => InternalVegapunkFree, nameof(VegapunkFree), v => InternalVegapunkFree = v);


        /// <summary>
        /// Load a font from a stream (despite the name, nothing is cached, a new instance is returned on every call)
        /// </summary>
        /// <param name="s">The stream with the font file data</param>
        /// <param name="filename">The name of the font (returned by <see cref="ToString"/>), null for "&lt;Stream&gt;"</param>
        /// <param name="leaveOpen">If false the stream is disposed</param>
        /// <returns>The font</returns>
        public static SvgFont GetOrCreateCreateFromStream(Stream s, String filename = null, bool leaveOpen = false)
            => new SvgFont(s, filename, leaveOpen);

        static String TryGetFontFile(String family, String style, String weight, params String[] extraFontFolders)
        {
            var ranker = new Search.SimpleTextSearch().CreateRanker(String.Join(' ', family, style, weight));
            //  A file must contain (a word of) the family name to be a candidate (the rank is greater than zero if any search word is found)
            var familyRanker = new Search.SimpleTextSearch().CreateRanker(family ?? "");
            double best = 0;
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
                    if (familyRanker.Rank(fn) <= 0)
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


        /// <summary>
        /// Find a font file by (fuzzy) matching "family style weight" against the font file names (without extension) in the extra folders,
        /// the folders added with <see cref="AddFontPath"/> and the system font folder, and load it (see <see cref="GetOrCreate(string)"/>).
        /// The result (also a miss) is cached per family, style and weight (regardless of the folders) until <see cref="ClearCache"/> is called.
        /// </summary>
        /// <param name="family">The font family</param>
        /// <param name="style">The font style, ex: "italic", may be null</param>
        /// <param name="weight">The font weight, ex: "bold", may be null</param>
        /// <param name="extraFontFolders">Additional folders to search first (null entries are ignored)</param>
        /// <returns>The best matching font (the file with the highest rank among the files whose name contains a word of <paramref name="family"/>),
        /// or null if no such font file was found (callers choose their own fallback, ex: <see cref="SvgCanvas.RasterizeText"/> uses <see cref="MontserratBlack"/>)</returns>
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


        /// <summary>
        /// Load a font file, the font is cached by its full path (until <see cref="ClearCache"/> is called)
        /// </summary>
        /// <param name="filename">The font file name</param>
        /// <returns>The font, null if the file doesn't exist</returns>
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


        /// <summary>
        /// Add a (global) folder to search for fonts, reference counted: the folder is searched until <see cref="RemoveFontPath"/> has been called as many times
        /// </summary>
        /// <param name="folder">The folder (case sensitive)</param>
        public static void AddFontPath(String folder)
            => Paths.IncValue(folder);

        /// <summary>
        /// Remove a folder added with <see cref="AddFontPath"/> (decrements the reference count)
        /// </summary>
        /// <param name="folder">The folder (case sensitive)</param>
        public static void RemoveFontPath(String folder)
            => Paths.DecValue(folder);

        static readonly ConcurrentCount<String> Paths = new ConcurrentCount<string>(StringComparer.Ordinal);




        /// <summary>
        /// Clear the cache of fonts loaded from files and of family lookups (the embedded fonts are not affected)
        /// </summary>
        public static void ClearCache()
        {
            var c = Cache;
            lock(c)
            {
                c.Clear();
            }
        }


        static readonly ConcurrentDictionary<String, SvgFont> Cache = new ConcurrentDictionary<string, SvgFont>(StringComparer.Ordinal);

        /// <summary>
        /// The quoted <see cref="Filename"/>
        /// </summary>
        /// <returns>The quoted file name</returns>
        public override string ToString() => Filename.ToQuoted();

        /// <summary>
        /// The file name of the font (the font name for embedded fonts, ex: "MontserratBlack", or "&lt;Stream&gt;")
        /// </summary>
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