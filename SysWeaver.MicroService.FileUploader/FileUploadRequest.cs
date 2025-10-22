using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Check upload status for one or more files
    /// </summary>
    public sealed class FileUploadRequest
    {
        /// <summary>
        /// The name of the repository to upload to
        /// </summary>
        public String Repo;
        /// <summary>
        /// File information
        /// </summary>
        public FileUploadInfo[] Files;
    }
}
