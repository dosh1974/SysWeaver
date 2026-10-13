using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Security;
using Twilio.Types;

namespace SysWeaver.TextMessage
{

    /// <summary>
    /// Send and receive text messages (SMS, MMS, WhatsApp, RCS) using Twilio Programmable Messaging.
    /// Incoming messages and status callbacks are posted by Twilio to webhooks served by this service (see <see cref="TwilioTextMessageParams.WebhookUrl"/>),
    /// they are published using <see cref="ITextMessageEvents"/> and queued for <see cref="GetIncoming"/> / <see cref="GetStatusUpdates"/>.
    /// </summary>
    public sealed partial class TwilioTextMessageService : ITextMessageSender, ITextMessageReceiverService, ITextMessageStatusService, ITextMessageEvents, IHttpServerModule, IHaveStats, IPerfMonitored, IDisposable
    {
        const String LogPrefix = "[Twilio] ";
        const String WhatsAppPrefix = "whatsapp:";
        const String RcsPrefix = "rcs:";

        public TwilioTextMessageService(TwilioTextMessageParams p = null, IMessageHost msg = null)
        {
            p = p ?? new TwilioTextMessageParams();
            Msg = msg;
            Size = Math.Max(p.Size, 10);
            var timeOut = Math.Max(p.TimeOut, 0) * 1000;
            var maxPoll = Math.Max(p.MaxPoll, 10);
            Incoming = new(x => x.Id, x => x.System, x => x.Clone(), Size, timeOut, maxPoll);
            Statuses = new(x => x.Id, x => x.System, x => x.Clone(), Size, timeOut, maxPoll);
            RetryCount = Math.Max(1, p.RetryCount) - 1;
            NotOnChannelTime = TimeSpan.FromHours(Math.Max(0, p.NotOnChannelHours));
            SendMessageIntent = p.SendMessageIntent;
            var t = p.WhatsAppTemplate;
            if (!String.IsNullOrWhiteSpace(t?.ContentSid))
                WhatsAppTemplate = t;
            Tos = p.ToString();
            //  Credentials
            p.GetUserPassword(out var user, out var password);
            var isApiKey = user.StartsWith("SK", StringComparison.Ordinal);
            var accountSid = p.AccountSid?.Trim();
            if (String.IsNullOrEmpty(accountSid))
            {
                if (isApiKey)
                    throw new Exception(nameof(p.AccountSid) + " parameter is required when using an API key!");
                accountSid = user;
            }
            AccountSid = accountSid;
            var authToken = p.AuthToken?.GetApiKey(false);
            if (String.IsNullOrEmpty(authToken) && !isApiKey)
                authToken = password;
            if (String.IsNullOrEmpty(authToken))
                Msg?.AddMessage(LogPrefix + "No " + nameof(p.AuthToken) + " specified, webhook requests can't be validated so incoming messages and statuses are ignored", MessageLevels.Warning);
            else
                Validator = new RequestValidator(authToken);
            Client = new TwilioRestClient(user, password, accountSid);
            //  Senders
            var senders = p.Senders;
            if ((senders == null) || (senders.Length <= 0))
                throw new Exception(nameof(p.Senders) + " parameter must contain at least one sender!");
            Senders = senders.Select(x => new Sender(x)).ToArray();
            //  Webhooks
            var url = (p.WebhookUrl ?? "").Trim().Trim('/');
            if (url.Length <= 0)
                throw new Exception(nameof(p.WebhookUrl) + " parameter may not be empty!");
            WebhookUrl = url;
            IncomingUrl = url + "/incoming";
            StatusUrl = url + "/status";
            OnlyForPrefixes = [url];
            var pub = p.PublicUrl?.Trim();
            if (!String.IsNullOrEmpty(pub))
            {
                pub = pub.TrimEnd('/') + '/';
                if (!Uri.TryCreate(pub, UriKind.Absolute, out var _))
                    throw new Exception(nameof(p.PublicUrl) + " parameter must be an absolute url!");
                PublicUrl = pub;
            }
            IncomingHandler = new WebhookRequestHandler("Incoming", HandleIncoming);
            StatusHandler = new WebhookRequestHandler("Status", HandleStatus);
            Msg?.AddMessage(LogPrefix + "Using " + Senders.Length + " sender(s), webhooks at " + url.ToQuoted(), MessageLevels.Debug);
        }

