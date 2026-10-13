using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;
using SysWeaver.Remote;
using SysWeaver.TextMessage.WhatsAppApi;

namespace SysWeaver.TextMessage
{

    /// <summary>
    /// Send and receive text messages using the WhatsApp Cloud API.
    /// Incoming messages (and delivery statuses) are pushed by Meta to a webhook served by this service (see <see cref="WhatsAppTextMessageParams.WebhookUrl"/>),
    /// they are published using <see cref="ITextMessageEvents"/> and queued for <see cref="GetIncoming"/> / <see cref="GetStatusUpdates"/>.
    /// </summary>
    public sealed partial class WhatsAppTextMessageService : ITextMessageSender, ITextMessageReceiverService, ITextMessageStatusService, ITextMessageEvents, IHttpServerModule, IHaveStats, IPerfMonitored, IDisposable
    {
        const String LogPrefix = "[WhatsApp] ";
        const TextMessageChannels Channel = TextMessageChannels.WhatsApp;

        public WhatsAppTextMessageService(WhatsAppTextMessageParams p = null, IMessageHost msg = null)
        {
            p = p ?? new WhatsAppTextMessageParams();
            Msg = msg;
            Size = Math.Max(p.Size, 10);
            var timeOut = Math.Max(p.TimeOut, 0) * 1000;
            var maxPoll = Math.Max(p.MaxPoll, 10);
            Incoming = new(x => x.Id, x => x.System, x => x.Clone(), Size, timeOut, maxPoll);
            Statuses = new(x => x.Id, x => x.System, x => x.Clone(), Size, timeOut, maxPoll);
            RetryCount = Math.Max(1, p.RetryCount) - 1;
            MarkAsRead = p.MarkAsRead;
            NotOnWhatsAppTime = TimeSpan.FromHours(Math.Max(0, p.NotOnWhatsAppHours));
            var t = p.Template;
            if (!String.IsNullOrWhiteSpace(t?.Name))
                Template = t;
            Tos = p.ToString();
            //  Numbers
            var nums = p.Numbers;
            if ((nums == null) || (nums.Length <= 0))
                throw new Exception(nameof(p.Numbers) + " parameter must contain at least one number!");
            var defToken = p.GetApiKey(false);
            var graphUrl = String.Concat((p.GraphUrl ?? "https://graph.facebook.com/").TrimEnd('/'), '/', (p.ApiVersion ?? "v24.0").Trim('/'), '/');
            var numbers = new List<Number>(nums.Length);
            var byId = new Dictionary<String, Number>(StringComparer.Ordinal);
            foreach (var x in nums)
            {
                var n = new Number(x, defToken, graphUrl);
                if (!byId.TryAdd(n.Id, n))
                {
                    n.Dispose();
                    throw new Exception("The phone number id " + n.Id.ToQuoted() + " is specified more than once!");
                }
                n.Api.OnCallEnd += OnApiCallEnd;
                numbers.Add(n);
            }
            Numbers = numbers.ToArray();
            NumbersById = byId;
            //  Webhook
            var url = (p.WebhookUrl ?? "").Trim().Trim('/');
            if (url.Length <= 0)
                throw new Exception(nameof(p.WebhookUrl) + " parameter may not be empty!");
            WebhookUrl = url;
            OnlyForPrefixes = [url];
            VerifyToken = String.IsNullOrEmpty(p.VerifyToken) ? null : p.VerifyToken;
            var secret = p.AppSecret?.GetApiKey(false);
            AppSecret = String.IsNullOrEmpty(secret) ? null : Encoding.UTF8.GetBytes(secret);
            VerifyHandler = new WebhookRequestHandler("Verify", Verify);
            PostHandler = new WebhookRequestHandler("Notify", Notify);
            if ((VerifyToken == null) || (AppSecret == null))
                Msg?.AddMessage(LogPrefix + "No " + nameof(p.VerifyToken) + " and/or " + nameof(p.AppSecret) + " specified, incoming messages can't be received", MessageLevels.Warning);
            Msg?.AddMessage(LogPrefix + "Using " + Numbers.Length + " number(s), webhook at " + url.ToQuoted(), MessageLevels.Debug);
        }

