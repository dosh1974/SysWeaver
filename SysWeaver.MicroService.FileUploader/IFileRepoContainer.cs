namespace SysWeaver.MicroService
{
    /// <summary>
    /// Something that provides multiple file repositories to the <see cref="FileUploaderService"/>
    /// </summary>
    public interface IFileRepoContainer
    {
        /// <summary>
        /// Array of file repositories
        /// </summary>
        IFileRepo[] Repos { get; }

    }

}
