namespace SysWeaver.MicroService
{
    /// <summary>
    /// The image formats that a screenshot can be returned in
    /// </summary>
    public enum ScreenshotImageFormats
    {
        /// <summary>
        /// Lossless PNG image
        /// </summary>
        Png,
        /// <summary>
        /// Lossy JPEG image (see <see cref="ScreenshotJpegRequest.Quality"/>)
        /// </summary>
        Jpg,
    }




}
