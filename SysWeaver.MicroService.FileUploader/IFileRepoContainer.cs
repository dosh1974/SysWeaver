namespace SysWeaver.MicroService
{
    public interface IFileRepoContainer
    {
        /// <summary>
        /// Array of file repositories
        /// </summary>
        IFileRepo[] Repos { get; }

    }

}
