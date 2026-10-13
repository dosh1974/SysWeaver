using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Pop3;
using MailKit.Security;
using MimeKit;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.EmailReader
{

    /// <summary>
    /// Receive emails from a mailbox using POP3.
    /// The mailbox is checked periodically, new emails are queued for <see cref="GetIncoming"/>.
    /// </summary>
    public sealed class Pop3EmailReaderService : IEmailReceiverService, IHaveStats, IPerfMonitored, IDisposable
    {
        const String LogPrefix = "[Pop3] ";

        public Pop3EmailReaderService(Pop3EmailReaderParams p = null, IMessageHost msg = null)
        {
            p = p ?? new Pop3EmailReaderParams();
            Msg = msg;
            Server = p.Server?.Trim();
            if (String.IsNullOrEmpty(Server))
                throw new Exception(nameof(p.Server) + " parameter may not be empty!");
            p.GetUserPassword(out User, out Password, true);
            Port = Math.Max(0, p.Port);
            Security = p.Security switch
            {
                Pop3Security.SslOnConnect => SecureSocketOptions.SslOnConnect,
                Pop3Security.StartTls => SecureSocketOptions.StartTls,
                Pop3Security.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
                Pop3Security.None => SecureSocketOptions.None,
                _ => throw new Exception("Unknown " + nameof(p.Security) + " " + p.Security.ToString().ToQuoted()),
            };
            AcceptAnyCertificate = p.AcceptAnyCertificate;
            DeleteAfterRead = p.DeleteAfterRead;
            PreferHtml = p.PreferHtml;
            PollInterval = Math.Max(1, p.PollInterval) * 1000;
            PollRetryDelay = Math.Max(1, p.PollRetryDelay) * 1000;
            ServerTimeOut = Math.Max(1, p.ServerTimeOut) * 1000;
            MaxDownload = Math.Max(1, p.MaxDownload);
            System = String.IsNullOrWhiteSpace(p.System) ? null : p.System.Trim();
            foreach (var a in p.Addresses ?? [])
            {
                var address = a?.Address?.Trim();
                if (String.IsNullOrEmpty(address))
                    throw new Exception(nameof(Pop3AddressParams.Address) + " parameter may not be empty!");
                StringValidate.Email(address);
                Addresses[address] = String.IsNullOrWhiteSpace(a.System) ? System : a.System.Trim();
            }
            Size = Math.Max(p.Size, 10);
            var timeOut = Math.Max(p.TimeOut, 0) * 1000;
            var maxPoll = Math.Max(p.MaxPoll, 10);
            Incoming = new(x => x.Id, x => x.System, x => x.Clone(), Size, timeOut, maxPoll);
            Tos = p.ToString();
            _ = PollLoop(Cancel.Token);
            Msg?.AddMessage(LogPrefix + "Checking " + Server.ToQuoted() + " every " + (PollInterval / 1000) + " seconds" + (DeleteAfterRead ? "" : ", emails are left on the server"), MessageLevels.Debug);
        }

        public override string ToString() => Tos;

        public void Dispose()
        {
            Cancel.Cancel();
            Incoming.Dispose();
        }

        readonly IMessageHost Msg;
        readonly String Tos;
        readonly String Server;
        readonly int Port;
        readonly String User;
        readonly String Password;
        readonly SecureSocketOptions Security;
        readonly bool AcceptAnyCertificate;
        readonly bool DeleteAfterRead;
        readonly bool PreferHtml;
        readonly int PollInterval;
        readonly int PollRetryDelay;
        readonly int ServerTimeOut;
        readonly int MaxDownload;
        readonly String System;
        readonly int Size;
        readonly CancellationTokenSource Cancel = new();

        /// <summary>
        /// Key = email address, Value = the system
        /// </summary>
        readonly Dictionary<String, String> Addresses = new(StringComparer.OrdinalIgnoreCase);

        #region Checking

        /// <summary>
        /// Key = the unique id (UIDL) of emails that have been read, Value = true if it was processed, false if it failed (left on the server).
        /// Only accessed by the poll loop.
        /// </summary>
        readonly Dictionary<String, bool> ReadUids = new(StringComparer.Ordinal);

        async Task PollLoop(CancellationToken cancel)
        {
            //  Let the constructor finish
            await Task.Yield();
            while (!cancel.IsCancellationRequested)
            {
                var delay = PollInterval;
                try
                {
                    await Check(cancel).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (cancel.IsCancellationRequested)
                        break;
                    PollFails.OnException(ex);
                    Msg?.AddMessage(LogPrefix + "Failed to check the mailbox at " + Server.ToQuoted(), ex, MessageLevels.Warning);
                    delay = PollRetryDelay;
                }
                try
                {
                    await Task.Delay(delay, cancel).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Connect to the server, read (and delete) new emails
        /// </summary>
        async Task Check(CancellationToken cancel)
        {
            using var _ = PerfMon.Track(nameof(Check));
            using var client = new Pop3Client();
            client.Timeout = ServerTimeOut;
            if (AcceptAnyCertificate)
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            await client.ConnectAsync(Server, Port, Security, cancel).ConfigureAwait(false);
            await client.AuthenticateAsync(User, Password, cancel).ConfigureAwait(false);
            var count = client.Count;
            IList<String> uids = null;
            if ((count > 0) && client.Capabilities.HasFlag(Pop3Capabilities.UIDL))
                uids = await client.GetMessageUidsAsync(cancel).ConfigureAwait(false);
            //  Forget emails that are no longer on the server
            if ((uids != null) || (count <= 0))
            {
                var onServer = new HashSet<String>(uids ?? [], StringComparer.Ordinal);
                foreach (var x in ReadUids.Keys.ToArray())
                    if (!onServer.Contains(x))
                        ReadUids.Remove(x);
            }
            List<Pop3Email> deleted = null;
            int downloaded = 0;
            for (int i = 0; (i < count) && (downloaded < MaxDownload); ++i)
            {
                var uid = uids?[i];
                if ((uid != null) && ReadUids.TryGetValue(uid, out var processed))
                {
                    //  Already read, but the delete wasn't committed (the session wasn't closed properly)
                    if (processed && DeleteAfterRead)
                        await client.DeleteMessageAsync(i, cancel).ConfigureAwait(false);
                    continue;
                }
                var m = await client.GetMessageAsync(i, cancel).ConfigureAwait(false);
                ++downloaded;
                var rec = Process(m, uid);
                if (uid != null)
                    ReadUids[uid] = rec != null;
                //  Emails that fails to process are left on the server
                if ((rec == null) || !DeleteAfterRead)
                    continue;
                await client.DeleteMessageAsync(i, cancel).ConfigureAwait(false);
                deleted ??= new();
                deleted.Add(rec);
            }
            if ((uids == null) && (count > 0) && !DeleteAfterRead && !WarnedNoUidl)
            {
                WarnedNoUidl = true;
                Msg?.AddMessage(LogPrefix + "The server " + Server.ToQuoted() + " doesn't support UIDL, all emails are read on every check (duplicates with the same Message-Id are ignored), consider using " + nameof(Pop3EmailReaderParams.DeleteAfterRead), MessageLevels.Warning);
            }
            //  Commits the deletes
            await client.DisconnectAsync(true, cancel).ConfigureAwait(false);
            if (deleted != null)
                foreach (var x in deleted)
                    x.Status = "deleted";
        }

        bool WarnedNoUidl;

        /// <summary>
        /// Process a downloaded email
        /// </summary>
        /// <returns>The record, or null if the email couldn't be processed</returns>
        Pop3Email Process(MimeMessage m, String uid)
        {
            using var _ = PerfMon.Track(nameof(Process));
            try
            {
                var from = m.From.Mailboxes.FirstOrDefault();
                var to = GetTo(m, out var system);
                var html = m.HtmlBody;
                var text = m.TextBody;
                var useHtml = (html != null) && (PreferHtml || (text == null));
                var id = m.MessageId;
                if (String.IsNullOrWhiteSpace(id))
                    id = NewId();
                var rec = new Pop3Email
                {
                    Id = id,
                    Uid = uid,
                    From = from?.Address,
                    SenderName = String.IsNullOrWhiteSpace(from?.Name) ? null : from.Name,
                    To = to,
                    Subject = m.Subject ?? "",
                    Body = (useHtml ? html : text) ?? "",
                    IsHtml = useHtml,
                    Time = DateTime.UtcNow,
                    System = system,
                    InReplyTo = m.InReplyTo,
                    Status = "received",
                };
                AddRecord(rec);
                Incoming.Add(rec.Clone());
                return rec;
            }
            catch (Exception ex)
            {
                ProcessFails.OnException(ex);
                Msg?.AddMessage(LogPrefix + "Failed to process the email " + (m.MessageId ?? uid).ToQuoted() + ", it's left on the server", ex, MessageLevels.Warning);
                return null;
            }
        }

        /// <summary>
        /// Headers that some servers add with the envelope receiver
        /// </summary>
        static readonly String[] DeliveredToHeaders = ["Delivered-To", "X-Original-To", "Envelope-To"];

        /// <summary>
        /// Get the address that the email was sent to (a configured address if any matches) and the system
        /// </summary>
        String GetTo(MimeMessage m, out String system)
        {
            var addresses = DeliveredToHeaders.SelectMany(h => m.Headers.Where(x => x.Field.FastEquals(h)).Select(x => x.Value?.Trim()))
                .Concat(m.To.Mailboxes.Select(x => x.Address))
                .Concat(m.Cc.Mailboxes.Select(x => x.Address))
                .Where(x => !String.IsNullOrEmpty(x));
            foreach (var a in addresses)
            {
                if (Addresses.TryGetValue(a, out system))
                    return a;
            }
            system = System;
            return m.To.Mailboxes.FirstOrDefault()?.Address;
        }

        static String NewId() => "Pop3" + Interlocked.Increment(ref Counter);

        static long Counter = DateTime.UtcNow.Ticks - new DateTime(2025, 1, 1).Ticks;

        #endregion//Checking

        #region Receiving

        readonly TextMessageQueue<IncomingEmail> Incoming;

        readonly LinkedList<Pop3Email> Messages = new();

        void AddRecord(Pop3Email rec)
        {
            lock (Messages)
            {
                Messages.AddFirst(rec);
                Messages.Drain(Size);
            }
        }

        /// <summary>
        /// Poll for any new emails, blocks until new emails are available
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new emails</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        public Task<IncomingEmail[]> GetIncoming(GetIncomingEmailRequest request)
            => Incoming.Poll(request?.LastId, request?.System);

        /// <summary>
        /// A table with all received emails
        /// </summary>
        /// <param name="request">The table request</param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiRequestCache(1)]
        [WebMenuTable(null, "Emails", "POP3 emails", "The list of emails received using POP3")]
        public TableData MessageTable(TableDataRequest request)
            => TableDataTools.Get(request, 2000, Messages, "POP3 emails");

        #endregion//Receiving

        #region Stats

        readonly ExceptionTracker PollFails = new ExceptionTracker();
        readonly ExceptionTracker ProcessFails = new ExceptionTracker();

        public IEnumerable<Stats> GetStats()
        {
            const String n = nameof(Pop3EmailReaderService);
            foreach (var x in PollFails.GetStats(n, "Poll."))
                yield return x;
            foreach (var x in ProcessFails.GetStats(n, "Process."))
                yield return x;
        }

        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(nameof(Pop3EmailReaderService));

        #endregion//Stats
    }
}
