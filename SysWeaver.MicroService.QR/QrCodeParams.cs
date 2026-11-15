using System;



namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="QrCodeService"/>
    /// </summary>
    public sealed class QrCodeParams
    {
        /// <summary>
        /// Optional end points that will serve qr codes (default is "qr/Code.svg" and "qr/Code.png").
        /// </summary>
        public QrCodeEndPoint[] EndPoints = [
            new QrCodeEndPoint(),
            new QrCodeEndPoint
            {
                Url = "qr/Code.png",
            }
            ];

        /// <summary>
        /// The compression methods in the preferred order, used by responses created for other services (<see cref="QrCodeService.CreateResponse"/>)
        /// </summary>
        public String ResponseCompression = "deflate: Best, br: Best, gzip: Best";

        /// <summary>
        /// Auth required for responses created for other services (<see cref="QrCodeService.CreateResponse"/>), null for none
        /// </summary>
        public String ResponseAuth;

    }

}
