using SkiaSharp;
using Svg;
using Svg.Skia;
using Svg.Skia.TypefaceProviders;
using System;
using System.IO;
using System.Text;

namespace SysWeaver.Media
{
    /// <summary>
    /// Renders an svg document to png / jpeg images using Svg.Skia (SkiaSharp).
    /// The svg is loaded once (in the constructor) and can then be rendered at different sizes.
    /// A requested width / height of zero or less uses the svg size (rounded up) with a scale of 1 for that axis, each axis is scaled independently
    /// (so only setting the width stretches the image horizontally).
    /// </summary>
    public sealed class SvgBitmapRenderer : IDisposable
    {

        /// <summary>
        /// Render an svg document to a png image (rgba, transparent background)
        /// </summary>
        /// <param name="svg">The svg document</param>
        /// <param name="width">The image width, zero or less to use the svg width (each axis is scaled independently)</param>
        /// <param name="height">The image height, zero or less to use the svg height (each axis is scaled independently)</param>
        /// <returns>The png file data</returns>
        /// <exception cref="InvalidDataException">The svg can't be loaded (ex: the root element isn't an svg element)</exception>
        /// <exception cref="System.Xml.XmlException">The data is not valid xml</exception>
        public static Byte[] CreatePng(String svg, int width = 0, int height = 0)
        {
            var mem = Encoding.UTF8.GetBytes(svg);
            using var s = new SKSvg();
            using (var ms = new MemoryStream(mem))
                Load(s, ms);
            var c = s.Picture.CullRect;
            GetScale(out var scaleX, out var scaleY, ref width, ref height, c.Width, c.Height);
            using var msd = new MemoryStream(width * height * 4);
            s.Picture.ToImage(msd, SKColors.Empty, SKEncodedImageFormat.Png, 100, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Unpremul, Cs);
            return msd.ToArray();
        }


        /// <summary>
        /// The last typeface provider used by Svg.Skia (after the system font manager and the default typeface), only used if the font family isn't installed.
        /// The SkiaSharp Linux native library (without dependencies) has no system font manager: without this text elements would not be drawn at all.
        /// Uses the font files found by <see cref="SvgFont.GetOrCreate(string, string, string, string[])"/> (font folders added with
        /// <see cref="SvgFont.AddFontPath(string)"/> and the system font folder), else the embedded <see cref="SvgFont.MontserratBlack"/> font (like <see cref="SvgCanvas.RasterizeText"/>).
        /// </summary>
        sealed class FallbackTypefaceProvider : ITypefaceProvider
        {
            public static readonly FallbackTypefaceProvider Instance = new FallbackTypefaceProvider();

            public SKTypeface FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
            {
                var weight = (int)fontWeight >= (int)SKFontStyleWeight.SemiBold ? "bold" : null;
                var style = fontStyle == SKFontStyleSlant.Upright ? null : "italic";
                if (!String.IsNullOrWhiteSpace(fontFamily))
                {
                    foreach (var f in fontFamily.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var family = f.Trim('\'', '"');
                        if (family.Length <= 0)
                            continue;
                        var font = SvgFont.GetOrCreate(family, style, weight);
                        if (font == null)
                            continue;
                        //  Read the data (SKTypeface.FromFile keeps the file open)
                        using var data = SKData.CreateCopy(File.ReadAllBytes(font.Filename));
                        var tf = SKTypeface.FromData(data);
                        if (tf != null)
                            return tf;
                    }
                }
                return SKTypeface.FromData(Fallback.Value);
            }

            static readonly Lazy<SKData> Fallback = new Lazy<SKData>(() => SKData.CreateCopy(SvgFont.GetEmbeddedData(nameof(SvgFont.MontserratBlack))));
        }

        static void Load(SKSvg s, Stream data)
        {
            s.Settings.TypefaceProviders?.Add(FallbackTypefaceProvider.Instance);
            try
            {
                if ((s.Load(data) != null) && (s.Picture != null))
                    return;
            }
            catch (NullReferenceException ex)
            {
                //  Svg.Skia throws a NullReferenceException if the root element isn't an svg element
                throw new InvalidDataException("Failed to load SVG, the root element must be an svg element", ex);
            }
            throw new InvalidDataException("Failed to load SVG");
        }

        static void GetScale(out float scaleX, out float scaleY, ref int width, ref int height, double svgWidth, double svgHeight)
        {
            scaleX = 1;
            scaleY = 1;
            if (width > 0)
                scaleX = (float)((Decimal)width / (Decimal)svgWidth);
            else
                width = (int)Math.Ceiling(svgWidth);
            if (height > 0)
                scaleY = (float)((Decimal)height / (Decimal)svgHeight);
            else
                height = (int)Math.Ceiling(svgHeight);
        }

        void GetScale(out float scaleX, out float scaleY, ref int width, ref int height)
            => GetScale(out scaleX, out scaleY, ref width, ref height, Width, Height);

        /// <summary>
        /// Render to a png image (rgba, transparent background) and write it to a stream
        /// </summary>
        /// <param name="s">The stream to write to</param>
        /// <param name="width">The image width, zero or less to use the svg width</param>
        /// <param name="height">The image height, zero or less to use the svg height</param>
        /// <param name="leaveOpen">If false the stream is disposed</param>
        public void ToPng(Stream s, int width = 0, int height = 0, bool leaveOpen = false)
        {
            GetScale(out var scaleX, out var scaleY, ref width, ref height);
            using var _ = leaveOpen ? null : s;
            Svg.Picture.ToImage(s, SKColors.Empty, SKEncodedImageFormat.Png, 100, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Unpremul, Cs);
        }

