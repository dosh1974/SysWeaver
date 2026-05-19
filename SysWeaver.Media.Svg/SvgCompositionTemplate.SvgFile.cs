using System;

namespace SysWeaver.Media
{



    public sealed partial class SvgCompositionTemplate
    {
        sealed class SvgFile
        {
#if DEBUG
            public override string ToString() => String.Concat(IsBitmap ? "Bitmap \"" : "Svg \"", NameTemplate.Template, '"');
#endif//DEBUG

            public readonly String[] Vars;
            public readonly TextTemplate NameTemplate;
            public readonly bool IsBitmap;

            public SvgFile(string[] vars, TextTemplate nameTemplate, bool isBitmap)
            {
                Vars = vars;
                NameTemplate = nameTemplate;
                IsBitmap = isBitmap;
            }
        }
    }


}