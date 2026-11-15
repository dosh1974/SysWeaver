using System;



namespace SysWeaver.MicroService
{
    /// <summary>
    /// Configuration of an end point serving QR codes, the payload is the query string of the request
    /// </summary>
    public sealed class QrCodeEndPoint
    {
        /// <summary>
        /// The endpoint to server qr code images from.
        /// Must end with .svg or .png.
        /// </summary>
        public String Url = "qr/Code.svg";
        
        /// <summary>
        /// Auth required to access the end-point, default is "debug"
        /// </summary>
        public String Auth = "debug";

        /// <summary>
        /// Size of the image (only used for png end points), the generated image will be less or equal to size x size pixels (default is 1024).
        /// </summary>
        public int Size = 1024;

        /// <summary>
        /// The compression methods in the preferred order to serve data (only svg endpoints are compressed)
        /// </summary>
        public String Compression = "deflate: Best, br: Best, gzip: Best";

        /// <summary>
        /// True (the default) to draw a quiet zone (the 4 cell border around the code)
        /// </summary>
        public bool DrawQuite = true;

        /// <summary>
        /// Bright color as 0xrrggbb (default is white)
        /// </summary>
        public int BrightColor = 0xffffff;

        /// <summary>
        /// Dark color as 0xrrggbb (default is black)
        /// </summary>
        public int DarkColor = 0x000000;
    }

}
