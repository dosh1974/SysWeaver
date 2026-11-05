using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A one time token for deleting the log file, with the facts the confirmation should show (see <see cref="FileLogService.GetDeleteLogFileToken"/>).
    /// </summary>
    public sealed class DeleteLogFileToken
    {
        /// <summary>
        /// The token to send to "Api/logFile/DeleteLogFile" (as {Token}), valid for 5 minutes, for the same session, once.
        /// </summary>
        public String Token;

        /// <summary>
        /// When the token expires (UTC).
        /// </summary>
        public DateTime Expires;

        /// <summary>
        /// The name of the log file, ex: "WebServiceServer.log".
        /// </summary>
        public String Name;

        /// <summary>
        /// True if the file exists.
        /// </summary>
        public bool Exists;

        /// <summary>
        /// The size of the file in bytes.
        /// </summary>
        public long Size;

        /// <summary>
        /// When the file was last written (UTC), null if it doesn't exist.
        /// </summary>
        public DateTime? LastWrite;

        /// <summary>
        /// What is lost (show it in the confirmation).
        /// </summary>
        public String Warning;
    }

    /// <summary>
    /// The body of "Api/logFile/DeleteLogFile" (see <see cref="FileLogService.DeleteLogFile"/>).
    /// </summary>
    public sealed class DeleteLogFileRequest
    {
        /// <summary>
        /// The token from "Api/logFile/GetDeleteLogFileToken".
        /// </summary>
        public String Token;

        /// <summary>
        /// Ignored (sent by the generic text viewer's delete).
        /// </summary>
        public String Url;

        /// <summary>
        /// Ignored (sent by the generic text viewer's delete).
        /// </summary>
        public String Name;
    }
}
