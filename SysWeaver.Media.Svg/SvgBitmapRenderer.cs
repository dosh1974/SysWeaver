using SkiaSharp;
using Svg;
using Svg.Skia;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace SysWeaver.Media
{
    public sealed class SvgBitmapRenderer : IDisposable
    {
        
        public static Byte[] CreatePng(String svg, int width = 0, int height = 0)
        {
            var mem = Encoding.UTF8.GetBytes(svg);
            using var s = new SKSvg();
            using (var ms = new MemoryStream(mem))
                if (s.Load(ms) == null)
                    throw new Exception("Failed to load SVG");
            var c = s.Picture.CullRect;
            GetScale(out var scaleX, out var scaleY, ref width, ref height, c.Width, c.Height);
            using var msd = new MemoryStream(width * height * 4);
            s.Picture.ToImage(msd, SKColors.Empty, SKEncodedImageFormat.Png, 100, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Unpremul, Cs);
            return msd.ToArray();
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

        public void ToPng(Stream s, int width = 0, int height = 0, bool leaveOpen = false)
        {
            GetScale(out var scaleX, out var scaleY, ref width, ref height);
            using var _ = leaveOpen ? null : s;
            Svg.Picture.ToImage(s, SKColors.Empty, SKEncodedImageFormat.Png, 100, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Unpremul, Cs);
        }

        public Memory<Byte> ToPng(int width = 0, int height = 0)
        {
            GetScale(out var scaleX, out var scaleY, ref width, ref height);
            using var ms = new MemoryStream(width * height * 4);
            Svg.Picture.ToImage(ms, SKColors.Empty, SKEncodedImageFormat.Png, 100, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Unpremul, Cs);
            return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Position);
        }


        static readonly SKColorSpace Cs = SKColorSpace.CreateSrgb();

        readonly SKSvg Svg = new SKSvg();
        public readonly double X;
        public readonly double Y;
        public readonly double Width;
        public readonly double Height;

        public void Dispose()
        {
            Svg.Dispose();
        }

        public SvgBitmapRenderer(Stream svgData, bool leaveOpen = false)
        {
            using var _ = leaveOpen ? null : svgData;
            var s = Svg;
            if (s.Load(svgData) == null)
                throw new Exception("Failed to load SVG");
            var vp = s.Picture.CullRect;
            X = vp.Left;
            Y = vp.Top;
            Width = vp.Width;
            Height = vp.Height;
        }

        public SvgBitmapRenderer(Byte[] svgData)
        {
            using var ms = new MemoryStream(svgData);
            var s = Svg;
            if (s.Load(ms) == null)
                throw new Exception("Failed to load SVG");
            var vp = s.Picture.CullRect;
            X = vp.Left;
            Y = vp.Top;
            Width = vp.Width;
            Height = vp.Height;
        }

        public unsafe SvgBitmapRenderer(ReadOnlyMemory<Byte> svgData)
        {
            using var p = svgData.Pin();
            using var ms = new UnmanagedMemoryStream((Byte*)p.Pointer, svgData.Length);
            var s = Svg;
            if (s.Load(ms) == null)
                throw new Exception("Failed to load SVG");
            var vp = s.Picture.CullRect;
            X = vp.Left;
            Y = vp.Top;
            Width = vp.Width;
            Height = vp.Height;
        }


    }



}