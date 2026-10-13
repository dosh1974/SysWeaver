using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.EmailReader
{

    /// <summary>
    /// Receive emails from a mailbox using IMAP.
    /// Unread (unseen) emails in a folder are read and marked as read, deleted or moved, IDLE is used to get notified of new emails (if supported, else the folder is checked periodically).
    /// New emails are queued for <see cref="GetIncoming"/>.
    /// </summary>
    public sealed class ImapEmailReaderService : IEmailReceiverService, IHaveStats, IPerfMonitored, IDisposable
    {
        const String LogPrefix = "[Imap] ";

        /// <summary>
        /// IDLE is restarted with this interval (servers may drop idle connections after 30 minutes)
        /// </summary>
        const int IdleRestart = 9 * 60 * 1000;

        public ImapEmailReaderService(ImapEmailReaderParams p = null, IMessageHost msg = null)
        {
            p = p ?? new ImapEmailReaderParams();
            Msg = msg;
            Server = p.Server?.Trim();
            if (String.IsNullOrEmpty(Server))
                throw new Exception(nameof(p.Server) + " parameter may not be empty!");
            p.GetUserPassword(out User, out Password, true);
            Port = Math.Max(0, p.Port);
            Security = p.Security switch
            {
                ImapSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
                ImapSecurity.StartTls => SecureSocketOptions.StartTls,
                ImapSecurity.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
                ImapSecurity.None => SecureSocketOptions.None,
                _ => throw new Exception("Unknown " + nameof(p.Security) + " " + p.Security.ToString().ToQuoted()),
            };
            AcceptAnyCertificate = p.AcceptAnyCertificate;
            Folder = String.IsNullOrWhiteSpace(p.Folder) ? null : p.Folder.Trim();
            AfterRead = p.AfterRead;
            MoveTo = String.IsNullOrWhiteSpace(p.MoveTo) ? null : p.MoveTo.Trim();
            if ((AfterRead == ImapAfterRead.Move) && (MoveTo == null))
                throw new Exception(nameof(p.MoveTo) + " parameter may not be empty when " + nameof(p.AfterRead) + " is " + nameof(ImapAfterRead.Move));
            PreferHtml = p.PreferHtml;
            UseIdle = p.UseIdle;
            PollInterval = Math.Max(1, p.PollInterval) * 1000;
            PollRetryDelay = Math.Max(1, p.PollRetryDelay) * 1000;
            ServerTimeOut = Math.Max(1, p.ServerTimeOut) * 1000;
            MaxDownload = Math.Max(1, p.MaxDownload);
            System = String.IsNullOrWhiteSpace(p.System) ? null : p.System.Trim();
            foreach (var a in p.Addresses ?? [])
            {
                var address = a?.Address?.Trim();
                if (String.IsNullOrEmpty(address))
                    throw new Exception(nameof(ImapAddressParams.Address) + " parameter may not be empty!");
                StringValidate.Email(address);
                Addresses[address] = String.IsNullOrWhiteSpace(a.System) ? System : a.System.Trim();
            }
            Size = Math.Max(p.Size, 10);
            var timeOut = Math.Max(p.TimeOut, 0) * 1000;
            var maxPoll = Math.Max(p.MaxPoll, 10);
            Incoming = new(x => x.Id, x => x.System, x => x.Clone(), Size, timeOut, maxPoll);
            Tos = p.ToString();
            _ = RunLoop(Cancel.Token);
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
        readonly String Folder;
        readonly ImapAfterRead AfterRead;
        readonly String MoveTo;
        readonly bool PreferHtml;
        readonly bool UseIdle;
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
        /// Key = the UID of unseen emails that have been read, Value = true if it was processed, false if it failed (left as unseen).
        /// Only accessed by the run loop.
        /// </summary>
        readonly Dictionary<uint, bool> Handled = new();

        /// <summary>
        /// The UID validity of the folder that Handled is valid for
        /// </summary>
        uint UidValidity;

        async Task RunLoop(CancellationToken cancel)
        {
            //  Let the constructor finish
            await Task.Yield();
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    await Session(cancel).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (cancel.IsCancellationRequested)
                        break;
                    PollFails.OnException(ex);
                    Msg?.AddMessage(LogPrefix + "Failed to read the mailbox at " + Server.ToQuoted(), ex, MessageLevels.Warning);
                }
                try
                {
                    await Task.Delay(PollRetryDelay, cancel).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Connect to the server and keep reading new emails (until cancelled or a failure)
        /// </summary>
        async Task Session(CancellationToken cancel)
        {
            using var client = new ImapClient();
            client.Timeout = ServerTimeOut;
            if (AcceptAnyCertificate)
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            await client.ConnectAsync(Server, Port, Security, cancel).ConfigureAwait(false);
            await client.AuthenticateAsync(User, Password, cancel).ConfigureAwait(false);
            var folder = Folder == null ? client.Inbox : await client.GetFolderAsync(Folder, cancel).ConfigureAwait(false);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancel).ConfigureAwait(false);
            var moveTo = AfterRead == ImapAfterRead.Move ? await client.GetFolderAsync(MoveTo, cancel).ConfigureAwait(false) : null;
            var idle = UseIdle && client.Capabilities.HasFlag(ImapCapabilities.Idle);
            var uidPlus = client.Capabilities.HasFlag(ImapCapabilities.UidPlus);
            Msg?.AddMessage(LogPrefix + "Reading " + folder.FullName.ToQuoted() + " at " + Server.ToQuoted() + (idle ? " using IDLE" : " every " + (PollInterval / 1000) + " seconds"), MessageLevels.Debug);
            if (folder.UidValidity != UidValidity)
            {
                Handled.Clear();
                UidValidity = folder.UidValidity;
            }
            try
            {
                for (; ; )
                {
                    cancel.ThrowIfCancellationRequested();
                    //  Keep checking while there are more emails than can be downloaded in one check
                    if (await Check(folder, moveTo, uidPlus, cancel).ConfigureAwait(false))
                        continue;
                    if (idle)
                    {
                        using var done = new CancellationTokenSource(IdleRestart);
                        EventHandler<EventArgs> onChange = (s, e) => done.Cancel();
                        folder.CountChanged += onChange;
                        try
                        {
                            await client.IdleAsync(done.Token, cancel).ConfigureAwait(false);
                        }
                        finally
                        {
                            folder.CountChanged -= onChange;
                        }
                    }
                    else
                    {
                        await Task.Delay(PollInterval, cancel).ConfigureAwait(false);
                        await client.NoOpAsync(cancel).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (client.IsConnected && !cancel.IsCancellationRequested)
                {
                    try
                    {
                        await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Read new (unseen) emails
        /// </summary>
        /// <returns>True if there are more emails to read</returns>
        async Task<bool> Check(IMailFolder folder, IMailFolder moveTo, bool uidPlus, CancellationToken cancel)
        {
            using var _ = PerfMon.Track(nameof(Check));
            var uids = await folder.SearchAsync(SearchQuery.NotSeen, cancel).ConfigureAwait(false);
            //  Forget emails that are no longer unseen
            var unseen = new HashSet<uint>(uids.Select(x => x.Id));
            foreach (var x in Handled.Keys.ToArray())
                if (!unseen.Contains(x))
                    Handled.Remove(x);
            List<UniqueId> done = new();
            List<ImapEmail> recs = new();
            int downloaded = 0;
            bool more = false;
            foreach (var uid in uids)
            {
                if (Handled.TryGetValue(uid.Id, out var processed))
                {
                    //  Already read, but the after read action failed
                    if (processed)
                        done.Add(uid);
                    continue;
                }
                if (downloaded >= MaxDownload)
                {
                    more = true;
                    break;
                }
                //  Doesn't set the seen flag (BODY.PEEK)
                var m = await folder.GetMessageAsync(uid, cancel).ConfigureAwait(false);
                ++downloaded;
                var rec = Process(m, uid.Id.ToString());
                Handled[uid.Id] = rec != null;
                //  Emails that fails to process are left as unseen
                if (rec == null)
                    continue;
                done.Add(uid);
                recs.Add(rec);
            }
            if ((done.Count <= 0) || (AfterRead == ImapAfterRead.Nothing))
                return more;
            String status;
            switch (AfterRead)
            {
                case ImapAfterRead.MarkSeen:
                    await folder.AddFlagsAsync(done, MessageFlags.Seen, true, cancel).ConfigureAwait(false);
                    status = "seen";
                    break;
                case ImapAfterRead.Delete:
                    await folder.AddFlagsAsync(done, MessageFlags.Seen | MessageFlags.Deleted, true, cancel).ConfigureAwait(false);
                    //  Only expunge our emails if possible (other emails may be flagged as deleted by other clients)
                    if (uidPlus)
                        await folder.ExpungeAsync(done, cancel).ConfigureAwait(false);
                    else
                        await folder.ExpungeAsync(cancel).ConfigureAwait(false);
                    status = "deleted";
                    break;
                case ImapAfterRead.Move:
                    await folder.AddFlagsAsync(done, MessageFlags.Seen, true, cancel).ConfigureAwait(false);
                    await folder.MoveToAsync(done, moveTo, cancel).ConfigureAwait(false);
                    status = "moved";
                    break;
                default:
                    throw new Exception("Unknown " + nameof(AfterRead) + " " + AfterRead.ToString().ToQuoted());
            }
            foreach (var x in recs)
                x.Status = status;
            return more;
        }

        /// <summary>
        /// Process a downloaded email
        /// </summary>
        /// <returns>The record, or null if the email couldn't be processed</returns>
        ImapEmail Process(MimeMessage m, String uid)
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
                var rec = new ImapEmail
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
                Msg?.AddMessage(LogPrefix + "Failed to process the email " + (m.MessageId ?? uid).ToQuoted() + ", it's left as unread", ex, MessageLevels.Warning);
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

        static String NewId() => "Imap" + Interlocked.Increment(ref Counter);

        static long Counter = DateTime.UtcNow.Ticks - new DateTime(2025, 1, 1).Ticks;

        #endregion//Checking

        #region Receiving

        readonly TextMessageQueue<IncomingEmail> Incoming;

        readonly LinkedList<ImapEmail> Messages = new();

        void AddRecord(ImapEmail rec)
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
        [WebMenuTable(null, "Emails", "IMAP emails", "The list of emails received using IMAP")]
        public TableData MessageTable(TableDataRequest request)
            => TableDataTools.Get(request, 2000, Messages, "IMAP emails");

        #endregion//Receiving

        #region Stats

        readonly ExceptionTracker PollFails = new ExceptionTracker();
        readonly ExceptionTracker ProcessFails = new ExceptionTracker();

        public IEnumerable<Stats> GetStats()
        {
            const String n = nameof(ImapEmailReaderService);
            foreach (var x in PollFails.GetStats(n, "Poll."))
                yield return x;
            foreach (var x in ProcessFails.GetStats(n, "Process."))
                yield return x;
        }

        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(nameof(ImapEmailReaderService));

        #endregion//Stats
    }
}