        public override string ToString() => Tos;

        public void Dispose()
        {
            Incoming.Dispose();
            Statuses.Dispose();
        }

        readonly IMessageHost Msg;
        readonly String Tos;
        readonly int Size;
        readonly int RetryCount;
        readonly TimeSpan NotOnChannelTime;
        readonly bool SendMessageIntent;
        readonly TwilioTemplateParams WhatsAppTemplate;
        readonly String AccountSid;
        readonly RequestValidator Validator;
        readonly ITwilioRestClient Client;

        #region Senders

        sealed class Sender
        {
            public Sender(TwilioSenderParams p)
            {
                var from = p?.From?.Trim();
                var ms = p?.MessagingServiceSid?.Trim();
                if (String.IsNullOrEmpty(from) && String.IsNullOrEmpty(ms))
                    throw new Exception("A sender must have a " + nameof(p.From) + " and / or a " + nameof(p.MessagingServiceSid) + "!");
                if (!String.IsNullOrEmpty(from))
                {
                    //  Phone numbers must be E.164 (no spaces etc), keep any channel prefix ("whatsapp:")
                    var i = from.IndexOf(':');
                    var prefix = i < 0 ? "" : from.Substring(0, i + 1);
                    var number = from.Substring(i + 1).Trim();
                    if (number.StartsWith('+'))
                        from = String.Concat(prefix, '+', GetDigits(number));
                }
                From = String.IsNullOrEmpty(from) ? null : from;
                MessagingServiceSid = String.IsNullOrEmpty(ms) ? null : ms;
                Key = From ?? MessagingServiceSid;
                System = String.IsNullOrEmpty(p.System) ? null : p.System;
                var channel = p.Channel;
                if (channel == TextMessageChannels.Any)
                {
                    if ((From != null) && From.StartsWith(WhatsAppPrefix, StringComparison.OrdinalIgnoreCase))
                        channel = TextMessageChannels.WhatsApp;
                    else if ((From != null) && From.StartsWith(RcsPrefix, StringComparison.OrdinalIgnoreCase))
                        channel = TextMessageChannels.Rcs;
                    else
                        channel = TextMessageChannels.Sms;
                }
                Channel = channel;
                var countries = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                Display = Key;
                if (From != null)
                {
                    var number = StripPrefix(From);
                    if (number.StartsWith('+'))
                    {
                        try
                        {
                            PhonePrefix.GetValidatedPhoneNumber(out var _, out var iso, out var pre, out var num, number);
                            Display = String.Join(' ', pre, num);
                            Digits = GetDigits(number);
                            if (!String.IsNullOrEmpty(iso))
                                countries.Add(iso);
                        }
                        catch
                        {
                        }
                    }
                }
                foreach (var x in p.Countries ?? [])
                    if (!String.IsNullOrWhiteSpace(x))
                        countries.Add(x.Trim());
                Countries = countries;
            }

            public override string ToString() => String.Concat(Display, " (", Channel, ')');

            /// <summary>
            /// Unique key (the From address or the messaging service sid)
            /// </summary>
            public readonly String Key;
            public readonly String From;
            public readonly String MessagingServiceSid;
            public readonly TextMessageChannels Channel;
            public readonly String Display;
            /// <summary>
            /// The digits of the phone number (if From is a phone number)
            /// </summary>
            public readonly String Digits;
            public readonly String System;
            public readonly IReadOnlySet<String> Countries;

            /// <summary>
            /// The "To" address for a recipient phone number (E.164)
            /// </summary>
            public String ToAddress(String e164) => Channel switch
            {
                TextMessageChannels.WhatsApp => WhatsAppPrefix + e164,
                _ => e164,
            };
        }

        readonly Sender[] Senders;

        static String StripPrefix(String address)
        {
            var i = address.IndexOf(':');
            return i < 0 ? address : address.Substring(i + 1);
        }

        static String GetDigits(String s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                if ((c >= '0') && (c <= '9'))
                    sb.Append(c);
            return sb.ToString();
        }

