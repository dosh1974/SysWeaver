namespace SysWeaver.MicroService
{
    /// <summary>
    /// A request to take a screenshot of a web page, returned as a JPEG image
    /// </summary>
    public class ScreenshotJpegRequest : ScreenshotRequest
    {
        /// <summary>
        /// Quality
        /// </summary>
        [EditSlider]
        [EditDefault(70.0)]
        [EditRange(10.0, 100.0)]
        public int Quality = 70;
    }




}
