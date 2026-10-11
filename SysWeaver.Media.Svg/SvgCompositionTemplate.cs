using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Compression;

namespace SysWeaver.Media
{



    /// <summary>
    /// A template that composes an svg image from layers of svg / bitmap files selected by variables (see <see cref="MakeComposite"/>).
    /// Template format (one item per line, lines starting with '#' are comments):
    /// an optional first line "WxH" with the size (default 256x256), then one line per layer with '@' separated parts:
    /// "x,y,w,h" the layer box (default: the whole image), "#[var]" a color template (applied to the gray scale colors of the svg, see <see cref="SvgCompositionTemplateCache"/>),
    /// a 3 char sizing code ("GGS": horizontal and vertical gravity '-' start, '*' center, '+' end, then sizing '-' fit, '*' stretch, '+' fill)
    /// and a comma separated list of file name templates ("[var]" is replaced by the variable value), the first file that can be resolved is used.
    /// File names starting with '$' are read using the alternative reader, other names are relative to the base path.
    /// </summary>
    public sealed partial class SvgCompositionTemplate
    {

        /// <summary>
        /// Load a template from a file (may be compressed, see <see cref="CompFile"/>)
        /// </summary>
        /// <param name="filename">The template file name</param>
        /// <param name="basePath">The base path of the layer files, null to use the folder of the template file</param>
        /// <returns>The template, null if the file wasn't found</returns>
        /// <exception cref="FormatException">The template is invalid</exception>
        public static SvgCompositionTemplate Load(String filename, String basePath = null)
        {
            var lines = CompFile.TryGetNonCommentLines(filename);
            if (lines == null)
                return null;
            basePath = basePath ?? Path.GetDirectoryName(Path.GetFullPath(filename));
            return Create(lines, basePath);
        }

        /// <summary>
        /// Create a template from lines of text (see the class description for the format, comments must already be removed)
        /// </summary>
        /// <param name="c">The lines, null or empty creates an empty 256x256 template</param>
        /// <param name="basePath">The base path of the layer files, null for the current directory</param>
        /// <returns>The template</returns>
        /// <exception cref="FormatException">A position doesn't have 4 numbers, or a layer has no file</exception>
        public static SvgCompositionTemplate Create(string[] c, String basePath = null)
        {
            var cl = c?.Length ?? 0;
            List<SvgLayer> layers = new List<SvgLayer>(cl);
            double width = 256;
            double height = 256;
            if (cl <= 0)
                return new SvgCompositionTemplate(width, height, layers.ToArray(), basePath);
            var ci = CultureInfo.InvariantCulture;
            int i = 0;
            var dim = c[i].Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (dim.Length == 2)
            {
                if (double.TryParse(dim[0], ci, out var w) && double.TryParse(dim[1], ci, out var h))
                {
                    width = w;
                    height = h;
                    ++i;
                }
            }
            var bme = BitmapExtensions;
            for (; i < cl; ++i)
            {
                var layerText = c[i];
                var layerParts = layerText.Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                double x = 0;
                double y = 0;
                double w = width;
                double h = height;
                TextTemplate color = null;
                String[] files = null;
                SvgFile[] bf = null;
                SvgCanvasGravity gravX = SvgCanvasGravity.Center;
                SvgCanvasGravity gravY = SvgCanvasGravity.Center;
                SvgCanvasSizing size = SvgCanvasSizing.Fit;
                foreach (var part in layerParts)
                {
                    var f = part[0];
                    if (f == '#')
                    {
                        color = new TextTemplate(part, "[", "]", true, false);
                        continue;
                    }
                    if (GravDecode.TryGetValue(f, out var gh))
                    {
                        if (part.Length == 3)
                        {
                            if (GravDecode.TryGetValue(part[1], out var gv))
                            {
                                if (SizeDecode.TryGetValue(part[2], out var ss))
                                {
                                    gravX = gh;
                                    gravY = gv;
                                    size = ss;
                                    continue;
                                }
                            }
                        }
                    }

                    if (Char.IsAsciiDigit(f) || (f == '-') | (f == '+'))
                    {
                        var p = part.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        if (p.Length != 4)
                            throw new FormatException("Expected 4 variables for position!");
                        x = double.Parse(p[0], ci);
                        y = double.Parse(p[1], ci);
                        w = double.Parse(p[2], ci);
                        h = double.Parse(p[3], ci);
                        continue;
                    }
                    files = part.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    var fl = files.Length;
                    bf = new SvgFile[fl];
                    for (var fi = 0; fi < fl; ++fi)
                    {
                        var fn = files[fi].RemoveQuotes();
                        var isBitMap = bme.Contains(Path.GetExtension(fn).FastToLower());
                        var tt = new TextTemplate(fn, "[", "]", true, false);
                        bf[fi] = new SvgFile(tt.Vars.Select(x => x.FastToLower()).ToArray(), tt, isBitMap);
                    }
                }
                if ((bf?.Length ?? 0) <= 0)
                    throw new FormatException("Expected at least one file!");
                layers.Add(new SvgLayer(x, y, w, h, bf, color, gravX, gravY, size));
            }
            return new SvgCompositionTemplate(width, height, layers.ToArray(), basePath);
        }

