namespace SysWeaver.MicroService
{
    /// <summary>
    /// A request for a screenshot in any of the supported image formats
    /// </summary>
    public class ScreenshotImageRequest : ScreenshotJpegRequest
    {
        /// <summary>
        /// The image format
        /// </summary>
        [EditDefault(ScreenshotImageFormats.Png)]
        public ScreenshotImageFormats Format = ScreenshotImageFormats.Png;

        
        
        /// <summary>
        /// Create an image request (in the jpeg format) from a jpeg request
        /// </summary>
        /// <param name="jpeg">The request to copy the values from</param>
        /// <returns>A new request</returns>
        public static ScreenshotImageRequest From(ScreenshotJpegRequest jpeg)
        {
            return new ScreenshotImageRequest
            {
                Control = jpeg.Control,
                ExtraDelayMs = jpeg.ExtraDelayMs,
                Format = ScreenshotImageFormats.Jpg,
                Height = jpeg.Height,
                OptimizeForSpeed = jpeg.OptimizeForSpeed,
                Quality = jpeg.Quality,
                Scale = jpeg.Scale,
                Url = jpeg.Url,
                Width = jpeg.Width,
            };
        }

        /// <summary>
        /// Create an image request (in the png format) from a png request (the quality is the default)
        /// </summary>
        /// <param name="png">The request to copy the values from</param>
        /// <returns>A new request</returns>
        public static ScreenshotImageRequest From(ScreenshotPngRequest png)
        {
            return new ScreenshotImageRequest
            {
                Control = png.Control,
                ExtraDelayMs = png.ExtraDelayMs,
                Format = ScreenshotImageFormats.Png,
                Height = png.Height,
                OptimizeForSpeed = png.OptimizeForSpeed,
                Scale = png.Scale,
                Url = png.Url,
                Width = png.Width,
            };
        }


    }




}
