using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Data;

namespace SysWeaver.Media
{
    public sealed class SvgCompositionTemplateCache
    {

        public void Clear()
        {
            SvgCache.Clear();
            BitmapCache.Clear();
        }

        public static readonly SvgCompositionTemplateCache Default = new SvgCompositionTemplateCache();

        public SvgCompositionTemplateCache(int keepTemplatesInMemoryForMinutes = 8 * 60)
        {
            Keep = TimeSpan.FromMinutes(Math.Max(1, keepTemplatesInMemoryForMinutes));
        }

        public readonly TimeSpan Keep;


        public ValueTask<String> GetResolvedSvgFile(TextTemplate nameTemplate, IReadOnlyDictionary<String, String> vars, String basePath = null, Func<String, ValueTask<ReadOnlyMemory<Byte>>> altReader = null, String color = null)
            => GetResolvedSvgFile(nameTemplate.Get(k => vars.TryGetValue(k.FastToLower(), out var v) ? v : null), vars, basePath, altReader, color);

        public async ValueTask<String> GetResolvedSvgFile(String name, IReadOnlyDictionary<String, String> vars, String basePath = null, Func<String, ValueTask<ReadOnlyMemory<Byte>>> altReader = null, String color = null)
        {
            var svgTemp = await SvgCache.GetOrUpdateValueAsync(name, n => LoadSvgFile(n, basePath, altReader)).ConfigureAwait(false);
            if (svgTemp == null)
                return null;
            var cols = color == null ? DefColorValue : GetColorVars(color);
            return svgTemp.Get(k =>
            {
                var key = k.FastToLower();
                if (vars.TryGetValue(key, out var v))
                    return v;
                if (cols.TryGetValue(key, out v))
                    return v;
                return null;
            });
        }

        public ValueTask<String> GetBitmapFile(TextTemplate nameTemplate, IReadOnlyDictionary<String, String> vars, String basePath = null, Func<String, ValueTask<ReadOnlyMemory<Byte>>> altReader = null)
            => GetBitmapFile(nameTemplate.Get(k => vars.TryGetValue(k.FastToLower(), out var v) ? v : null), basePath, altReader);

        public ValueTask<String> GetBitmapFile(String name, String basePath = null, Func<String, ValueTask<ReadOnlyMemory<Byte>>> altReader = null)
            => BitmapCache.GetOrUpdateValueAsync(name, n => LoadBitmapFile(n, basePath, altReader));


        async ValueTask<TextTemplate> LoadSvgFile(String name, String basePath, Func<String, ValueTask<ReadOnlyMemory<Byte>>> altReader)
        {
            String svg;
            if (name[0] == '$')
            {
                var res = await altReader(name.Substring(1)).ConfigureAwait(false);
                if (res.IsEmpty)
                    return null;
                svg = Encoding.UTF8.GetString(res.Span);
            }
            else
            {
                svg = CompFile.TryGetAllText(Path.Combine(basePath ?? "", name));
            }
            if (svg == null)
                return null;
            svg = TextTemplate.SearchAndReplace(svg, ColorRep, true, false);
            return new TextTemplate(svg, "[", "]", true);
        }

        static readonly IReadOnlyDictionary<String, String> DefColorValue = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            { "Col1", "#111" },
            { "Col2", "#222" },
            { "Col3", "#333" },
            { "Col4", "#444" },
            { "Col5", "#555" },
            { "Col6", "#666" },
            { "Col7", "#777" },
            { "Col8", "#888" },
            { "Col9", "#999" },
            { "Col10", "#aaa" },
            { "Col11", "#bbb" },
            { "Col12", "#ccc" },
            { "Col13", "#ddd" },
            { "Col14", "#eee" },
            { "Col15", "#fff" },
        }.Freeze();

