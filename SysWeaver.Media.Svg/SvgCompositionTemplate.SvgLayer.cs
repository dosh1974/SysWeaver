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

            public SvgLayer(double x, double y, double width, double height, SvgFile[] files)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Files = files;
            }
        }
    }


}