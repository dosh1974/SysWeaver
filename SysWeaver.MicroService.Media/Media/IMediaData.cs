namespace SysWeaver.MicroService.Media
{
    /// <summary>
    /// Base interface for all media data
    /// </summary>
    public interface IMediaData
    {
        /// <summary>
        /// Validate the paramaters (set them to valid bounds)
        /// </summary>
        void Validate();
    }
}
