using System;

namespace SysWeaver.MicroService
{
    public sealed class FileUploadResult
    {
        /// <summary>
        /// The result of a file upload request
        /// </summary>
        public FileUploadStatus Result;

        /// <summary>
        /// The url to the uploaded file (if applicable)
        /// </summary>
        public String Url;

        /// <summary>
        /// If chunks was provided, this is a list of missing chunks
        /// </summary>
        public Byte[] Chunks;


        public FileUploadResult()
        {
        }

        public FileUploadResult(FileUploadStatus result, string url = null)
        {
            Result = result;
            Url = url;
        }

#if DEBUG
        public override string ToString()
            => String.Concat(Result, " (", (int)Result, ") ", Url?.ToQuoted());
#endif//DEBUG


        public static readonly FileUploadResult AlreadyUploaded = new FileUploadResult
        {
            Result = FileUploadStatus.AlreadyUploaded,
        };

        public static readonly FileUploadResult Upload = new FileUploadResult
        {
            Result = FileUploadStatus.Upload,
        };

        public static readonly FileUploadResult RefuseSize = new FileUploadResult
        {
            Result = FileUploadStatus.RefuseSize,
        };

        public static readonly FileUploadResult RefuseExtension = new FileUploadResult
        {
            Result = FileUploadStatus.RefuseExtension,
        };

        public static readonly FileUploadResult Refuse = new FileUploadResult
        {
            Result = FileUploadStatus.Refuse,
        };

        public static readonly FileUploadResult NotAuthorized = new FileUploadResult
        {
            Result = FileUploadStatus.NotAuthorized,
        };

        public static readonly FileUploadResult None = new FileUploadResult
        {
            Result = FileUploadStatus.None,
        };

    }





}
