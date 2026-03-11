using System;

namespace SysWeaver.HttpTransformer
{
    public class ImageTransformerParams
    {

        /// <summary>
        /// If true, optimization is performed on legacy files (bmp, tif, jpg, png)
        /// </summary>
        public bool Optimize = true;

        /// <summary>
        /// If true, new formats avif, webp are getting fallback to legacy
        /// </summary>
        public bool SupportNew = true;

        /// <summary>
        /// File extensions to support
        /// </summary>
        public String[] Support =
            [
                "psd",
                "tga",
                "dds",
                "exr",
                "jfif",
                "jp2",
                "jxl",
                "pcx",
                "pict",
            ];
    }

}
