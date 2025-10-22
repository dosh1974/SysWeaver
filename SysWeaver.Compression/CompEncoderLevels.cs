namespace SysWeaver.Compression
{
    public enum CompEncoderLevels
    {
        /// <summary>
        /// Use for real-time compression of API's etc
        /// </summary>
        Fast = 0,
        /// <summary>
        /// Use for offline preview or for longer caching etc
        /// </summary>
        Balanced,
        /// <summary>
        /// Use for offline builds etc
        /// </summary>
        Best
    }

}
