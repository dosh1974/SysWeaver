using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using Azure;
using Azure.Communication.Email;
using SysWeaver.MicroService;


namespace SysWeaver.Email
{


    /// <summary>
    /// Email sender service that sends emails using Microsoft Azure Communication Services (ACS) Email
    /// </summary>
    public sealed class MicrosoftAcsEmailService : IEmailSender, IHaveStats, IPerfMonitored
    {
        /// <summary>
        /// Create the service
        /// </summary>
        /// <param name="p">The parameters, <see cref="MicrosoftAcsEmailParams.From"/> and the api key (a connection string, or an access key together with <see cref="MicrosoftAcsEmailParams.Endpoint"/>) are required</param>
        /// <exception cref="Exception">A required parameter is missing</exception>
        public MicrosoftAcsEmailService(MicrosoftAcsEmailParams p = null)
        {
            p = p ?? new MicrosoftAcsEmailParams();
            var from = p.From?.Trim();
            if (String.IsNullOrEmpty(from))
                throw new Exception(nameof(p.From) + " parameter may not be empty!");
            Sender = new MailAddress(from).Address;
            Wait = p.WaitForCompletion ? WaitUntil.Completed : WaitUntil.Started;
            Tos = p.ToString();
            var options = new EmailClientOptions();
            // Retries are handled by this service (so that they can be tracked)
            options.Retry.MaxRetries = 0;
            var key = p.GetApiKey();
            if (key.StartsWith("endpoint=", StringComparison.OrdinalIgnoreCase))
            {
                Client = new EmailClient(key, options);
            }
            else
            {
                var endPoint = p.Endpoint?.Trim();
                if (String.IsNullOrEmpty(endPoint))
                    throw new Exception(nameof(p.Endpoint) + " parameter may not be empty when the " + nameof(p.ApiKey) + " is an access key (and not a connection string)!");
                Client = new EmailClient(new Uri(endPoint), new AzureKeyCredential(key), options);
            }
            RetryCount = Math.Max(1, p.RetryCount) - 1;
        }
        /// <inheritdoc />
        public override string ToString() => Tos;
        
        /// <summary>
        /// The from address that emails are sent from
        /// </summary>
        public readonly String Sender;

        readonly WaitUntil Wait;
        readonly String Tos;
        readonly ExceptionTracker RetryFails = new ExceptionTracker();
        readonly ExceptionTracker SendFails = new ExceptionTracker();

        readonly int RetryCount;

        static bool CanRetry(Exception ex)
        {
            if (ex is RequestFailedException re)
            {
                var s = re.Status;
                return (s == 0) || (s == 408) || (s == 429) || (s >= 500);
            }
            return true;
        }

        /// <summary>
        /// Send an email, failed attempts that can be retried (timeouts, throttling and server errors) are retried up to the configured number of attempts.
        /// </summary>
        /// <param name="msg">The message to send</param>
        /// <returns>A task that completes when ACS has accepted the message (or when it has completed, if <see cref="MicrosoftAcsEmailParams.WaitForCompletion"/> is set)</returns>
        /// <exception cref="Exception">The message could not be sent, or the operation reported failure (only if waiting for completion)</exception>
        public async Task SendEmail(OutgoingEmailMessage msg)
        {
            using var _ = PerfMon.Track(nameof(SendEmail));

            var content = new EmailContent(msg.Subject?.Trim() ?? "");
            if (msg.IsHtml)
                content.Html = msg.Body ?? "";
            else
                content.PlainText = msg.Body ?? "";
            var m = new EmailMessage(Sender, new MailAddress(msg.Email).Address, content);
            var ret = RetryCount;
            var c = Client;
            for (int i = 0; ; ++i)
            {
                EmailSendOperation op;
                try
                {
                    op = await c.SendAsync(Wait, m).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if ((i == ret) || !CanRetry(ex))
                    {
                        SendFails.OnException(ex);
                        throw;
                    }
                    RetryFails.OnException(ex);
                    continue;
                }
                if (op.HasCompleted && (op.Value.Status == EmailSendStatus.Failed))
                {
                    var ex = new Exception("Failed to send email, operation " + op.Id.ToQuoted() + " failed!");
                    SendFails.OnException(ex);
                    throw ex;
                }
                return;
            }
        }

        /// <inheritdoc />
        public IEnumerable<Stats> GetStats()
        {
            foreach (var x in SendFails.GetStats(nameof(MicrosoftAcsEmailService), "Send."))
                yield return x;
            foreach (var x in RetryFails.GetStats(nameof(MicrosoftAcsEmailService), "Retry."))
                yield return x;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        readonly EmailClient Client;

        /// <inheritdoc />
        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(nameof(MicrosoftAcsEmailService));

    }

}