        static readonly IReadOnlyDictionary<Char, SvgCanvasGravity> GravDecode = new Dictionary<Char, SvgCanvasGravity>()
        {
            { '-', SvgCanvasGravity.Start },
            { '*', SvgCanvasGravity.Center },
            { '+', SvgCanvasGravity.End },
        }.Freeze();

        static readonly IReadOnlyDictionary<Char, SvgCanvasSizing> SizeDecode = new Dictionary<Char, SvgCanvasSizing>()
        {
            { '-', SvgCanvasSizing.Fit },
            { '*', SvgCanvasSizing.Stretch },
            { '+', SvgCanvasSizing.Fill },
        }.Freeze();


        static readonly IReadOnlySet<String> BitmapExtensions = ReadOnlyData.Set(StringComparer.Ordinal,
            ".png",
            ".jpg",
            ".jpeg",
            ".tiff"
            );


        /// <summary>
        /// The width of the composed image
        /// </summary>
        public readonly double Width;
        /// <summary>
        /// The height of the composed image
        /// </summary>
        public readonly double Height;
        /// <summary>
        /// The base path of the layer files (empty for the current directory)
        /// </summary>
        public readonly String BasePath;
        readonly SvgLayer[] Layers;
#if DEBUG
        public override string ToString() => String.Concat(Width.ToValueString(), 'x', Height.ToValueString(), " with ", Layers.Length, " layers");
#endif//DEBUG

        SvgCompositionTemplate(double width, double height, SvgLayer[] layers, String basePath)
        {
            Width = width;
            Height = height;
            Layers = layers;
            BasePath = basePath ?? "";
        }


        /// <summary>
        /// Compose an svg image: for each layer, the first file whose variables all have a non-empty value (and that can be loaded) is added.
        /// Svg files get their text elements converted to paths (fonts are also searched in the base path and its "Fonts" sub folder, and in the folder of the svg file),
        /// bitmaps are embedded as data uris.
        /// </summary>
        /// <param name="vars">The variable values, the keys must be lower case</param>
        /// <param name="title">An optional title element</param>
        /// <param name="altReader">The reader for file names starting with '$', may be null if no such files are used</param>
        /// <param name="cache">The file cache to use, null for <see cref="SvgCompositionTemplateCache.Default"/></param>
        /// <returns>The svg document</returns>
        public async Task<String> MakeComposite(IReadOnlyDictionary<String, String> vars, String title = null, SvgTools.FileReader altReader = null, SvgCompositionTemplateCache cache = null)
        {
            cache = cache ?? SvgCompositionTemplateCache.Default;
            var svg = new SvgCanvas(Width, Height);
            if (!String.IsNullOrEmpty(title))
            {
                var titleE = svg.CreateElement("title");
                titleE.Value = title;
                svg.Svg.AddFirst(titleE);
            }

            var folders = new HashSet<String>(StringComparer.Ordinal);
            var bp = BasePath;
            var bpf = Path.GetFullPath(String.IsNullOrEmpty(bp) ? "." : bp);
            var fontPath = Path.Combine(bpf, "Fonts");
            try
            {
                SvgFont.AddFontPath(bpf);
                SvgFont.AddFontPath(fontPath);
                foreach (var layer in Layers)
                {
                    foreach (var f in layer.Files)
                    {
                        bool ok = true;
                        foreach (var v in f.Vars)
                        {
                            ok &= vars.TryGetValue(v, out var vv) && !String.IsNullOrEmpty(vv);
                            if (!ok)
                                break;
                        }
                        if (!ok)
                            continue;
                        if (f.IsBitmap)
                        {
                            var res = await cache.GetBitmapFile(f.NameTemplate, vars, bp, altReader).ConfigureAwait(false);
                            if (res != null)
                            {
                                svg.Image(res, layer.X, layer.Y, layer.Width, layer.Height, true, layer.Size, layer.GravX, layer.GravY);
                                break;
                            }
                        }
                        else
                        {
                            var filename = f.NameTemplate.Get(k => vars.TryGetValue(k.FastToLower(), out var v) ? v : null);
                            var color = layer.Color?.Get(k => vars.TryGetValue(k.FastToLower(), out var v) ? v : null);
                            if (color != null)
                                if (color.FastStartsWith("##"))
                                    color = color.Substring(1);
                            var res = await cache.GetResolvedSvgFile(filename, vars, bp, altReader, color).ConfigureAwait(false);
                            if (res != null)
                            {
                                String fp2 = null;
                                var other = SvgCanvas.Create(res);
                                try
                                {
                                    if (filename[0] != '$')
                                    {
                                        fp2 = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(bp, filename)));
                                        SvgFont.AddFontPath(fp2);

                                    }
                                    other.RasterizeText();
                                }
                                finally
                                {
                                    if (fp2 != null)
                                        SvgFont.RemoveFontPath(fp2);
                                }
                                svg.EmbeddSvg(other, layer.X, layer.Y, layer.Width, layer.Height, true, layer.Size, layer.GravX, layer.GravY);
                                /*                            try
                                                            {
                                                                svg.EmbeddSvg(res, layer.X, layer.Y, layer.Width, layer.Height);
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                svg.EmbeddSvg(res, layer.X, layer.Y, layer.Width, layer.Height);
                                                            }*/
                                break;
                            }
                        }
                    }
                }
            }
            finally
            {
                SvgFont.RemoveFontPath(bpf);
                SvgFont.RemoveFontPath(fontPath);

            }
            return svg.ToSvgString();
        }
    
    }

}