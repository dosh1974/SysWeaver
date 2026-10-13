using System;

namespace SysWeaver.Email
{
    /// <summary>
    /// An email to send (or fake as incoming)
    /// </summary>
    public sealed class FakeEmailRequest
    {
        /// <summary>
        /// The sender email address.
        /// When sending: ignored (the From of the service is used).
        /// When faking an incoming email: the (fake) user that sent the email, required.
        /// </summary>
        public String From;

        /// <summary>
        /// The display name of the sender (incoming only)
        /// </summary>
        public String SenderName;

        /// <summary>
        /// The receiver email address.
        /// When sending: required.
        /// When faking an incoming email: the system address that received the email (null to use the From of the service).
        /// </summary>
        public String To;

        /// <summary>
        /// The subject
        /// </summary>
        public String Subject;

        /// <summary>
        /// The body, html if <see cref="IsHtml"/> is true, else plain text
        /// </summary>
        public String Body;

        /// <summary>
        /// True if the body is html
        /// </summary>
        public bool IsHtml;

        /// <summary>
        /// The system that the email belongs to (incoming only)
        /// </summary>
        public String System;

        /// <summary>
        /// The id of the email that this is a reply to (incoming only)
        /// </summary>
        public String InReplyTo;
    }

    /// <summary>
    /// Identifies an email
    /// </summary>
    public sealed class FakeEmailIdRequest
    {
        /// <summary>
        /// The id of the email
        /// </summary>
        public String Id;
    }

    /// <summary>
    /// Request a list of all emails
    /// </summary>
    public sealed class FakeEmailListRequest
    {
        /// <summary>
        /// The version of the list that the caller have (start with 0).
        /// Blocks until the list changes (or a time out) if this is the current version.
        /// </summary>
        public long Version;
    }

    /// <summary>
    /// A list of all emails
    /// </summary>
    public sealed class FakeEmailList
    {
        /// <summary>
        /// The version of the list, pass this in the next request to wait for a change
        /// </summary>
        public long Version;

        /// <summary>
        /// True if nothing changed (Emails is null)
        /// </summary>
        public bool Unchanged;

        /// <summary>
        /// The email address that the service sends emails from
        /// </summary>
        public String From;

        /// <summary>
        /// All emails, newest first
        /// </summary>
        public FakeEmailSummary[] Emails;
    }

    /// <summary>
    /// The summary of an email (no body)
    /// </summary>
    public sealed class FakeEmailSummary
    {
        public String Id;
        public bool Incoming;
        public String From;
        public String SenderName;
        public String To;
        public String Subject;
        public String Preview;
        public bool IsHtml;
        public DateTime Time;
        public String System;
        public String Error;
        public String InReplyTo;

        internal FakeEmailSummary(AnyEmail e)
        {
            Id = e.Id;
            Incoming = e.Incoming;
            From = e.From;
            SenderName = e.SenderName;
            To = e.To;
            Subject = e.Subject;
            Preview = e.Preview;
            IsHtml = e.IsHtml;
            Time = e.Time;
            System = e.System;
            Error = e.Error;
            InReplyTo = e.InReplyTo;
        }
    }

}