        public override string ToString() => Tos;

        public void Dispose()
        {
            foreach (var x in Numbers)
            {
                x.Api.OnCallEnd -= OnApiCallEnd;
                x.Dispose();
            }
            Incoming.Dispose();
            Statuses.Dispose();
        }

        readonly IMessageHost Msg;
        readonly String Tos;
        readonly int Size;
        readonly int RetryCount;
        readonly bool MarkAsRead;
        readonly TimeSpan NotOnWhatsAppTime;
        readonly WhatsAppTemplateParams Template;

        #region Numbers

        sealed class Number : IDisposable
        {
            public Number(WhatsAppNumberParams p, String defToken, String graphUrl)
            {
                var id = p?.PhoneNumberId?.Trim();
                if (String.IsNullOrEmpty(id))
                    throw new Exception(nameof(p.PhoneNumberId) + " parameter may not be empty!");
                Id = id;
                System = String.IsNullOrEmpty(p.System) ? null : p.System;
                var countries = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                Display = id;
                var phone = p.PhoneNumber?.Trim();
                if (!String.IsNullOrEmpty(phone))
                {
                    PhonePrefix.GetValidatedPhoneNumber(out var _, out var iso, out var pre, out var num, phone);
                    Display = String.Join(' ', pre, num);
                    Digits = GetDigits(Display);
                    if (!String.IsNullOrEmpty(iso))
                        countries.Add(iso);
                }
                foreach (var x in p.Countries ?? [])
                    if (!String.IsNullOrWhiteSpace(x))
                        countries.Add(x.Trim());
                Countries = countries;
                var token = p.Token?.GetApiKey() ?? defToken;
                if (String.IsNullOrEmpty(token))
                    throw new Exception("No access token specified for the phone number id " + id.ToQuoted() + "!");
                Api = new RemoteConnection
                {
                    BaseUrl = String.Concat(graphUrl, id, '/'),
                    BearerToken = token,
                }.Create<IWhatsAppCloudApi>();
            }

            public override string ToString() => String.Concat(Display, " [", Id, ']');

            public void Dispose() => Api.Dispose();

            public readonly String Id;
            public readonly String Display;
            public readonly String Digits;
            public readonly String System;
            public readonly IReadOnlySet<String> Countries;
            public readonly IWhatsAppCloudApi Api;
        }

        readonly Number[] Numbers;
        readonly IReadOnlyDictionary<String, Number> NumbersById;

        /// <summary>
        /// Key = phone number id + '|' + user wa_id, Value = time of the last incoming message from the user to the number.
        /// Used to track the customer service window (free form messages are only allowed within 24 hours of the last incoming message).
        /// </summary>
        readonly ConcurrentDictionary<String, DateTime> LastIncoming = new(StringComparer.Ordinal);

        static String WindowKey(String numberId, String digits) => String.Concat(numberId, '|', digits);

        /// <summary>
        /// Some margin, since the time of the last message is the time it was received
        /// </summary>
        static readonly TimeSpan WindowLength = TimeSpan.FromHours(24) - TimeSpan.FromMinutes(5);

        bool IsWindowOpen(Number n, String digits)
            => LastIncoming.TryGetValue(WindowKey(n.Id, digits), out var t) && ((DateTime.UtcNow - t) < WindowLength);

        /// <summary>
        /// Find a number by phone number id or phone number
        /// </summary>
        Number FindNumber(String idOrPhone)
        {
            if (String.IsNullOrWhiteSpace(idOrPhone))
                return null;
            idOrPhone = idOrPhone.Trim();
            if (NumbersById.TryGetValue(idOrPhone, out var n))
                return n;
            var digits = GetDigits(idOrPhone);
            return digits.Length > 0 ? Numbers.FirstOrDefault(x => x.Digits == digits) : null;
        }

