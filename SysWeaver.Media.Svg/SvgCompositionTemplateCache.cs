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
    /// <summary>
    /// A cache of the files used by <see cref="SvgCompositionTemplate"/>: svg files (as templates) and bitmaps (as data uris).
    /// Files are cached by their full path (base path + name), or for names starting with '$' by the name and the reader (compared with <see cref="Delegate.Equals(object)"/>, same target and method);
    /// svg files for <see cref="Keep"/>, bitmaps for 1 hour.
    /// In svg files, the double quoted gray colors "#111", "#222" .. "#fff" are replaced by the variables [Col1] .. [Col15]:
    /// without a color they get their original value, with a color they are scaled: Col<i>i</i> = color * i / 15 (rounded).
    /// </summary>
    public sealed class SvgCompositionTemplateCache
    {

        /// <summary>
        /// Remove all cached files
        /// </summary>
        public void Clear()
        {
            SvgCache.Clear();
            BitmapCache.Clear();
        }

        /// <summary>
        /// The default (shared) cache
        /// </summary>
        public static readonly SvgCompositionTemplateCache Default = new SvgCompositionTemplateCache();

        /// <summary>
        /// Create a cache
        /// </summary>
        /// <param name="keepTemplatesInMemoryForMinutes">How long svg files are kept in the cache (at least 1 minute)</param>
        public SvgCompositionTemplateCache(int keepTemplatesInMemoryForMinutes = 8 * 60)
        {
            Keep = TimeSpan.FromMinutes(Math.Max(1, keepTemplatesInMemoryForMinutes));
            SvgCache = new(Keep);
        }

        /// <summary>
        /// How long svg files are kept in the cache
        /// </summary>
        public readonly TimeSpan Keep;


        /// <summary>
        /// Get an svg file with the variables (and colors) resolved, see <see cref="GetResolvedSvgFile(string, IReadOnlyDictionary{string, string}, string, SvgTools.FileReader, string)"/>
        /// </summary>
        /// <param name="nameTemplate">The file name template, the variables are resolved using <paramref name="vars"/> (lower cased keys)</param>
        /// <param name="vars">The variable values, the keys must be lower case</param>
        /// <param name="basePath">The base path of the file</param>
        /// <param name="altReader">The reader for file names starting with '$'</param>
        /// <param name="color">An optional color for the gray scale colors</param>
        /// <returns>The svg document, null if not found</returns>
        public Task<String> GetResolvedSvgFile(TextTemplate nameTemplate, IReadOnlyDictionary<String, String> vars, String basePath = null, SvgTools.FileReader altReader = null, String color = null)
            => GetResolvedSvgFile(nameTemplate.Get(k => vars.TryGetValue(k.FastToLower(), out var v) ? v : null), vars, basePath, altReader, color);

        /// <summary>
        /// Get an svg file (cached as a template) with the [var] variables resolved using <paramref name="vars"/> and the gray scale colors replaced
        /// </summary>
        /// <param name="name">The file name, relative to <paramref name="basePath"/>, or "$name" to read it using <paramref name="altReader"/> (must return the svg mime type)</param>
        /// <param name="vars">The variable values, the keys must be lower case</param>
        /// <param name="basePath">The base path of the file</param>
        /// <param name="altReader">The reader for file names starting with '$'</param>
        /// <param name="color">An optional color for the gray scale colors, null to keep the original gray colors</param>
        /// <returns>The svg document, null if not found</returns>
        public async Task<String> GetResolvedSvgFile(String name, IReadOnlyDictionary<String, String> vars, String basePath = null, SvgTools.FileReader altReader = null, String color = null)
        {
            var svgTemp = await SvgCache.GetOrUpdateAsync(GetKey(name, basePath, altReader), LoadSvgFile).ConfigureAwait(false);
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

        /// <summary>
        /// Get a bitmap file as a data uri, see <see cref="GetBitmapFile(string, string, SvgTools.FileReader)"/>
        /// </summary>
        /// <param name="nameTemplate">The file name template, the variables are resolved using <paramref name="vars"/> (lower cased keys)</param>
        /// <param name="vars">The variable values, the keys must be lower case</param>
        /// <param name="basePath">The base path of the file</param>
        /// <param name="altReader">The reader for file names starting with '$'</param>
        /// <returns>The data uri, null if not found</returns>
        public ValueTask<String> GetBitmapFile(TextTemplate nameTemplate, IReadOnlyDictionary<String, String> vars, String basePath = null, SvgTools.FileReader altReader = null)
            => GetBitmapFile(nameTemplate.Get(k => vars.TryGetValue(k.FastToLower(), out var v) ? v : null), basePath, altReader);

        /// <summary>
        /// Get a bitmap file as a data uri ("data:mime;base64,..."), the mime type is taken from the file extension.
        /// The result is cached for 1 hour (by the full path, or the name and the reader for names starting with '$').
        /// </summary>
        /// <param name="name">The file name (must have an extension), relative to <paramref name="basePath"/>, or "$name" to read it using <paramref name="altReader"/></param>
        /// <param name="basePath">The base path of the file</param>
        /// <param name="altReader">The reader for file names starting with '$'</param>
        /// <returns>The data uri, null if not found</returns>
        public ValueTask<String> GetBitmapFile(String name, String basePath = null, SvgTools.FileReader altReader = null)
            => BitmapCache.GetOrUpdateAsync(GetKey(name, basePath, altReader), LoadBitmapFile);

        /// <summary>
        /// The cache key of a file: the full path of a file, or a name starting with '$' and the reader
        /// </summary>
        /// <param name="Name">The full path or the name starting with '$'</param>
        /// <param name="Reader">The reader for names starting with '$', else null</param>
        readonly record struct FileKey(String Name, SvgTools.FileReader Reader);

        static FileKey GetKey(String name, String basePath, SvgTools.FileReader altReader)
            => name[0] == '$' ? new FileKey(name, altReader) : new FileKey(Path.GetFullPath(Path.Combine(basePath ?? "", name)), null);

        static async Task<TextTemplate> LoadSvgFile(FileKey key)
        {
            String svg;
            var name = key.Name;
            if (name[0] == '$')
            {
                var res = await key.Reader(name.Substring(1)).ConfigureAwait(false);
                if (res.Item1.IsEmpty)
                    return null;
                if (!res.Item2.FastEquals(MimeTypeMap.Svg))
                    return null;
                svg = Encoding.UTF8.GetString(res.Item1.Span);
            }
            else
            {
                svg = CompFile.TryGetAllText(name);
            }
            if (svg == null)
                return null;
            svg = TextTemplate.SearchAndReplace(svg, ColorRep, true, false);
            return new TextTemplate(svg, "[", "]", true);
        }

        //  Case insensitive: the keys are looked up lower cased
        static readonly IReadOnlyDictionary<String, String> DefColorValue = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
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

        static async Task<String> LoadBitmapFile(FileKey key)
        {
            var name = key.Name;
            var ext = name.Substring(name.LastIndexOf('.'));
            var mime = MimeTypeMap.GetMimeType(ext)?.Item1;
            if (name[0] == '$')
            {
                var res = await key.Reader(name.Substring(1)).ConfigureAwait(false);
                if (res.Item1.IsEmpty)
                    return null;
                if (mime.Contains("charset="))
                    return String.Concat("data:", mime, ",", Encoding.UTF8.GetString(res.Item1.Span));
                return String.Concat("data:", mime, ";base64,", Convert.ToBase64String(res.Item1.Span));
            }
            else
            {
                if (!File.Exists(name))
                    return null;
                using var mem = FileReadOnlyMemory.Read(name);
                if (mime.Contains("charset="))
                    return String.Concat("data:", mime, ",", Encoding.UTF8.GetString(mem.Memory.Span));
                return String.Concat("data:", mime, ";base64,", Convert.ToBase64String(mem.Memory.Span));
            }
        }

        /// <summary>
        /// Get statistics: the cache statistics of the svg ("Svg.") and bitmap ("Bitmap.") caches, followed by the approximate sizes in bytes
        /// ("SvgSize", "BitmapSize" and "Size")
        /// </summary>
        /// <param name="system">The system name of the stats</param>
        /// <param name="prefix">A prefix for the stat names</param>
        /// <returns>The statistics</returns>
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

        readonly FastMemCache<FileKey, TextTemplate> SvgCache;
        readonly FastMemCache<FileKey, String> BitmapCache = new(TimeSpan.FromHours(1));
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