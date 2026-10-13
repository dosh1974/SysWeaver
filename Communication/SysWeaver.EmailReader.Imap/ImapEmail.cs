using System;
using SysWeaver.MicroService;

namespace SysWeaver.EmailReader
{
    /// <summary>
    /// A received email (as displayed in the email table)
    /// </summary>
    public sealed class ImapEmail : IncomingEmail
    {
        public override string ToString() => String.Concat('#', Id, " from ", From, " [", Status, "]: ", Subject);

        /// <summary>
        /// The unique id of the email in the folder (IMAP UID)
        /// </summary>
        public String Uid;

        /// <summary>
        /// The status, ex: "received", "seen", "deleted", "moved"
        /// </summary>
        public String Status;
    }
}
