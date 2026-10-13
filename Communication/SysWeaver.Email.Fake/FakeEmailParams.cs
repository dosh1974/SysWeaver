using System;

namespace SysWeaver.Email
{
    public sealed class FakeEmailParams
    {
        /// <summary>
        /// Maximum size of the email queues (will try to keep them around this number)
        /// </summary>
        public int Size = 250;

        /// <summary>
        /// Time out in seconds when waiting for new emails
        /// </summary>
        public int TimeOut = 30;

        /// <summary>
        /// Maximum number of emails to return on a poll
        /// </summary>
        public int MaxPoll = 100;

        /// <summary>
        /// The email address that emails are sent from.
        /// Default: "noreply@example.com".
        /// </summary>
        public String From = "noreply@example.com";

        /// <summary>
        /// Emails sent to addresses in any of these domains (ex: "fail.example.com") fails (Send throws), the email is still kept and marked as failed.
        /// </summary>
        public String[] FailDomains;
    }

}
