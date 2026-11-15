using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Shared results and helpers for file repositories
    /// </summary>
    public static class FileRepoTools
    {
        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.InvalidParams"/>
        /// </summary>
        public static readonly FileUploadResult InvalidParams = new FileUploadResult(FileUploadStatus.InvalidParams);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.OperationInProgress"/>
        /// </summary>
        public static readonly FileUploadResult OperationInProgress = new FileUploadResult(FileUploadStatus.OperationInProgress);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.UploadFailed"/>
        /// </summary>
        public static readonly FileUploadResult UploadFailed = new FileUploadResult(FileUploadStatus.UploadFailed);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.RefuseSize"/>
        /// </summary>
        public static readonly FileUploadResult RefuseSize = new FileUploadResult(FileUploadStatus.RefuseSize);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.InvalidFile"/>
        /// </summary>
        public static readonly FileUploadResult InvalidFile = new FileUploadResult(FileUploadStatus.InvalidFile);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.MultipleFilesNotAllowed"/>
        /// </summary>
        public static readonly FileUploadResult MultipleFilesNotAllowed = new FileUploadResult(FileUploadStatus.MultipleFilesNotAllowed);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.RefuseExtension"/>
        /// </summary>
        public static readonly FileUploadResult RefuseExtension = new FileUploadResult(FileUploadStatus.RefuseExtension);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.Upload"/>
        /// </summary>
        public static readonly FileUploadResult Upload = new FileUploadResult(FileUploadStatus.Upload);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.InvalidFileName"/>
        /// </summary>
        public static readonly FileUploadResult InvalidFileName = new FileUploadResult(FileUploadStatus.InvalidFileName);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.Refuse"/>
        /// </summary>
        public static readonly FileUploadResult Refuse = new FileUploadResult(FileUploadStatus.Refuse);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.UnknownRepo"/>
        /// </summary>
        public static readonly FileUploadResult UnknownRepo = new FileUploadResult(FileUploadStatus.UnknownRepo);

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.NotAuthorized"/>
        /// </summary>
        public static readonly FileUploadResult NotAuthorized = new FileUploadResult(FileUploadStatus.NotAuthorized);


        /// <summary>
        /// Format a web hash as a file hash
        /// </summary>
        /// <param name="hashStr">The hash as coming from the web</param>
        /// <returns>A file hash string, null if the hash isn't 16 bytes (32 hex characters)</returns>
        public static String FormatAsFileHash(String hashStr)
        {
            var hash = ByteArrayExtensions.FromHex(hashStr);
            if (hash.Length != 16)
                return null;
            return HashTools.GetHashString16(hash);
        }


    }

}
