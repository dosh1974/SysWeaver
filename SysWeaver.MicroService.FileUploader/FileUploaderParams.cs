namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="FileUploaderService"/>
    /// </summary>
    public sealed class FileUploaderParams
    {
        /// <summary>
        /// Array of file repositories (repositories provided by other services are added as well)
        /// </summary>
        public StaticFileRepo[] Repos;

    }
}
