using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The result of a file upload (or the check if a file can be uploaded)
    /// </summary>
    public sealed class FileUploadResult
    {
        /// <summary>
        /// The status of a file upload request
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


        /// <summary>
        /// Create an empty result (status <see cref="FileUploadStatus.None"/>)
        /// </summary>
        public FileUploadResult()
        {
        }

        /// <summary>
        /// Create a result
        /// </summary>
        /// <param name="result">The status</param>
        /// <param name="url">The url to the uploaded file (if applicable)</param>
        public FileUploadResult(FileUploadStatus result, string url = null)
        {
            Result = result;
            Url = url;
        }

#if DEBUG
        /// <inheritdoc />
        public override string ToString()
            => String.Concat(Result, " (", (int)Result, ") ", Url?.ToQuoted());
#endif//DEBUG


        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.AlreadyUploaded"/>
        /// </summary>
        public static readonly FileUploadResult AlreadyUploaded = new FileUploadResult
        {
            Result = FileUploadStatus.AlreadyUploaded,
        };

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.Upload"/>
        /// </summary>
        public static readonly FileUploadResult Upload = new FileUploadResult
        {
            Result = FileUploadStatus.Upload,
        };

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.RefuseSize"/>
        /// </summary>
        public static readonly FileUploadResult RefuseSize = new FileUploadResult
        {
            Result = FileUploadStatus.RefuseSize,
        };

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.RefuseExtension"/>
        /// </summary>
        public static readonly FileUploadResult RefuseExtension = new FileUploadResult
        {
            Result = FileUploadStatus.RefuseExtension,
        };

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.Refuse"/>
        /// </summary>
        public static readonly FileUploadResult Refuse = new FileUploadResult
        {
            Result = FileUploadStatus.Refuse,
        };

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.NotAuthorized"/>
        /// </summary>
        public static readonly FileUploadResult NotAuthorized = new FileUploadResult
        {
            Result = FileUploadStatus.NotAuthorized,
        };

        /// <summary>
        /// A shared result with the status <see cref="FileUploadStatus.None"/>
        /// </summary>
        public static readonly FileUploadResult None = new FileUploadResult
        {
            Result = FileUploadStatus.None,
        };

    }





}