        /// <summary>
        /// Find the sender that received an incoming message (by the "To" address or the messaging service)
        /// </summary>
        Sender FindReceiver(String to, String messagingServiceSid)
        {
            if (!String.IsNullOrEmpty(to))
            {
                foreach (var s in Senders)
                    if ((s.From != null) && String.Equals(s.From, to, StringComparison.OrdinalIgnoreCase))
                        return s;
                var digits = GetDigits(StripPrefix(to));
                var isWa = to.StartsWith(WhatsAppPrefix, StringComparison.OrdinalIgnoreCase);
                if (digits.Length > 0)
                    foreach (var s in Senders)
                        if ((s.Digits == digits) && ((s.Channel == TextMessageChannels.WhatsApp) == isWa))
                            return s;
            }
            if (!String.IsNullOrEmpty(messagingServiceSid))
                foreach (var s in Senders)
                    if (s.MessagingServiceSid == messagingServiceSid)
                        return s;
            return null;
        }

        /// <summary>
        /// Key = sender key + '|' + user digits, Value = time of the last incoming message (used to track the WhatsApp customer service window)
        /// </summary>
        readonly ConcurrentDictionary<String, DateTime> LastIncoming = new(StringComparer.Ordinal);

        static String WindowKey(Sender s, String digits) => String.Concat(s.Key, '|', digits);

        static readonly TimeSpan WindowLength = TimeSpan.FromHours(24) - TimeSpan.FromMinutes(5);

        bool IsWindowOpen(Sender s, String digits)
            => LastIncoming.TryGetValue(WindowKey(s, digits), out var t) && ((DateTime.UtcNow - t) < WindowLength);

        /// <summary>
        /// Key = channel + '|' + phone number digits, Value = time when the channel should be tried again
        /// </summary>
        readonly ConcurrentDictionary<String, DateTime> NotOnChannel = new(StringComparer.Ordinal);

        static String ChannelKey(TextMessageChannels channel, String digits) => String.Concat((int)channel, '|', digits);

        bool IsNotOnChannel(TextMessageChannels channel, String digits)
            => NotOnChannel.TryGetValue(ChannelKey(channel, digits), out var until) && (DateTime.UtcNow < until);

        void SetNotOnChannel(TextMessageChannels channel, String digits)
        {
            if (NotOnChannelTime > TimeSpan.Zero)
                NotOnChannel[ChannelKey(channel, digits)] = DateTime.UtcNow + NotOnChannelTime;
        }

        /// <summary>
        /// Select the sender:
        /// 1. The sender that received the message that this is a reply to (response token).
        /// 2. The requested sender (from).
        /// 3. Only senders of the system (if any sender belongs to the system).
        /// 4. A WhatsApp sender with an open customer service window to the user.
        /// 5. A sender for the country of the recipient, among the senders of the channel of the first remaining sender (the order of the senders is the channel priority).
        /// 6. The first sender.
        /// Only senders with a channel allowed by the message, and not known to fail for the recipient, are considered.
        /// </summary>
        Sender SelectSender(OutgoingTextMessage msg, String digits, String isoCountry, out bool knownNotOnChannel)
        {
            knownNotOnChannel = false;
            var allowed = Senders.Where(x => msg.Allows(x.Channel)).ToList();
            if (allowed.Count <= 0)
                return null;
            var nums = allowed.Where(x => !IsNotOnChannel(x.Channel, digits)).ToList();
            if (nums.Count <= 0)
            {
                knownNotOnChannel = true;
                return null;
            }
            var token = msg.ResponseToken;
            if (!String.IsNullOrEmpty(token))
            {
                var e = token.LastIndexOf('/');
                if (e > 0)
                {
                    var key = token.Substring(0, e);
                    var ts = nums.FirstOrDefault(x => x.Key == key);
                    if (ts != null)
                        return ts;
                }
            }
            var from = msg.From?.Trim();
            if (!String.IsNullOrEmpty(from))
            {
                var fd = GetDigits(StripPrefix(from));
                var fs = nums.FirstOrDefault(x => String.Equals(x.From, from, StringComparison.OrdinalIgnoreCase) || (x.MessagingServiceSid == from) || ((fd.Length > 0) && (x.Digits == fd)));
                if (fs != null)
                    return fs;
            }
            var system = msg.System;
            if (!String.IsNullOrEmpty(system))
            {
                var s = nums.Where(x => system.FastEquals(x.System)).ToList();
                if (s.Count > 0)
                    nums = s;
            }
            foreach (var n in nums)
                if ((n.Channel == TextMessageChannels.WhatsApp) && IsWindowOpen(n, digits))
                    return n;
            //  The order of the senders is the channel priority, the country only selects between senders of the same channel
            var channel = nums[0].Channel;
            nums = nums.Where(x => x.Channel == channel).ToList();
            if (!String.IsNullOrEmpty(isoCountry))
                foreach (var n in nums)
                    if (n.Countries.Contains(isoCountry))
                        return n;
            return nums[0];
        }

