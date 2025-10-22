using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
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
            using var s = asm.GetManifestResourceStream(pre + name + ".ttf.br");
            using var d = new BrotliStream(s, CompressionMode.Decompress);
            return GetOrCreateCreateFromStream(d, name, true);
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

        static readonly ConcurrentDictionary<String, SvgFont> Cache = new ConcurrentDictionary<string, SvgFont>(StringComparer.Ordinal);

        public override string ToString() => Filename.ToQuoted();

        public readonly String Filename;

        SvgFont(String filename)
        {
            Filename = filename;
            using (var s = new FileStream(filename, FileMode.Open))
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