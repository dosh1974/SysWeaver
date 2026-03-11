using ImageMagick;
using System;

namespace SysWeaver.HttpTransformer
{

    sealed class ImageFormat
    {
        public readonly MagickFormat Format;
        public readonly String Extension;
        public readonly uint Quality;
        public readonly IWriteDefines Def;

        public readonly bool HaveAlpha;
        public readonly bool ForceCompress;
        public readonly Tuple<String, bool> Mime;

        public readonly bool OnlyCompress;

        public ImageFormat(string extension)
        {
            extension = '.' + extension.TrimStart('.');
            Extension = extension;
            Mime = MimeTypeMap.GetMimeType(extension);
            OnlyCompress = true;
        }

        public ImageFormat(MagickFormat format, string extension, uint quality = 90, IWriteDefines def = null, bool haveAlpha = true, bool forceCompress = false)
        {
            extension = '.' + extension.TrimStart('.');
            Extension = extension;
            Mime = MimeTypeMap.GetMimeType(extension);
            Format = format;
            Quality = quality;
            Def = def;
            HaveAlpha = haveAlpha;
            ForceCompress = forceCompress;
        }

    }

}
