using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{


    /// <summary>
    /// Email sender service that sends emails using an SMTP server
    /// </summary>
    public sealed class SmtpEmailService : IEmailSender, IHaveStats, IPerfMonitored
    {
        /// <summary>
        /// Create the service
        /// </summary>
        /// <param name="p">The parameters (the user name and password are required)</param>
        public SmtpEmailService(SmtpEmailParams p = null)
        {
            p = p ?? new SmtpEmailParams();
            p.GetUserPassword(out var user, out var password);
            Sender = user;
            Tos = String.Concat("User: ", user.ToQuoted(), ", ", p);
            Client = new SmtpClient(p.Server)
            {
                Port = p.Port,
                Credentials = new NetworkCredential(user, password),
                EnableSsl = p.EnableSSL,
            };
            RetryCount = Math.Max(1, p.RetryCount) - 1;
        }
        /// <inheritdoc />
        public override string ToString() => Tos;
        
        /// <summary>
        /// The from address that emails are sent from (the user name)
        /// </summary>
        public readonly String Sender;
        readonly String Tos;
        readonly ExceptionTracker RetryFails = new ExceptionTracker();
        readonly ExceptionTracker SendFails = new ExceptionTracker();

        readonly int RetryCount;

        /// <summary>
        /// Send an email, failed attempts are retried up to the configured number of attempts
        /// </summary>
        /// <param name="msg">The message to send</param>
        /// <returns>A task that completes when the SMTP server has accepted the message</returns>
        /// <exception cref="Exception">The message could not be sent (the last error is thrown), or an address is invalid</exception>
        public async Task SendEmail(OutgoingEmailMessage msg)
        {
            using var _ = PerfMon.Track(nameof(SendEmail));
            using var m = new MailMessage
            {
                From = new MailAddress(Sender),
                Subject = msg.Subject?.Trim(),
                Body = msg.Body,
                IsBodyHtml = msg.IsHtml,
            };
            m.To.Add(new MailAddress(msg.Email));
            var ret = RetryCount;
            var c = Client;
            for (int i = 0; ; ++i)
            {
                try
                {
                    await c.SendMailAsync(m).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    if (i == ret)
                    {
                        SendFails.OnException(ex);
                        throw;
                    }
                    RetryFails.OnException(ex);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Client.Dispose();
        }

        /// <inheritdoc />
        public IEnumerable<Stats> GetStats()
        {
            foreach (var x in SendFails.GetStats(nameof(SmtpEmailService), "Send."))
                yield return x;
            foreach (var x in RetryFails.GetStats(nameof(SmtpEmailService), "Retry."))
                yield return x;
        }

        readonly SmtpClient Client;

        /// <inheritdoc />
        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(nameof(SmtpEmailService));

    }

}
