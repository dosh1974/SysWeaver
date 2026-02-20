namespace SysWeaver.AI
{
    public enum OpenAiImageSizes
    {
        /// <summary>
        /// Let the image model choose aspect ratio
        /// </summary>
        Auto,

        /// <summary>
        /// 1:1 aspect ratio
        /// </summary>
        Square,

        /// <summary>
        /// Typically 3:4 aspect ratio
        /// </summary>
        Portrait,

        /// <summary>
        /// Typically 4:3 aspect ratio
        /// </summary>
        Landscape,

        /// <summary>
        /// Typically 9:16 aspect ratio
        /// </summary>
        TallPortrait,

        /// <summary>
        /// Typically 16:9 aspect ratio
        /// </summary>
        WideLandscape,

    }
}
