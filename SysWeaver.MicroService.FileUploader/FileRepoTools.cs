using System;

namespace SysWeaver.MicroService
{
    public static class FileRepoTools
    {

        public static readonly FileUploadResult InvalidParams = new FileUploadResult(FileUploadStatus.InvalidParams);
        public static readonly FileUploadResult OperationInProgress = new FileUploadResult(FileUploadStatus.OperationInProgress);
        public static readonly FileUploadResult UploadFailed = new FileUploadResult(FileUploadStatus.UploadFailed);
        public static readonly FileUploadResult RefuseSize = new FileUploadResult(FileUploadStatus.RefuseSize);
        public static readonly FileUploadResult InvalidFile = new FileUploadResult(FileUploadStatus.InvalidFile);
        public static readonly FileUploadResult MultipleFilesNotAllowed = new FileUploadResult(FileUploadStatus.MultipleFilesNotAllowed);
        public static readonly FileUploadResult RefuseExtension = new FileUploadResult(FileUploadStatus.RefuseExtension);
        public static readonly FileUploadResult Upload = new FileUploadResult(FileUploadStatus.Upload);
        public static readonly FileUploadResult InvalidFileName = new FileUploadResult(FileUploadStatus.InvalidFileName);
        public static readonly FileUploadResult Refuse = new FileUploadResult(FileUploadStatus.Refuse);
        public static readonly FileUploadResult UnknownRepo = new FileUploadResult(FileUploadStatus.UnknownRepo);
        public static readonly FileUploadResult NotAuthorized = new FileUploadResult(FileUploadStatus.NotAuthorized);


        /// <summary>
        /// Format a web hash as a file hash
        /// </summary>
        /// <param name="hashStr">The hash as coming from the web</param>
        /// <returns>A file hash hash</returns>
        public static String FormatAsFileHash(String hashStr)
        {
            var hash = ByteArrayExtensions.FromHex(hashStr);
            if (hash.Length != 16)
                return null;
            return HashTools.GetHashString16(hash);
        }


    }

}
