namespace SysWeaver.MicroService.Media
{
    /// <summary>
    /// For some media formats we can choose to just use audio or video
    /// </summary>
    public enum MediaVideoStreams
    {
        /// <summary>
        /// Use both video and audio from the media
        /// </summary>
        VideoAndAudio = 0,
        /// <summary>
        /// Use only video from the media
        /// </summary>
        OnlyVideo,
        /// <summary>
        /// Use only audio from the media
        /// </summary>
        OnlyAudio,
    }
}
