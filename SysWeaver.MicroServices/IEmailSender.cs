using System;
using System.Threading;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{
    public interface IEmailSender : IDisposable
    {
        /// <summary>
        /// Send an email message
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        Task SendEmail(OutgoingEmailMessage msg);

    }

    public static class EmailExtensions
    {
        /// <summary>
        /// Send an email
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="to"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        /// <param name="isHtml"></param>
        /// <returns></returns>
        public static Task Send(this IEmailSender sender, String to, String subject, String body, bool isHtml = false)
            => sender.SendEmail(new OutgoingEmailMessage
                    {
                        Email = to,
                        Subject = subject,
                        Body = body,
                        IsHtml = isHtml,
                    });

    }


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
