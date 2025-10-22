using System;



namespace SysWeaver.MicroService
{
    public sealed class QrCodeEndPoint
    {
        /// <summary>
        /// The endpoint to server qr code images from.
        /// Must end with .svg or .png.
        /// </summary>
        public String Url = "qr/Code.svg";
        
        /// <summary>
        /// Auth required to access the end-point
        /// </summary>
        public String Auth = "debug";

        /// <summary>
        /// Size of the image (only used for png end points), the generated image will be less or equal to size x size pixels.
        /// </summary>
        public int Size = 1024;

        /// <summary>
        /// The compression methods in the preferred order to serve data (only svg endpoints are compressed)
        /// </summary>
        public String Compression = "deflate: Best, br: Best, gzip: Best";

        /// <summary>
        /// True to draw a quite zone
        /// </summary>
        public bool DrawQuite = true;

        /// <summary>
        /// Bright color as 0xrrggbb
        /// </summary>
        public int BrightColor = 0xffffff;

        /// <summary>
        /// Dark color as 0xrrggbb
        /// </summary>
        public int DarkColor = 0x000000;
    }

}