        /// <summary>
        /// Select the number to send from:
        /// 1. The number that received the message that this is a reply to (response token).
        /// 2. The requested sender (from).
        /// 3. Only numbers of the system (if any number belongs to the system).
        /// 4. The number with an open customer service window to the user (that the user wrote to most recently).
        /// 5. A number for the country of the recipient.
        /// 6. The first number.
        /// </summary>
        Number SelectNumber(OutgoingTextMessage msg, String digits, String isoCountry)
        {
            var token = msg.ResponseToken;
            if (!String.IsNullOrEmpty(token))
            {
                var e = token.IndexOf('/');
                if ((e > 0) && NumbersById.TryGetValue(token.Substring(0, e), out var tn))
                    return tn;
            }
            var fn = FindNumber(msg.From);
            if (fn != null)
                return fn;
            IReadOnlyList<Number> nums = Numbers;
            if (nums.Count == 1)
                return nums[0];
            var system = msg.System;
            if (!String.IsNullOrEmpty(system))
            {
                var s = nums.Where(x => system.FastEquals(x.System)).ToList();
                if (s.Count > 0)
                    nums = s;
            }
            Number best = null;
            DateTime bestTime = DateTime.UtcNow - WindowLength;
            foreach (var n in nums)
            {
                if (LastIncoming.TryGetValue(WindowKey(n.Id, digits), out var t) && (t > bestTime))
                {
                    best = n;
                    bestTime = t;
                }
            }
            if (best != null)
                return best;
            if (!String.IsNullOrEmpty(isoCountry))
                foreach (var n in nums)
                    if (n.Countries.Contains(isoCountry))
                        return n;
            return nums[0];
        }

        #endregion//Numbers

        #region Api calls

        /// <summary>
        /// The remote connection only use text responses as the exception message, the Graph API returns json errors.
        /// The error is captured in the OnCallEnd event, which is invoked in the async context of the caller (so the capture instance is visible).
        /// </summary>
        sealed class ErrorCapture
        {
            public WaError Error;
        }

        static readonly AsyncLocal<ErrorCapture> Capture = new();

        static void OnApiCallEnd(long id, Exception ex, int statusCode, String ser, ref Memory<Byte> data)
        {
            if ((ex == null) || data.IsEmpty)
                return;
            var c = Capture.Value;
            if (c == null)
                return;
            try
            {
                c.Error = System.Text.Json.JsonSerializer.Deserialize<WaErrorResponse>(data.Span, JsonOptions)?.error;
            }
            catch
            {
            }
        }

        static async Task<T> Call<T>(Func<Task<T>> fn)
        {
            var c = new ErrorCapture();
            Capture.Value = c;
            try
            {
                return await fn().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var e = c.Error;
                if (e == null)
                    throw;
                throw new WhatsAppApiException(e, (ex as HttpResponseException)?.ResponseCode ?? 0, ex);
            }
        }

        /// <summary>
        /// Graph API error codes that are temporary
        /// </summary>
        static readonly IReadOnlySet<int> TransientCodes = new HashSet<int>
        {
            1,          //  API Unknown
            2,          //  API Service
            4,          //  API Too Many Calls
            80007,      //  Rate limit issues
            130429,     //  Rate limit hit
            131000,     //  Something went wrong
            131016,     //  Service unavailable
        };

        static bool CanRetry(Exception ex)
        {
            if (ex is WhatsAppApiException we)
                return TransientCodes.Contains(we.Code) || (we.StatusCode >= 500);
            if (ex is HttpResponseException he)
                return (he.ResponseCode >= 500) || (he.ResponseCode == 429) || (he.ResponseCode == 408);
            return true;
        }

