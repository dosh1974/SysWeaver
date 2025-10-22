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
    }





}
