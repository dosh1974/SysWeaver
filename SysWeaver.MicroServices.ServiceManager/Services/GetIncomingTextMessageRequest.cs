using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Request for incoming text messages
    /// </summary>
    public sealed class GetIncomingTextMessageRequest
    {
        /// <summary>
        /// The id of the last received message (start with null).
        /// Will block until a message arrives that is newer than this.
        /// </summary>
        public String LastId;

        /// <summary>
        /// If non-empty, only messages sent to this system are returned
        /// </summary>
        public String System;
    }
}
