using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.Email
{

    [WebApiUrl("fakeEmail")]
    [WebMenuLink(null, "Debug/FakeEmail", "Fake email", "fakeEmail/index.html", "Show the fake email inbox")]
    public sealed class FakeEmailService : IEmailSender, IEmailReceiverService
    {
        public FakeEmailService(FakeEmailParams p = null)
        {
            p = p ?? new FakeEmailParams();
            Size = Math.Max(p.Size, 10);
            TimeOut = Math.Max(p.TimeOut, 0) * 1000;
            var maxPoll = Math.Max(p.MaxPoll, 10);
            Sender = p.From?.Trim();
            if (String.IsNullOrEmpty(Sender))
                Sender = "noreply@example.com";
            FailDomains = (p.FailDomains ?? []).Select(x => x?.Trim().TrimStart('@').ToLowerInvariant()).Where(x => !String.IsNullOrEmpty(x)).ToArray();
            Incoming = new(x => x.Id, x => x.System, x => x.Clone(), Size, TimeOut, maxPoll);
        }

        public void Dispose()
        {
            Incoming.Dispose();
            Changed.Dispose();
        }

        public override string ToString() => String.Concat(Sender, " (", Messages.Count, " emails)");

        readonly int Size;
        readonly int TimeOut;
        readonly String[] FailDomains;

        readonly TextMessageQueue<IncomingEmail> Incoming;
        readonly BlockUntilChange Changed = new BlockUntilChange();

        /// <summary>
        /// The from address when sending emails using this instance
        /// </summary>
        public readonly String Sender;

        static String Validate(String email, String what)
        {
            email = email?.Trim();
            if (String.IsNullOrEmpty(email))
                throw new Exception("No " + what + " email address supplied!");
            StringValidate.Email(email);
            return email;
        }

        bool Fails(String email)
        {
            var i = email.LastIndexOf('@');
            var domain = email.Substring(i + 1).ToLowerInvariant();
            return FailDomains.Any(x => domain == x || domain.EndsWith("." + x, StringComparison.Ordinal));
        }

        void Add(AnyEmail m)
        {
            lock (Messages)
            {
                Messages.AddFirst(m);
                Messages.Drain(Size);
            }
            Changed.Change();
        }

        AnyEmail SendInternal(String to, String subject, String message, bool isHtml)
        {
            to = Validate(to, "receiver");
            var m = new AnyEmail(false, Sender, to, subject?.Trim() ?? "", message ?? "", isHtml, null);
            if (Fails(to))
                m.Error = "Fake: delivery to " + to.ToQuoted() + " failed";
            Add(m);
            if (m.Error != null)
                throw new Exception(m.Error);
            return m;
        }

        /// <summary>
        /// Send a message
        /// </summary>
        /// <param name="to">The receiver email address</param>
        /// <param name="subject">The subject</param>
        /// <param name="message">The body</param>
        /// <param name="isHtml">True if the body is html</param>
        public Task Send(String to, String subject, String message, bool isHtml = false)
        {
            try
            {
                SendInternal(to, subject, message, isHtml);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                //  Fail like an async provider would
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// Send an email (as if the system sent it)
        /// </summary>
        /// <param name="email">The email details (From is ignored)</param>
        /// <returns>The id of the email</returns>
        [WebApi]
        public String SendEmail(FakeEmailRequest email)
            => SendInternal(email.To, email.Subject, email.Body, email.IsHtml).Id;

        /// <summary>
        /// Fake an incoming email
        /// </summary>
        /// <param name="email">Email details (From is the sender, To is the address that received the email)</param>
        /// <returns>The id of the email</returns>
        [WebApi]
        public String FakeIncoming(FakeEmailRequest email)
        {
            var from = Validate(email.From, "sender");
            var to = String.IsNullOrWhiteSpace(email.To) ? Sender : Validate(email.To, "receiver");
            var m = new AnyEmail(true, from, to, email.Subject?.Trim() ?? "", email.Body ?? "", email.IsHtml, email.System)
            {
                SenderName = String.IsNullOrWhiteSpace(email.SenderName) ? "Fake user" : email.SenderName.Trim(),
                InReplyTo = email.InReplyTo,
            };
            Add(m);
            Incoming.Add(m.Clone());
            return m.Id;
        }

        /// <summary>
        /// Poll for any new emails, blocks until new emails are available
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new emails</returns>
        [WebApi]
        public Task<IncomingEmail[]> GetIncoming(GetIncomingEmailRequest request)
            => Incoming.Poll(request?.LastId, request?.System);

        /// <summary>
        /// Get all emails (without bodies), blocks until there is a change if the supplied version is the current one
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The emails</returns>
        [WebApi]
        public async Task<FakeEmailList> GetEmails(FakeEmailListRequest request)
        {
            var v = request?.Version ?? 0;
            var cc = Changed.Cc;
            if ((cc == v) && (TimeOut > 0))
                cc = await Changed.WaitForChange(cc, TimeOut).ConfigureAwait(false);
            if (cc == v)
                return new FakeEmailList { Version = cc, Unchanged = true, From = Sender };
            FakeEmailSummary[] emails;
            lock (Messages)
            {
                //  Read the version inside the lock, a change after this will be picked up by the next call
                cc = Changed.Cc;
                emails = Messages.Select(x => new FakeEmailSummary(x)).ToArray();
            }
            return new FakeEmailList
            {
                Version = cc,
                From = Sender,
                Emails = emails,
            };
        }

        /// <summary>
        /// Get an email (including the body)
        /// </summary>
        /// <param name="request">The email</param>
        /// <returns>The email</returns>
        [WebApi]
        public AnyEmail GetEmail(FakeEmailIdRequest request)
        {
            lock (Messages)
                return Messages.FirstOrDefault(x => x.Id == request?.Id) ?? throw new Exception("No email with the id " + request?.Id.ToQuoted() + " found!");
        }

        /// <summary>
        /// Delete an email from the list (it's not removed from the incoming queue)
        /// </summary>
        /// <param name="request">The email</param>
        [WebApi]
        public void DeleteEmail(FakeEmailIdRequest request)
        {
            bool removed;
            lock (Messages)
            {
                var m = Messages.FirstOrDefault(x => x.Id == request?.Id);
                removed = (m != null) && Messages.Remove(m);
            }
            if (removed)
                Changed.Change();
        }

        /// <summary>
        /// Delete all emails from the list (they're not removed from the incoming queue)
        /// </summary>
        [WebApi]
        public void Clear()
        {
            lock (Messages)
                Messages.Clear();
            Changed.Change();
        }

        /// <summary>
        /// A table with all emails
        /// </summary>
        /// <param name="request">The table request</param>
        /// <returns></returns>
        [WebApi]
        [WebApiRequestCache(1)]
        [WebMenuTable(null, "Emails", "Emails", "The list of emails")]
        public TableData MessageTable(TableDataRequest request)
            => TableDataTools.Get(request, 2000, Messages, "Emails");


        readonly LinkedList<AnyEmail> Messages = new ();

    }


}