        /// <summary>
        /// Render to a png image (rgba, transparent background)
        /// </summary>
        /// <param name="width">The image width, zero or less to use the svg width</param>
        /// <param name="height">The image height, zero or less to use the svg height</param>
        /// <returns>The png file data</returns>
        public Memory<Byte> ToPng(int width = 0, int height = 0)
        {
            GetScale(out var scaleX, out var scaleY, ref width, ref height);
            using var ms = new MemoryStream(width * height * 4);
            Svg.Picture.ToImage(ms, SKColors.Empty, SKEncodedImageFormat.Png, 100, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Unpremul, Cs);
            return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Position);
        }


        /// <summary>
        /// Render to a jpeg image and write it to a stream
        /// </summary>
        /// <param name="s">The stream to write to</param>
        /// <param name="width">The image width, zero or less to use the svg width</param>
        /// <param name="height">The image height, zero or less to use the svg height</param>
        /// <param name="leaveOpen">If false the stream is disposed</param>
        /// <param name="quality">The jpeg quality [0, 100]</param>
        public void ToJpeg(Stream s, int width = 0, int height = 0, bool leaveOpen = false, int quality = 70)
        {
            GetScale(out var scaleX, out var scaleY, ref width, ref height);
            using var _ = leaveOpen ? null : s;
            Svg.Picture.ToImage(s, SKColors.Empty, SKEncodedImageFormat.Jpeg, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Unpremul, Cs);
        }

        /// <summary>
        /// Render to a jpeg image
        /// </summary>
        /// <param name="width">The image width, zero or less to use the svg width</param>
        /// <param name="height">The image height, zero or less to use the svg height</param>
        /// <param name="quality">The jpeg quality [0, 100]</param>
        /// <returns>The jpeg file data</returns>
        public Memory<Byte> ToJpeg(int width = 0, int height = 0, int quality = 70)
        {
            GetScale(out var scaleX, out var scaleY, ref width, ref height);
            using var ms = new MemoryStream(width * height * 4);
            Svg.Picture.ToImage(ms, SKColors.Empty, SKEncodedImageFormat.Jpeg, quality, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Unpremul, Cs);
            return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Position);
        }

        static readonly SKColorSpace Cs = SKColorSpace.CreateSrgb();

        readonly SKSvg Svg = new SKSvg();
        /// <summary>
        /// The left position of the svg picture bounds (cull rect)
        /// </summary>
        public readonly double X;
        /// <summary>
        /// The top position of the svg picture bounds (cull rect)
        /// </summary>
        public readonly double Y;
        /// <summary>
        /// The width of the svg picture bounds (cull rect)
        /// </summary>
        public readonly double Width;
        /// <summary>
        /// The height of the svg picture bounds (cull rect)
        /// </summary>
        public readonly double Height;

        /// <summary>
        /// Release the loaded svg
        /// </summary>
        public void Dispose()
        {
            Svg.Dispose();
        }

        /// <summary>
        /// Load an svg document from a stream
        /// </summary>
        /// <param name="svgData">The stream with the svg document</param>
        /// <param name="leaveOpen">If false the stream is disposed</param>
        /// <exception cref="InvalidDataException">The svg can't be loaded (ex: the root element isn't an svg element)</exception>
        /// <exception cref="System.Xml.XmlException">The data is not valid xml</exception>
        public SvgBitmapRenderer(Stream svgData, bool leaveOpen = false)
        {
            using var _ = leaveOpen ? null : svgData;
            var s = Svg;
            Load(s, svgData);
            var vp = s.Picture.CullRect;
            X = vp.Left;
            Y = vp.Top;
            Width = vp.Width;
            Height = vp.Height;
        }

        /// <summary>
        /// Load an svg document (utf-8)
        /// </summary>
        /// <param name="svgData">The svg document</param>
        /// <exception cref="InvalidDataException">The svg can't be loaded (ex: the root element isn't an svg element)</exception>
        /// <exception cref="System.Xml.XmlException">The data is not valid xml</exception>
        public SvgBitmapRenderer(Byte[] svgData)
        {
            using var ms = new MemoryStream(svgData);
            var s = Svg;
            Load(s, ms);
            var vp = s.Picture.CullRect;
            X = vp.Left;
            Y = vp.Top;
            Width = vp.Width;
            Height = vp.Height;
        }

        /// <summary>
        /// Load an svg document (utf-8)
        /// </summary>
        /// <param name="svgData">The svg document</param>
        /// <exception cref="InvalidDataException">The svg can't be loaded (ex: the root element isn't an svg element)</exception>
        /// <exception cref="System.Xml.XmlException">The data is not valid xml</exception>
        public unsafe SvgBitmapRenderer(ReadOnlyMemory<Byte> svgData)
        {
            using var p = svgData.Pin();
            using var ms = new UnmanagedMemoryStream((Byte*)p.Pointer, svgData.Length);
            var s = Svg;
            Load(s, ms);
            var vp = s.Picture.CullRect;
            X = vp.Left;
            Y = vp.Top;
            Width = vp.Width;
            Height = vp.Height;
        }


    }



}