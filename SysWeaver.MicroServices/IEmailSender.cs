using System;
using System.Threading;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Implemented by services that can send emails
    /// </summary>
    public interface IEmailSender : IDisposable
    {
        /// <summary>
        /// Send an email message
        /// </summary>
        /// <param name="msg">The message to send</param>
        /// <returns>A task that completes when the message has been sent</returns>
        Task SendEmail(OutgoingEmailMessage msg);

    }

    /// <summary>
    /// Extension methods for <see cref="IEmailSender"/>
    /// </summary>
    public static class EmailExtensions
    {
        /// <summary>
        /// Send an email
        /// </summary>
        /// <param name="sender">The email sender</param>
        /// <param name="to">The email address to send the message to</param>
        /// <param name="subject">The email subject</param>
        /// <param name="body">The email body</param>
        /// <param name="isHtml">True if the body is html, false if it is plain text</param>
        /// <returns>A task that completes when the message has been sent</returns>
        public static Task Send(this IEmailSender sender, String to, String subject, String body, bool isHtml = false)
            => sender.SendEmail(new OutgoingEmailMessage
                    {
                        Email = to,
                        Subject = subject,
                        Body = body,
                        IsHtml = isHtml,
                    });

    }


    /// <summary>
    /// An email message to send
    /// </summary>
    public class OutgoingEmailMessage
    {
        public override string ToString() => String.Concat(Email, ": ", Subject);

        /// <summary>
        /// The email address to send the message to
        /// </summary>
        [EditMin(1)]
        public string Email;

        /// <summary>
        /// The email subject
        /// </summary>
        public string Subject;

        /// <summary>
        /// The email body, could be html or plain text
        /// </summary>
        public String Body;

        /// <summary>
        /// The system that this message belongs to
        /// </summary>
        public String System;

        /// <summary>
        /// Set to true if the email body is html
        /// </summary>
        public bool IsHtml;

    }


}
