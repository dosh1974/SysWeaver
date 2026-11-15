using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using SysWeaver.MicroService;
using SesMessage = Amazon.SimpleEmailV2.Model.Message;


namespace SysWeaver.Email
{


    /// <summary>
    /// Email sender service that sends emails using Amazon Simple Email Service (SES v2)
    /// </summary>
    public sealed class AmazonSesEmailService : IEmailSender, IHaveStats, IPerfMonitored
    {
        /// <summary>
        /// Create the service
        /// </summary>
        /// <param name="p">The parameters, <see cref="AmazonSesEmailParams.From"/> is required</param>
        /// <exception cref="Exception">The From address is empty</exception>
        public AmazonSesEmailService(AmazonSesEmailParams p = null)
        {
            p = p ?? new AmazonSesEmailParams();
            var from = p.From?.Trim();
            if (String.IsNullOrEmpty(from))
                throw new Exception(nameof(p.From) + " parameter may not be empty!");
            Sender = from;
            FromAddress = FormatFrom(from, p.FromName?.Trim());
            ConfigurationSet = String.IsNullOrEmpty(p.ConfigurationSet) ? null : p.ConfigurationSet;
            Tos = p.ToString();
            var config = new AmazonSimpleEmailServiceV2Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(p.Region),
                // Retries are handled by this service (so that they can be tracked)
                MaxErrorRetry = 0,
            };
            Client = p.GetUserPassword(out var user, out var password, false)
                ? new AmazonSimpleEmailServiceV2Client(new BasicAWSCredentials(user, password), config)
                : new AmazonSimpleEmailServiceV2Client(config);
            RetryCount = Math.Max(1, p.RetryCount) - 1;
        }
        /// <inheritdoc />
        public override string ToString() => Tos;

        /// <summary>
        /// The (trimmed) from address that emails are sent from
        /// </summary>
        public readonly String Sender;
        readonly String FromAddress;
        readonly String ConfigurationSet;
        readonly String Tos;
        readonly ExceptionTracker RetryFails = new ExceptionTracker();
        readonly ExceptionTracker SendFails = new ExceptionTracker();

        readonly int RetryCount;

        /// <summary>
        /// Build the "Name &lt;address&gt;" form, non-ascii names must be MIME encoded (RFC 2047) for SES
        /// </summary>
        static String FormatFrom(String address, String name)
        {
            if (String.IsNullOrEmpty(name))
                return new MailAddress(address).Address;
            bool isAscii = true;
            foreach (var c in name)
            {
                if (c < 32 || c > 126)
                {
                    isAscii = false;
                    break;
                }
            }
            name = isAscii
                ? String.Concat('"', name.Replace("\\", "\\\\").Replace("\"", "\\\""), '"')
                : String.Concat("=?utf-8?B?", Convert.ToBase64String(Encoding.UTF8.GetBytes(name)), "?=");
            return String.Concat(name, " <", new MailAddress(address).Address, '>');
        }

        static bool CanRetry(Exception ex)
        {
            if (ex is TooManyRequestsException)
                return true;
            if (ex is AmazonServiceException se)
                return se.ErrorType != ErrorType.Sender;
            return true;
        }

        /// <summary>
        /// Send an email (as a simple UTF-8 message), failed attempts that can be retried are retried up to the configured number of attempts
        /// </summary>
        /// <param name="msg">The message to send</param>
        /// <returns>A task that completes when SES has accepted the message</returns>
        /// <exception cref="Exception">The message could not be sent (the last error is thrown), or an address is invalid</exception>
        public async Task SendEmail(OutgoingEmailMessage msg)
        {
            using var _ = PerfMon.Track(nameof(SendEmail));
            var body = new Content
            {
                Data = msg.Body ?? "",
                Charset = "UTF-8",
            };
            var req = new SendEmailRequest
            {
                FromEmailAddress = FromAddress,
                Destination = new Destination
                {
                    ToAddresses = [new MailAddress(msg.Email).Address],
                },
                Content = new EmailContent
                {
                    Simple = new SesMessage
                    {
                        Subject = new Content
                        {
                            Data = msg.Subject?.Trim() ?? "",
                            Charset = "UTF-8",
                        },
                        Body = msg.IsHtml ? new Body { Html = body } : new Body { Text = body },
                    },
                },
                ConfigurationSetName = ConfigurationSet,
            };
            var ret = RetryCount;
            var c = Client;
            for (int i = 0; ; ++i)
            {
                try
                {
                    await c.SendEmailAsync(req).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    if ((i == ret) || !CanRetry(ex))
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
            foreach (var x in SendFails.GetStats(nameof(AmazonSesEmailService), "Send."))
                yield return x;
            foreach (var x in RetryFails.GetStats(nameof(AmazonSesEmailService), "Retry."))
                yield return x;
        }

        readonly AmazonSimpleEmailServiceV2Client Client;

        /// <inheritdoc />
        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(nameof(AmazonSesEmailService));

    }

}