        async Task<WaSendResponse> SendWhatsApp(Number n, String digits, String text, TextMessageTemplate template)
        {
            var req = new WaSendRequest
            {
                to = digits,
            };
            if (template != null)
            {
                req.type = "template";
                req.template = new WaTemplate
                {
                    name = template.Name,
                    language = new WaLanguage { code = template.Language ?? "en" },
                    components =
                    [
                        new WaTemplateComponent
                        {
                            type = "body",
                            parameters = (template.Parameters ?? [text]).Select(x => new WaTemplateParameter { type = "text", text = x }).ToArray(),
                        },
                    ],
                };
            }
            else
            {
                req.type = "text";
                req.text = new WaText { body = text };
            }
            var api = n.Api;
            var ret = RetryCount;
            for (int i = 0; ; ++i)
            {
                try
                {
                    return await Call(() => api.Send(req)).ConfigureAwait(false);
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
                await Task.Delay(500 * (i + 1)).ConfigureAwait(false);
            }
        }

        async Task MarkRead(Number n, String messageId)
        {
            try
            {
                await Call(() => n.Api.MarkRead(new WaReadRequest { message_id = messageId })).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MarkReadFails.OnException(ex);
            }
        }

        #endregion//Api calls

        #region Sending

        /// <summary>
        /// Sent messages waiting for a final status
        /// </summary>
        sealed class Pending
        {
            public Pending(OutgoingTextMessage message, String digits, WhatsAppTextMessage record)
            {
                Message = message;
                Digits = digits;
                Record = record;
            }
            public readonly DateTime Time = DateTime.UtcNow;
            public readonly OutgoingTextMessage Message;
            public readonly String Digits;
            public readonly WhatsAppTextMessage Record;
        }

        /// <summary>
        /// Key = message id (wamid)
        /// </summary>
        readonly ConcurrentDictionary<String, Pending> PendingOut = new(StringComparer.Ordinal);

        /// <summary>
        /// Key = wa_id (phone number digits), Value = time when the number should be tried using WhatsApp again
        /// </summary>
        readonly ConcurrentDictionary<String, DateTime> NotOnWhatsApp = new(StringComparer.Ordinal);

        static long Counter = DateTime.UtcNow.Ticks - new DateTime(2025, 1, 1).Ticks;

        static String NewId(String prefix) => prefix + Interlocked.Increment(ref Counter);

        static String GetDigits(String s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                if ((c >= '0') && (c <= '9'))
                    sb.Append(c);
            return sb.ToString();
        }

        bool IsKnownNotOnWhatsApp(String digits)
            => NotOnWhatsApp.TryGetValue(digits, out var until) && (DateTime.UtcNow < until);

        void SetNotOnWhatsApp(String digits)
        {
            if (NotOnWhatsAppTime > TimeSpan.Zero)
                NotOnWhatsApp[digits] = DateTime.UtcNow + NotOnWhatsAppTime;
        }

        TextMessageTemplate GetDefaultTemplate()
        {
            var t = Template;
            return t == null ? null : new TextMessageTemplate { Name = t.Name, Language = t.Language };
        }

        /// <summary>
        /// Send a text message.
        /// The message is sent from one of the configured numbers, see <see cref="SelectNumber"/>.
        /// Returns when WhatsApp have accepted the message, delivery is reported using status updates.
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
            if (!message.Allows(Channel))
                return message.Failed(Channel, TextMessageFailures.NotOnChannel, "The message doesn't allow the WhatsApp channel");
            PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var pre, out var num, phone);
            phone = String.Join(' ', pre, num);
            var digits = GetDigits(phone);
            Purge();
            if (IsKnownNotOnWhatsApp(digits))
                return message.Failed(Channel, TextMessageFailures.NotOnChannel, "The number recently failed as undeliverable", WhatsAppApiException.Undeliverable.ToString());
            var n = SelectNumber(message, digits, isoCountry);
            var rec = new WhatsAppTextMessage
            {
                Id = NewId("Out"),
                Time = DateTime.UtcNow,
                PhoneNumber = phone,
                Name = name,
                Country = isoCountry,
                Text = text,
                System = message.System,
                Channel = Channel,
                To = n.Display,
                Reference = message.Reference,
                Status = "sending",
            };
            AddRecord(rec);
            //  An explicit template is always used, else use the configured template when the window isn't known to be open
            var defTemplate = GetDefaultTemplate();
            var template = message.Template ?? (((Template?.AlwaysUse ?? false) || !IsWindowOpen(n, digits)) ? defTemplate : null);
            WaSendResponse res;
            try
            {
                try
                {
                    res = await SendWhatsApp(n, digits, text, template).ConfigureAwait(false);
                }
                catch (WhatsAppApiException ex) when ((ex.Code == WhatsAppApiException.ReEngagement) && (template == null) && (defTemplate != null))
                {
                    //  The window closed (or we didn't know about it), retry using the template
                    template = defTemplate;
                    res = await SendWhatsApp(n, digits, text, template).ConfigureAwait(false);
                }
            }
            catch (WhatsAppApiException ex) when (ex.Failure != TextMessageFailures.Other)
            {
                rec.Status = "failed " + ex.Error;
                if (ex.Code == WhatsAppApiException.Undeliverable)
                    SetNotOnWhatsApp(digits);
                var f = message.Failed(Channel, ex.Failure, ex.Error.ToString(), ex.Code.ToString());
                f.From = n.Display;
                return f;
            }
            catch (Exception ex)
            {
                rec.Status = "failed: " + ex.Message;
                throw;
            }
            rec.Status = template != null ? "accepted (template)" : "accepted";
            var id = res?.messages?.FirstOrDefault()?.id;
            if (id != null)
            {
                rec.Id = id;
                PendingOut[id] = new Pending(message, digits, rec);
            }
            return new TextMessageSendResult
            {
                Id = id,
                Reference = message.Reference,
                Status = TextMessageStatuses.Accepted,
                Channel = Channel,
                From = n.Display,
            };
        }

