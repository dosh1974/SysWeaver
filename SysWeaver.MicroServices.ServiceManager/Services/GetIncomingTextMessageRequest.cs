using System;


namespace SysWeaver.MicroService
{
    public sealed class GetIncomingTextMessageRequest
    {
        /// <summary>
        /// The id of the last recieved message (start with null).
        /// Will block until a message arrives that is newer than this.
        /// </summary>
        public String LastId;

        /// <summary>
        /// If non-empty, only messages sent to this system is returned
        /// </summary>
        public String System;
    }
}
