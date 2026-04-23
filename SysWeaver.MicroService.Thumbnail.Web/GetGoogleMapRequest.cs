using System;

namespace SysWeaver.MicroService
{
    public class GetGoogleMapRequest
    {


        /// <summary>
        /// The map center Latitude, Longitude
        /// </summary>
        [EditDefault("40.2525° N, 58.4395° E")]
        [EditMin(1)]
        public String Center = "40.2525° N, 58.4395° E";

        /// <summary>
        /// Zoom level (google maps zoom level)
        /// </summary>
        [EditDefault(17)]
        [EditRange(0, 20)]
        [EditSlider]
        public int Zoom = 17;

        /// <summary>
        /// Language to display (used for place names etc)
        /// </summary>
        [EditDefault("en")]
        [EditAllowNull]
        public String Language = "en";

        /// <summary>
        /// Type of map visuals
        /// </summary>
        [EditDefault(GoogleMapTypes.Satellite)]
        public GoogleMapTypes Type = GoogleMapTypes.Satellite;

        /// <summary>
        /// The width 
        /// </summary>
        [EditDefault(1920)]
        [EditRange(64, 16384)]
        public int Width = 1920;

        /// <summary>
        /// The height
        /// </summary>
        [EditDefault(1080)]
        [EditRange(64, 16384)]
        public int Height = 1080;

        /// <summary>
        /// Dpi, scaling dpi (bigger text etc)
        /// </summary>
        [EditDefault(100)]
        [EditRange(25, 400)]
        [EditSlider]
        public double Dpi = 100;

        /// <summary>
        /// Optional url to an image to use as a pin.
        /// You can set this to "true" to get the default pin.
        /// </summary>
        [EditDefault(null)]
        [EditAllowNull]
        public String Pin;

        /// <summary>
        /// When using a custom pin image you must specify the pin location withing the image.
        /// The value here should be a percentage, ex: "50%".
        /// </summary>
        [EditDefault(null)]
        [EditAllowNull]
        public String PinX;

        /// <summary>
        /// When using a custom pin image you must specify the pin location withing the image.
        /// The value here should be a percentage, ex: "50%".
        /// </summary>
        [EditDefault(null)]
        [EditAllowNull]
        public String PinY;

        /// <summary>
        /// Optionally specify the height of the pin.
        /// Set it to zero use the default height.
        /// </summary>
        [EditDefault(0.0)]
        [EditRange(0.0, 512.0)]
        [EditSlider]
        public double PinHeight = 0.0;

        /// <summary>
        /// Optimize for speed rather than quality
        /// </summary>
        [EditDefault(false)]
        public bool OptimizeForSpeed;

        /// <summary>
        /// Wait an additional time before capturing the screen shot (in ms)
        /// </summary>
        [EditDefault(0)]
        [EditRange(0, 100_000)]
        public int ExtraDelayMs = 0;

    }


}