        #endregion//Sending

        #region Receiving

        readonly TextMessageQueue<IncomingTextMessage> Incoming;
        readonly TextMessageQueue<TextMessageStatusUpdate> Statuses;

        public event Action<IncomingTextMessage> OnIncoming;
        public event Action<TextMessageStatusUpdate> OnStatus;

        readonly LinkedList<WhatsAppTextMessage> Messages = new();

        void AddRecord(WhatsAppTextMessage rec)
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
        [WebMenuTable(null, "Messages", "WhatsApp", "The list of WhatsApp messages")]
        public TableData MessageTable(TableDataRequest request)
            => TableDataTools.Get(request, 2000, Messages, "WhatsApp messages");

        #endregion//Receiving

        #region Housekeeping

        long LastPurge = DateTime.UtcNow.Ticks;

        static readonly TimeSpan KeepTime = TimeSpan.FromHours(24);

        /// <summary>
        /// Remove old entries from the lookups (at most once per minute)
        /// </summary>
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
            foreach (var x in NotOnWhatsApp)
                if (x.Value < now)
                    NotOnWhatsApp.TryRemove(x.Key, out var _);
        }

        #endregion//Housekeeping

        #region Stats

        readonly ExceptionTracker SendFails = new ExceptionTracker();
        readonly ExceptionTracker RetryFails = new ExceptionTracker();
        readonly ExceptionTracker WebhookFails = new ExceptionTracker();
        readonly ExceptionTracker MarkReadFails = new ExceptionTracker();

        public IEnumerable<Stats> GetStats()
        {
            const String n = nameof(WhatsAppTextMessageService);
            foreach (var x in SendFails.GetStats(n, "Send."))
                yield return x;
            foreach (var x in RetryFails.GetStats(n, "Retry."))
                yield return x;
            foreach (var x in WebhookFails.GetStats(n, "Webhook."))
                yield return x;
            foreach (var x in MarkReadFails.GetStats(n, "MarkRead."))
                yield return x;
        }

        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(nameof(WhatsAppTextMessageService));

        #endregion//Stats

    }

}
