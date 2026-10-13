using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{


    public sealed class SmtpEmailService : IEmailService, IDisposable, IHaveStats, IPerfMonitored
    {
        public SmtpEmailService(SmtpEmailParams p = null)
        {
            p = p ?? new SmtpEmailParams();
            p.GetUserPassword(out var user, out var password);
            User = user;
            Tos = String.Concat("User: ", user.ToQuoted(), ", ", p);
            Client = new SmtpClient(p.Server)
            {
                Port = p.Port,
                Credentials = new NetworkCredential(user, password),
                EnableSsl = p.EnableSSL,
            };
            RetryCount = Math.Max(1, p.RetryCount) - 1;
        }
        public override string ToString() => Tos;
        public string From => User;

        readonly String User;
        readonly String Tos;
        readonly ExceptionTracker RetryFails = new ExceptionTracker();
        readonly ExceptionTracker SendFails = new ExceptionTracker();

        readonly int RetryCount;

        public async Task Send(String to, String subject, String message, bool isHtml = false)
        {
            using var _ = PerfMon.Track(nameof(Send));
            using var m = new MailMessage
            {
                From = new MailAddress(User),
                Subject = subject?.Trim(),
                Body = message,
                IsBodyHtml = isHtml,
            };
            m.To.Add(new MailAddress(to));
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

        public void Dispose()
        {
            Client.Dispose();
        }

        public IEnumerable<Stats> GetStats()
        {
            foreach (var x in SendFails.GetStats(nameof(SmtpEmailService), "Send."))
                yield return x;
            foreach (var x in RetryFails.GetStats(nameof(SmtpEmailService), "Retry."))
                yield return x;
        }

        readonly SmtpClient Client;

        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(nameof(SmtpEmailService));

    }

}
