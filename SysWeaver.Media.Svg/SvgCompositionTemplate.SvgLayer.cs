using System;

namespace SysWeaver.Media
{



    public sealed partial class SvgCompositionTemplate
    {
        sealed class SvgLayer
        {
#if DEBUG
            public override string ToString() => String.Concat(Width.ToValueString(), 'x', Height.ToValueString(), " @ ", X.ToValueString(), ", ", Y.ToValueString(), ": ", String.Join("; ", Files));
#endif//DEBUG

            public readonly double X;
            public readonly double Y;
            public readonly double Width;
            public readonly double Height;
            public readonly SvgFile[] Files;
            public readonly TextTemplate Color;
            public readonly SvgCanvasGravity GravX;
            public readonly SvgCanvasGravity GravY;
            public readonly SvgCanvasSizing Size;


            public SvgLayer(double x, double y, double width, double height, SvgFile[] files, TextTemplate color, SvgCanvasGravity gravX, SvgCanvasGravity gravY, SvgCanvasSizing size)
            {

                X = x;
                Y = y;
                Width = width;
                Height = height;
                Files = files;
                Color = color;
                GravX = gravX;
                GravY = gravY;
                Size = size;

            }
        }
    }


}