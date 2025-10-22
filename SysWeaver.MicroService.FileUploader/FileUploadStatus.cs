namespace SysWeaver.MicroService
{
    /// <summary>
    /// The status for a file that is about to be uploaded to a server
    /// </summary>
    public enum FileUploadStatus
    {
        /// <summary>
        /// The server only accepts a single file upload
        /// </summary>
        MultipleFilesNotAllowed = -12,

        /// <summary>
        /// The repo is unknown
        /// </summary>
        UnknownRepo = -11,

        /// <summary>
        /// The file name is not valid
        /// </summary>
        InvalidFileName = -10,
        /// <summary>
        /// An upload of the same file is in progress
        /// </summary>
        OperationInProgress = -9,
        /// <summary>
        /// The file is not valid
        /// </summary>
        InvalidFile = -8,
        /// <summary>
        /// Can't upload this file since the disc quota will be exceeded
        /// </summary>
        DiscQuotaExceeded = -7,
        /// <summary>
        /// Not autharized to upload this file
        /// </summary>
        NotAuthorized = -6,
        /// <summary>
        /// Some paramaters are wrong
        /// </summary>
        InvalidParams = -5,
        /// <summary>
        /// The file extension is not accepeted
        /// </summary>
        RefuseExtension = -4,
        /// <summary>
        /// The file is refused due to it's size (typically too big)
        /// </summary>
        RefuseSize = -3,
        /// <summary>
        /// File is refused for some other reason
        /// </summary>
        Refuse = -2,
        /// <summary>
        /// The upload failed (might retry)
        /// </summary>
        UploadFailed = -1,
        /// <summary>
        /// No status found
        /// </summary>
        None = 0,
        /// <summary>
        /// The file already exist and the checksum etc matches so no need to upload again
        /// </summary>
        AlreadyUploaded = 1,
        /// <summary>
        /// The file is ok to be uploaded
        /// </summary>
        Upload = 2,
    }
}
