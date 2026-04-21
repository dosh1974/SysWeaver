namespace SysWeaver.MicroService
{
    public class ScreenshotImageRequest : ScreenshotJpegRequest
    {
        /// <summary>
        /// The image format
        /// </summary>
        [EditDefault(ScreenshotImageFormats.Png)]
        public ScreenshotImageFormats Format = ScreenshotImageFormats.Png;

        
        
        public static ScreenshotImageRequest From(ScreenshotJpegRequest jpeg)
        {
            return new ScreenshotImageRequest
            {
                Control = jpeg.Control,
                Format = ScreenshotImageFormats.Jpg,
                Height = jpeg.Height,
                OptimizeForSpeed = jpeg.OptimizeForSpeed,
                Quality = jpeg.Quality,
                Scale = jpeg.Scale,
                Url = jpeg.Url,
                Width = jpeg.Width,
            };
        }

        public static ScreenshotImageRequest From(ScreenshotPngRequest png)
        {
            return new ScreenshotImageRequest
            {
                Control = png.Control,
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
