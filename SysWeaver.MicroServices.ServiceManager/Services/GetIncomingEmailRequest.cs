using System;


namespace SysWeaver.MicroService
{
    public sealed class GetIncomingEmailRequest
    {
        /// <summary>
        /// The id of the last recieved email (start with null).
        /// Will block until an email arrives that is newer than this.
        /// </summary>
        public String LastId;

        /// <summary>
        /// If non-empty, only emails sent to this system is returned
        /// </summary>
        public String System;
    }
}
