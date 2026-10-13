using System;
using SysWeaver.MicroService;

namespace SysWeaver.EmailReader
{
    /// <summary>
    /// A received email (as displayed in the email table)
    /// </summary>
    public sealed class Pop3Email : IncomingEmail
    {
        public override string ToString() => String.Concat('#', Id, " from ", From, " [", Status, "]: ", Subject);

        /// <summary>
        /// The unique id of the email on the server (UIDL), null if the server doesn't support it
        /// </summary>
        public String Uid;

        /// <summary>
        /// The status, ex: "received", "deleted"
        /// </summary>
        public String Status;
    }
}
