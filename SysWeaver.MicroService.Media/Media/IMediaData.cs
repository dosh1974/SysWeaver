namespace SysWeaver.MicroService.Media
{
    /// <summary>
    /// Base interface for all media data
    /// </summary>
    public interface IMediaData
    {
        /// <summary>
        /// Validate the parameters (set them to valid bounds)
        /// </summary>
        void Validate();
    }
}
