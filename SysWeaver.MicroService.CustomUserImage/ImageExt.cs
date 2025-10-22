using ImageMagick;
using System;

namespace SysWeaver.MicroService
{
    sealed class ImageExt
    {
        public ImageExt(MagickFormat save, String ext)
        {
            SaveFormat = save;
            SaveExt = ext;
        }

        public readonly MagickFormat SaveFormat;
        public readonly String SaveExt;

    }
}
