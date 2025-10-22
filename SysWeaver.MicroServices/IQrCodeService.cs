using System;


namespace SysWeaver.MicroService
{

    /// <summary>
    /// Service for generating QR code svgs
    /// </summary>
    public interface IQrCodeService
    {
        /// <summary>
        /// Generate a QR code for the given data string
        /// </summary>
        /// <param name="data">The data string to encode in the QR code</param>
        /// <returns>The svg image containing the data</returns>
        String CreateQrCode(String data);

        /// <summary>
        /// Generate a QR code for the given data string, with optional parameters
        /// </summary>
        /// <param name="data">The data string to encode in the QR code</param>
        /// <param name="bright">The bright (background) css color</param>
        /// <param name="dark">The dark (dots) css color</param>
        /// <param name="safeArea">True to render a safe are (background) around the actual code</param>
        /// <returns>The svg image containing the data</returns>
        String CreateQrCode(String data, String bright, String dark = "#000", bool safeArea = true);
    }



}