        #endregion//Senders

        #region Sending

        /// <summary>
        /// Sent messages waiting for a final status
        /// </summary>
        sealed class Pending
        {
            public Pending(OutgoingTextMessage message, String digits, TextMessageChannels channel, TwilioTextMessage record)
            {
                Message = message;
                Digits = digits;
                Channel = channel;
                Record = record;
            }
            public readonly DateTime Time = DateTime.UtcNow;
            public readonly OutgoingTextMessage Message;
            public readonly String Digits;
            public readonly TextMessageChannels Channel;
            public readonly TwilioTextMessage Record;
        }

        /// <summary>
        /// Key = message sid
        /// </summary>
        readonly ConcurrentDictionary<String, Pending> PendingOut = new(StringComparer.Ordinal);

        static long Counter = DateTime.UtcNow.Ticks - new DateTime(2025, 1, 1).Ticks;

        static String NewId(String prefix) => prefix + Interlocked.Increment(ref Counter);

        static String GetIntent(TextMessagePurpose purpose) => purpose switch
        {
            TextMessagePurpose.Otp => "otp",
            TextMessagePurpose.Marketing => "marketing",
            _ => "notifications",
        };

        Uri GetStatusCallback(OutgoingTextMessage msg)
        {
            var pub = PublicUrl;
            if (pub == null)
                return null;
            var sb = new StringBuilder(pub);
            sb.Append(StatusUrl);
            char sep = '?';
            if (!String.IsNullOrEmpty(msg.Reference))
            {
                sb.Append(sep).Append("r=").Append(Uri.EscapeDataString(msg.Reference));
                sep = '&';
            }
            if (!String.IsNullOrEmpty(msg.System))
                sb.Append(sep).Append("s=").Append(Uri.EscapeDataString(msg.System));
            return new Uri(sb.ToString());
        }

        TextMessageTemplate GetTemplate(OutgoingTextMessage msg, Sender s, String digits)
        {
            if (msg.Template != null)
                return msg.Template;
            if (s.Channel != TextMessageChannels.WhatsApp)
                return null;
            var t = WhatsAppTemplate;
            if (t == null)
                return null;
            if (!t.AlwaysUse && IsWindowOpen(s, digits))
                return null;
            return new TextMessageTemplate { Name = t.ContentSid };
        }

        CreateMessageOptions CreateOptions(OutgoingTextMessage msg, Sender s, String e164, String text, TextMessageTemplate template)
        {
            var o = new CreateMessageOptions(new PhoneNumber(s.ToAddress(e164)))
            {
                StatusCallback = GetStatusCallback(msg),
            };
            if (s.From != null)
                o.From = new PhoneNumber(s.From);
            if (s.MessagingServiceSid != null)
                o.MessagingServiceSid = s.MessagingServiceSid;
            if (template != null)
            {
                o.ContentSid = template.Name;
                var pars = template.Parameters ?? [text];
                var vars = new Dictionary<String, String>(pars.Length);
                for (int i = 0; i < pars.Length; ++i)
                    vars[(i + 1).ToString()] = pars[i];
                o.ContentVariables = JsonSerializer.Serialize(vars);
            }
            else
            {
                o.Body = text;
            }
            if (msg.TimeToLive > 0)
                o.ValidityPeriod = Math.Clamp(msg.TimeToLive, 1, 36000);
            if (SendMessageIntent)
                o.MessageIntent = GetIntent(msg.Purpose);
            return o;
        }

