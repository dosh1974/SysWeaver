using System;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{
    /// <summary>
    /// An incoming or outgoing message (as displayed in the message table)
    /// </summary>
    public sealed class AmazonTextMessage : IncomingTextMessage
    {
        public override string ToString() => String.Concat('#', Id, Incoming ? " from " : " to ", PhoneNumber, " [", Status, "]: ", Text);

        /// <summary>
        /// True if this message was received, false if it was sent
        /// </summary>
        public bool Incoming;

        /// <summary>
        /// The last known status, ex: "received", "accepted", "SUCCESSFUL", "DELIVERED", "UNREACHABLE"
        /// </summary>
        public String Status;

        /// <summary>
        /// The reference of an outgoing message
        /// </summary>
        public String Reference;
    }
}