        static readonly IReadOnlyDictionary<String, String> ColorRep = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            { "\"#111\"", "\"[Col1]\"" },
            { "\"#222\"", "\"[Col2]\"" },
            { "\"#333\"", "\"[Col3]\"" },
            { "\"#444\"", "\"[Col4]\"" },
            { "\"#555\"", "\"[Col5]\"" },
            { "\"#666\"", "\"[Col6]\"" },
            { "\"#777\"", "\"[Col7]\"" },
            { "\"#888\"", "\"[Col8]\"" },
            { "\"#999\"", "\"[Col9]\"" },
            { "\"#aaa\"", "\"[Col10]\"" },
            { "\"#bbb\"", "\"[Col11]\"" },
            { "\"#ccc\"", "\"[Col12]\"" },
            { "\"#ddd\"", "\"[Col13]\"" },
            { "\"#eee\"", "\"[Col14]\"" },
            { "\"#fff\"", "\"[Col15]\"" },
        }.Freeze();

        async ValueTask<String> LoadBitmapFile(String name, String basePath, Func<String, ValueTask<ReadOnlyMemory<Byte>>> altReader)
        {
            var ext = name.Substring(name.LastIndexOf('.'));
            var mime = MimeTypeMap.GetMimeType(ext)?.Item1;
            String data = String.Concat("data:", mime, ";base64,");
            if (name[0] == '$')
            {
                var res = await altReader(name.Substring(1)).ConfigureAwait(false);
                if (res.IsEmpty)
                    return null;
                data += Convert.ToBase64String(res.Span);
            }
            else
            {
                name = Path.Combine(basePath ?? "", name);
                if (!File.Exists(name))
                    return null;
                using var mem = FileReadOnlyMemory.Read(name);
                data += Convert.ToBase64String(mem.Memory.Span);
            }
            return data;
        }

        public IEnumerable<Stats> GetStats(String system, String prefix = "")
        {
            foreach (var x in SvgCache.GetStats(system, prefix + "Svg."))
                yield return x;
            foreach (var x in BitmapCache.GetStats(system, prefix + "Bitmap."))
                yield return x;
            var svgSize = SvgCache.Sum(x => (long)(x.Item3?.Template?.Length ?? 0)) * 2;
            var bitmapSize = BitmapCache.Sum(x => (long)(x.Item3?.Length ?? 0)) * 2;
            var ba = TableDataByteSizeAttribute.Instance;
            yield return new Stats(system, prefix + "SvgSize", svgSize, "Approximate number of bytes in svg cache", ba);
            yield return new Stats(system, prefix + "BitmapSize", bitmapSize, "Approximate number of bytes in the bitmap cache", ba);
            yield return new Stats(system, prefix + "Size", svgSize + bitmapSize, "Approximate number of bytes in the caches", ba);
        }

        readonly FastMemCache<String, TextTemplate> SvgCache = new(TimeSpan.FromHours(8), StringComparer.Ordinal);
        readonly FastMemCache<String, String> BitmapCache = new(TimeSpan.FromHours(1), StringComparer.Ordinal);
        static readonly FastMemCache<String, IReadOnlyDictionary<String, String>> ColorCache = new(TimeSpan.FromHours(1), StringComparer.Ordinal);


        internal static IReadOnlyDictionary<String, String> GetColorVars(String color) =>
            ColorCache.GetOrUpdate(color, MakeColors);

        static IReadOnlyDictionary<String, String> MakeColors(String color)
        {
            var colors = new Dictionary<String, String>(15, StringComparer.Ordinal);
            HtmlColors.ParseHtmlColor(color, out var r, out var g, out var b, out var a);
            for (int i = 1; i <= 15; ++i)
            {
                var rr = ((r * i) + 7) / 15;
                var gg = ((g * i) + 7) / 15;
                var bb = ((b * i) + 7) / 15;
                colors.Add("col" + i, HtmlColors.MakeHtmlColor(rr, gg, bb, a));
            }
            return colors.Freeze();
        }

    }


}