        async Task<MessageResource> Create(CreateMessageOptions o)
        {
            var ret = RetryCount;
            for (int i = 0; ; ++i)
            {
                try
                {
                    return await MessageResource.CreateAsync(o, Client).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    bool transient = ex is ApiException ae ? TwilioErrors.IsTransient(ae.Code, ae.Status) : (ex is ApiConnectionException);
                    if ((i == ret) || !transient)
                    {
                        SendFails.OnException(ex);
                        throw;
                    }
                    RetryFails.OnException(ex);
                }
                await Task.Delay(500 * (i + 1)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Send a text message.
        /// The message is sent from one of the configured senders, see <see cref="SelectSender"/>.
        /// Returns when Twilio have accepted the message, delivery is reported using status updates.
        /// </summary>
        /// <param name="message">The message details</param>
        /// <returns>The result</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        public async Task<TextMessageSendResult> Send(OutgoingTextMessage message)
        {
            using var _ = PerfMon.Track(nameof(Send));
            var text = message?.Text?.Trim();
            if (String.IsNullOrEmpty(text))
                throw new Exception("No text supplied!");
            var phone = message.PhoneNumber?.Trim();
            if (String.IsNullOrEmpty(phone))
                throw new Exception("No phone number supplied!");
            PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var pre, out var num, phone);
            phone = String.Join(' ', pre, num);
            var digits = GetDigits(phone);
            var e164 = "+" + digits;
            Purge();
            var s = SelectSender(message, digits, isoCountry, out var knownNotOnChannel);
            if (s == null)
                return knownNotOnChannel
                    ? message.Failed(message.Channels, TextMessageFailures.NotOnChannel, "The number recently failed as not reachable on the allowed channel(s)")
                    : message.Failed(message.Channels, TextMessageFailures.NotOnChannel, "No sender for the allowed channel(s)");
            var rec = new TwilioTextMessage
            {
                Id = NewId("Out"),
                Time = DateTime.UtcNow,
                PhoneNumber = phone,
                Name = name,
                Country = isoCountry,
                Text = text,
                System = message.System,
                Channel = s.Channel,
                To = s.Display,
                Reference = message.Reference,
                Status = "sending",
            };
            AddRecord(rec);
            var template = GetTemplate(message, s, digits);
            MessageResource res;
            try
            {
                try
                {
                    res = await Create(CreateOptions(message, s, e164, text, template)).ConfigureAwait(false);
                }
                catch (ApiException ex) when ((TwilioErrors.GetFailure(ex.Code) == TextMessageFailures.OutsideWindow) && (template == null) && (s.Channel == TextMessageChannels.WhatsApp) && (WhatsAppTemplate != null))
                {
                    //  The window closed (or we didn't know about it), retry using the template
                    template = new TextMessageTemplate { Name = WhatsAppTemplate.ContentSid };
                    res = await Create(CreateOptions(message, s, e164, text, template)).ConfigureAwait(false);
                }
            }
            catch (ApiException ex) when (TwilioErrors.GetFailure(ex.Code) != TextMessageFailures.Other)
            {
                var failure = TwilioErrors.GetFailure(ex.Code);
                rec.Status = String.Concat("failed (", ex.Code, ") ", ex.Message);
                if (failure == TextMessageFailures.NotOnChannel)
                    SetNotOnChannel(s.Channel, digits);
                var f = message.Failed(s.Channel, failure, ex.Message, ex.Code.ToString());
                f.From = s.Display;
                return f;
            }
            catch (Exception ex)
            {
                rec.Status = "failed: " + ex.Message;
                throw;
            }
            var sid = res.Sid;
            var statusText = res.Status?.ToString();
            var status = TwilioErrors.GetStatus(statusText);
            if (status == TextMessageStatuses.Unknown)
                status = TextMessageStatuses.Accepted;
            rec.Id = sid ?? rec.Id;
            rec.Status = template != null ? statusText + " (template)" : statusText;
            var result = new TextMessageSendResult
            {
                Id = sid,
                Reference = message.Reference,
                Status = status,
                Channel = s.Channel,
                From = res.From?.ToString() ?? s.Display,
            };
            if (status == TextMessageStatuses.Failed)
            {
                var code = res.ErrorCode ?? 0;
                result.Failure = code == 0 ? TextMessageFailures.Other : TwilioErrors.GetFailure(code);
                result.ErrorCode = code == 0 ? null : code.ToString();
                result.Error = res.ErrorMessage;
                return result;
            }
            if (sid != null)
                PendingOut[sid] = new Pending(message, digits, s.Channel, rec);
            return result;
        }

        #endregion//Sending

        #region Receiving

        readonly TextMessageQueue<IncomingTextMessage> Incoming;
        readonly TextMessageQueue<TextMessageStatusUpdate> Statuses;

        public event Action<IncomingTextMessage> OnIncoming;
        public event Action<TextMessageStatusUpdate> OnStatus;

        readonly LinkedList<TwilioTextMessage> Messages = new();

        void AddRecord(TwilioTextMessage rec)
        {
            lock (Messages)
            {
                Messages.AddFirst(rec);
                Messages.Drain(Size);
            }
        }

        void PublishIncoming(IncomingTextMessage m)
        {
            if (!Incoming.Add(m))
                return;
            try
            {
                OnIncoming?.Invoke(m);
            }
            catch (Exception ex)
            {
                Msg?.AddMessage(LogPrefix + "An " + nameof(OnIncoming) + " handler failed", ex, MessageLevels.Warning);
            }
        }

        void PublishStatus(TextMessageStatusUpdate u)
        {
            Statuses.Add(u);
            try
            {
                OnStatus?.Invoke(u);
            }
            catch (Exception ex)
            {
                Msg?.AddMessage(LogPrefix + "An " + nameof(OnStatus) + " handler failed", ex, MessageLevels.Warning);
            }
        }

        /// <summary>
        /// Poll for any new text messages, blocks until new messages are available
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new messages</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        public Task<IncomingTextMessage[]> GetIncoming(GetIncomingTextMessageRequest request)
            => Incoming.Poll(request?.LastId, request?.System);

        /// <summary>
        /// Poll for any new status updates, blocks until new updates are available
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new updates</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        public Task<TextMessageStatusUpdate[]> GetStatusUpdates(GetTextMessageStatusRequest request)
            => Statuses.Poll(request?.LastId, request?.System);

        /// <summary>
        /// A table with all messages (incoming and outgoing)
        /// </summary>
        /// <param name="request">The table request</param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiRequestCache(1)]
        [WebMenuTable(null, "Messages", "Twilio", "The list of Twilio messages")]
        public TableData MessageTable(TableDataRequest request)
            => TableDataTools.Get(request, 2000, Messages, "Twilio messages");

        #endregion//Receiving

        #region Housekeeping

        long LastPurge = DateTime.UtcNow.Ticks;

        static readonly TimeSpan KeepTime = TimeSpan.FromHours(24);

        void Purge()
        {
            var now = DateTime.UtcNow;
            var last = Interlocked.Read(ref LastPurge);
            if ((now.Ticks - last) < TimeSpan.TicksPerMinute)
                return;
            if (Interlocked.CompareExchange(ref LastPurge, now.Ticks, last) != last)
                return;
            var old = now - KeepTime;
            foreach (var x in PendingOut)
                if (x.Value.Time < old)
                    PendingOut.TryRemove(x.Key, out var _);
            foreach (var x in LastIncoming)
                if (x.Value < old)
                    LastIncoming.TryRemove(x.Key, out var _);
            foreach (var x in SeenIncoming)
                if (x.Value < old)
                    SeenIncoming.TryRemove(x.Key, out var _);
            foreach (var x in NotOnChannel)
                if (x.Value < now)
                    NotOnChannel.TryRemove(x.Key, out var _);
        }

        #endregion//Housekeeping

        #region Stats

        readonly ExceptionTracker SendFails = new ExceptionTracker();
        readonly ExceptionTracker RetryFails = new ExceptionTracker();
        readonly ExceptionTracker WebhookFails = new ExceptionTracker();

        public IEnumerable<Stats> GetStats()
        {
            const String n = nameof(TwilioTextMessageService);
            foreach (var x in SendFails.GetStats(n, "Send."))
                yield return x;
            foreach (var x in RetryFails.GetStats(n, "Retry."))
                yield return x;
            foreach (var x in WebhookFails.GetStats(n, "Webhook."))
                yield return x;
        }

        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(nameof(TwilioTextMessageService));

        #endregion//Stats
    }
}
