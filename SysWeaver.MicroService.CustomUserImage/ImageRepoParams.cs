namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="ImageRepoService"/>
    /// </summary>
    public sealed class ImageRepoParams
    {
        /// <summary>
        /// The image repositories (files uploaded to them are resized to the sizes of the repository)
        /// </summary>
        public ImageRepo[] Repos;
    }
}
