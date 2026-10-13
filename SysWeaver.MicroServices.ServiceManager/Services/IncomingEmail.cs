using System;
using SysWeaver.Data;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Represents an incoming email
    /// </summary>
    public class IncomingEmail
    {
        /// <summary>
        /// The email address that the email was sent from
        /// </summary>
        public String From;

        /// <summary>
        /// The display name of the sender (if known)
        /// </summary>
        public String SenderName;

        /// <summary>
        /// The email address that the email was sent to
        /// </summary>
        public String To;

        /// <summary>
        /// The subject of the email
        /// </summary>
        [TableDataText(80)]
        public String Subject;

        /// <summary>
        /// The email body, html if <see cref="IsHtml"/> is true, else plain text
        /// </summary>
        [TableDataText(160)]
        public String Body;

        /// <summary>
        /// True if the <see cref="Body"/> is html, false if it's plain text
        /// </summary>
        public bool IsHtml;

        /// <summary>
        /// The UTC time stamp of when this email was recieved
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// The system that this email belongs to
        /// </summary>
        public String System;

        /// <summary>
        /// The email id, use the id of the latest email to poll for more incoming emails
        /// </summary>
        public String Id;

        /// <summary>
        /// The id of the email that this is a reply to (if known)
        /// </summary>
        public String InReplyTo;


        public override string ToString() => String.Concat('#', Id, ' ', From, ": ", Subject);

        public void CopyFrom(IncomingEmail from)
        {
            Id = from.Id;
            From = from.From;
            SenderName = from.SenderName;
            To = from.To;
            Subject = from.Subject;
            Body = from.Body;
            IsHtml = from.IsHtml;
            Time = from.Time;
            System = from.System;
            InReplyTo = from.InReplyTo;
        }

        public IncomingEmail Clone()
        {
            var t = new IncomingEmail();
            t.CopyFrom(this);
            return t;
        }

    }
}
