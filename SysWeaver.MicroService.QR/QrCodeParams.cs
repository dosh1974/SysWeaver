using System;



namespace SysWeaver.MicroService
{
    public sealed class QrCodeParams
    {
        /// <summary>
        /// Optional end points that will server qr codes.
        /// </summary>
        public QrCodeEndPoint[] EndPoints = [
            new QrCodeEndPoint(),
            new QrCodeEndPoint
            {
                Url = "qr/Code.png",
            }
            ];

        /// <summary>
        /// Used when some other service is requesting a response
        /// </summary>
        public String ResponseCompression = "deflate: Best, br: Best, gzip: Best";

        /// <summary>
        /// Used when some other service is requesting a response
        /// </summary>
        public String ResponseAuth;

    }

}
