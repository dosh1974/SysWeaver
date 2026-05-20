using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Compression;

namespace SysWeaver.Media
{



    public sealed partial class SvgCompositionTemplate
    {

        public static SvgCompositionTemplate Load(String filename, String basePath = null)
        {
            var lines = CompFile.TryGetNonCommentLines(filename);
            if (lines == null)
                return null;
            basePath = basePath ?? Path.GetDirectoryName(Path.GetFullPath(filename));
            return Create(lines, basePath);
        }

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
                            throw new Exception("Expected 4 variables for position!");
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
                    throw new Exception("Expected at least one file!");
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


        public readonly double Width;
        public readonly double Height;
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


        public async ValueTask<String> MakeComposite(IReadOnlyDictionary<String, String> vars, String title = null, Func<String, ValueTask<ReadOnlyMemory<Byte>>> altReader = null, SvgCompositionTemplateCache cache = null)
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
            var bpf = Path.GetFullPath(bp);
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
                            ok &= !String.IsNullOrEmpty(vars[v]);
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