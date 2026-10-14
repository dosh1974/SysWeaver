using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.PinpointSMSVoiceV2;
using Amazon.PinpointSMSVoiceV2.Model;
using Amazon.Runtime;
using Amazon.SQS;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{

    /// <summary>
    /// Send and receive SMS using AWS End User Messaging SMS (the SMS API formerly known as Amazon Pinpoint SMS).
    /// Incoming messages (two-way SMS) and delivery events are published by AWS to SNS topics, this service reads them from SQS queues subscribed to the topics (no public endpoint required),
    /// they are published using <see cref="ITextMessageEvents"/> and queued for <see cref="GetIncoming"/> / <see cref="GetStatusUpdates"/>.
    /// </summary>
    public sealed partial class AmazonEndUserMessagingTextMessageService : ITextMessageSender, ITextMessageReceiverService, ITextMessageStatusService, ITextMessageEvents, IHaveStats, IPerfMonitored, IDisposable
    {
        const String LogPrefix = "[AwsSms] ";
        const TextMessageChannels Channel = TextMessageChannels.Sms;
        const String ContextReference = "reference";
        const String ContextSystem = "system";

        public AmazonEndUserMessagingTextMessageService(AmazonEndUserMessagingParams p = null, IMessageHost msg = null)
        {
            p = p ?? new AmazonEndUserMessagingParams();
            Msg = msg;
            Size = Math.Max(p.Size, 10);
            var timeOut = Math.Max(p.TimeOut, 0) * 1000;
            var maxPoll = Math.Max(p.MaxPoll, 10);
            Incoming = new(x => x.Id, x => x.System, x => x.Clone(), Size, timeOut, maxPoll);
            Statuses = new(x => x.Id, x => x.System, x => x.Clone(), Size, timeOut, maxPoll);
            CacheTime = TimeSpan.FromHours(Math.Max(0, p.LookupCacheHours));
            UseCarrierLookup = p.CarrierLookup;
            ConfigurationSetName = String.IsNullOrWhiteSpace(p.ConfigurationSetName) ? null : p.ConfigurationSetName.Trim();
            IndiaEntityId = String.IsNullOrWhiteSpace(p.IndiaEntityId) ? null : p.IndiaEntityId.Trim();
            MaxPrice = String.IsNullOrWhiteSpace(p.MaxPrice) ? null : p.MaxPrice.Trim();
            ProtectConfigurationId = String.IsNullOrWhiteSpace(p.ProtectConfigurationId) ? null : p.ProtectConfigurationId.Trim();
            PollRetryDelay = Math.Max(1, p.PollRetryDelay) * 1000;
            Senders = (p.Senders ?? []).Select(x => new Sender(x)).ToArray();
            Tos = p.ToString();
            //  Clients
            var region = RegionEndpoint.GetBySystemName(p.Region ?? "us-east-1");
            var smsConfig = new AmazonPinpointSMSVoiceV2Config
            {
                MaxErrorRetry = Math.Max(1, p.RetryCount) - 1,
            };
            var sqsConfig = new AmazonSQSConfig();
            //  The service url (if set) takes precedence over the region
            if (String.IsNullOrWhiteSpace(p.ServiceUrl))
                smsConfig.RegionEndpoint = region;
            else
                smsConfig.ServiceURL = p.ServiceUrl.Trim();
            if (String.IsNullOrWhiteSpace(p.SqsServiceUrl))
                sqsConfig.RegionEndpoint = region;
            else
                sqsConfig.ServiceURL = p.SqsServiceUrl.Trim();
            if (p.GetUserPassword(out var user, out var password, false))
            {
                var creds = new BasicAWSCredentials(user, password);
                Sms = new AmazonPinpointSMSVoiceV2Client(creds, smsConfig);
                Sqs = new AmazonSQSClient(creds, sqsConfig);
            }
            else
            {
                Sms = new AmazonPinpointSMSVoiceV2Client(smsConfig);
                Sqs = new AmazonSQSClient(sqsConfig);
            }
            //  Receive
            var queues = (p.QueueUrls ?? []).Where(x => !String.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToArray();
            foreach (var q in queues)
                _ = PollQueue(q, Cancel.Token);
            if (queues.Length <= 0)
                Msg?.AddMessage(LogPrefix + "No " + nameof(p.QueueUrls) + " specified, incoming messages and delivery events can't be received", MessageLevels.Warning);
            else if (ConfigurationSetName == null)
                Msg?.AddMessage(LogPrefix + "No " + nameof(p.ConfigurationSetName) + " specified, delivery events are only received if the account has a default configuration set", MessageLevels.Debug);
            Msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Using {Senders.Length} sender(s) in {p.Region ?? "us-east-1"}, polling {queues.Length} queue(s)");
        }

        public override string ToString() => Tos;

        public void Dispose()
        {
            Cancel.Cancel();
            Sms.Dispose();
            Sqs.Dispose();
            Incoming.Dispose();
            Statuses.Dispose();
        }

        readonly IMessageHost Msg;
        readonly String Tos;
        readonly int Size;
        readonly TimeSpan CacheTime;
        readonly bool UseCarrierLookup;
        readonly String ConfigurationSetName;
        readonly String IndiaEntityId;
        readonly String MaxPrice;
        readonly String ProtectConfigurationId;
        readonly int PollRetryDelay;
        readonly IAmazonPinpointSMSVoiceV2 Sms;
        readonly IAmazonSQS Sqs;
        readonly CancellationTokenSource Cancel = new();

        #region Senders

        sealed class Sender
        {
            public Sender(AmazonSenderParams p)
            {
                var id = p?.OriginationIdentity?.Trim();
                if (String.IsNullOrEmpty(id))
                    throw new Exception(nameof(p.OriginationIdentity) + " parameter may not be empty!");
                var countries = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                var phone = p.PhoneNumber?.Trim();
                if (String.IsNullOrEmpty(phone) && id.StartsWith('+'))
                    phone = id;
                Display = id;
                if (!String.IsNullOrEmpty(phone))
                {
                    PhonePrefix.GetValidatedPhoneNumber(out var _, out var iso, out var pre, out var num, phone);
                    Display = String.Join(' ', pre, num);
                    Digits = GetDigits(Display);
                    if (!String.IsNullOrEmpty(iso))
                        countries.Add(iso);
                    //  Phone numbers must be E.164
                    if (id.StartsWith('+'))
                        id = "+" + Digits;
                }
                Identity = id;
                System = String.IsNullOrEmpty(p.System) ? null : p.System;
                foreach (var x in p.Countries ?? [])
                    if (!String.IsNullOrWhiteSpace(x))
                        countries.Add(x.Trim());
                Countries = countries;
            }

            public override string ToString() => Display;

            public readonly String Identity;
            public readonly String Display;
            public readonly String Digits;
            public readonly String System;
            public readonly IReadOnlySet<String> Countries;
        }

        readonly Sender[] Senders;

        static String GetDigits(String s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                if ((c >= '0') && (c <= '9'))
                    sb.Append(c);
            return sb.ToString();
        }

        Sender FindSender(String numberOrIdentity)
        {
            if (String.IsNullOrWhiteSpace(numberOrIdentity))
                return null;
            numberOrIdentity = numberOrIdentity.Trim();
            foreach (var s in Senders)
                if (String.Equals(s.Identity, numberOrIdentity, StringComparison.OrdinalIgnoreCase))
                    return s;
            var digits = GetDigits(numberOrIdentity);
            if (digits.Length > 0)
                foreach (var s in Senders)
                    if (s.Digits == digits)
                        return s;
            return null;
        }

        /// <summary>
        /// Select the sender:
        /// 1. The sender that received the message that this is a reply to (response token).
        /// 2. The requested sender (from).
        /// 3. Only senders of the system (if any sender belongs to the system).
        /// 4. A sender for the country of the recipient.
        /// 5. The first sender.
        /// Returns null if no senders are configured (AWS selects the origination identity).
        /// </summary>
        Sender SelectSender(OutgoingTextMessage msg, String isoCountry)
        {
            IReadOnlyList<Sender> nums = Senders;
            if (nums.Count <= 0)
                return null;
            var token = msg.ResponseToken;
            if (!String.IsNullOrEmpty(token))
            {
                var e = token.LastIndexOf('/');
                var ts = e > 0 ? FindSender(token.Substring(0, e)) : null;
                if (ts != null)
                    return ts;
            }
            var fs = FindSender(msg.From);
            if (fs != null)
                return fs;
            var system = msg.System;
            if (!String.IsNullOrEmpty(system))
            {
                var s = nums.Where(x => system.FastEquals(x.System)).ToList();
                if (s.Count > 0)
                    nums = s;
            }
            if (!String.IsNullOrEmpty(isoCountry))
                foreach (var n in nums)
                    if (n.Countries.Contains(isoCountry))
                        return n;
            return nums[0];
        }

        #endregion//Senders

        #region Sending

        /// <summary>
        /// Key = phone number digits, Value = the failure (NotOnChannel / InvalidNumber, None for valid mobile numbers) and when it expires
        /// </summary>
        readonly ConcurrentDictionary<String, (TextMessageFailures, DateTime)> Lookups = new(StringComparer.Ordinal);

        bool TryGetCached(String digits, out TextMessageFailures failure)
        {
            failure = TextMessageFailures.None;
            if (!Lookups.TryGetValue(digits, out var v) || (DateTime.UtcNow >= v.Item2))
                return false;
            failure = v.Item1;
            return true;
        }

        void SetCached(String digits, TextMessageFailures failure)
        {
            if (CacheTime > TimeSpan.Zero)
                Lookups[digits] = (failure, DateTime.UtcNow + CacheTime);
        }

        /// <summary>
        /// Check the number type using a carrier lookup (cached)
        /// </summary>
        async Task<TextMessageFailures> Lookup(String e164, String digits)
        {
            if (TryGetCached(digits, out var cached))
                return cached;
            if (!UseCarrierLookup)
                return TextMessageFailures.None;
            using var _ = PerfMon.Track(nameof(Lookup));
            TextMessageFailures failure;
            try
            {
                var res = await Sms.CarrierLookupAsync(new CarrierLookupRequest { PhoneNumber = e164 }).ConfigureAwait(false);
                failure = res.PhoneNumberType?.Value switch
                {
                    "LANDLINE" => TextMessageFailures.NotOnChannel,
                    "INVALID" => TextMessageFailures.InvalidNumber,
                    _ => TextMessageFailures.None,
                };
            }
            catch (Exception ex)
            {
                //  Don't fail the message because the lookup failed
                LookupFails.OnException(ex);
                return TextMessageFailures.None;
            }
            SetCached(digits, failure);
            return failure;
        }

        /// <summary>
        /// Sent messages waiting for a final status
        /// </summary>
        sealed class Pending
        {
            public Pending(OutgoingTextMessage message, AmazonTextMessage record)
            {
                Message = message;
                Record = record;
            }
            public readonly DateTime Time = DateTime.UtcNow;
            public readonly OutgoingTextMessage Message;
            public readonly AmazonTextMessage Record;
        }

        /// <summary>
        /// Key = message id
        /// </summary>
        readonly ConcurrentDictionary<String, Pending> PendingOut = new(StringComparer.Ordinal);

        static long Counter = DateTime.UtcNow.Ticks - new DateTime(2025, 1, 1).Ticks;

        static String NewId(String prefix) => prefix + Interlocked.Increment(ref Counter);

        /// <summary>
        /// Send a text message (SMS).
        /// The message is sent from one of the configured senders, see <see cref="SelectSender"/>.
        /// Returns when AWS have accepted the message, delivery is reported using status updates (requires a configuration set with an event destination and a queue).
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
                return message.Failed(Channel, TextMessageFailures.NotOnChannel, "The message doesn't allow the SMS channel");
            PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var pre, out var num, phone);
            phone = String.Join(' ', pre, num);
            var digits = GetDigits(phone);
            var e164 = "+" + digits;
            Purge();
            var lookup = await Lookup(e164, digits).ConfigureAwait(false);
            if (lookup != TextMessageFailures.None)
                return message.Failed(Channel, lookup, lookup == TextMessageFailures.NotOnChannel ? "The number is a landline" : "The number is invalid");
            var s = SelectSender(message, isoCountry);
            var rec = new AmazonTextMessage
            {
                Id = NewId("Out"),
                Time = DateTime.UtcNow,
                PhoneNumber = phone,
                Name = name,
                Country = isoCountry,
                Text = text,
                System = message.System,
                Channel = Channel,
                To = s?.Display,
                Reference = message.Reference,
                Status = "sending",
            };
            AddRecord(rec);
            var req = new SendTextMessageRequest
            {
                DestinationPhoneNumber = e164,
                MessageBody = text,
                MessageType = message.Purpose == TextMessagePurpose.Marketing ? Amazon.PinpointSMSVoiceV2.MessageType.PROMOTIONAL : Amazon.PinpointSMSVoiceV2.MessageType.TRANSACTIONAL,
            };
            if (s != null)
                req.OriginationIdentity = s.Identity;
            if (ConfigurationSetName != null)
                req.ConfigurationSetName = ConfigurationSetName;
            if (message.TimeToLive > 0)
                req.TimeToLive = Math.Clamp(message.TimeToLive, 5, 259200);
            if (MaxPrice != null)
                req.MaxPrice = MaxPrice;
            if (ProtectConfigurationId != null)
                req.ProtectConfigurationId = ProtectConfigurationId;
            //  The context is included in the delivery events
            var context = new Dictionary<String, String>(StringComparer.Ordinal);
            if (!String.IsNullOrEmpty(message.Reference))
                context[ContextReference] = message.Reference;
            if (!String.IsNullOrEmpty(message.System))
                context[ContextSystem] = message.System;
            if (context.Count > 0)
                req.Context = context;
            if (isoCountry == "IN")
            {
                var dcp = new Dictionary<String, String>(StringComparer.Ordinal);
                if (IndiaEntityId != null)
                    dcp["IN_ENTITY_ID"] = IndiaEntityId;
                if (!String.IsNullOrEmpty(message.Template?.Name))
                    dcp["IN_TEMPLATE_ID"] = message.Template.Name;
                if (dcp.Count > 0)
                    req.DestinationCountryParameters = dcp;
            }
            SendTextMessageResponse res;
            try
            {
                res = await Sms.SendTextMessageAsync(req).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SendFails.OnException(ex);
                var failure = AmazonEndUserMessagingErrors.GetFailure(ex, out var code);
                rec.Status = String.Concat("failed (", code ?? ex.GetType().Name, ") ", ex.Message);
                if (failure == TextMessageFailures.Other)
                    throw;
                var f = message.Failed(Channel, failure, ex.Message, code);
                f.From = s?.Display;
                return f;
            }
            var id = res.MessageId;
            rec.Id = id ?? rec.Id;
            rec.Status = "accepted";
            if (id != null)
                PendingOut[id] = new Pending(message, rec);
            return new TextMessageSendResult
            {
                Id = id,
                Reference = message.Reference,
                Status = TextMessageStatuses.Accepted,
                Channel = Channel,
                From = s?.Display,
            };
        }

        #endregion//Sending

        #region Receiving

        readonly TextMessageQueue<IncomingTextMessage> Incoming;
        readonly TextMessageQueue<TextMessageStatusUpdate> Statuses;

        public event Action<IncomingTextMessage> OnIncoming;
        public event Action<TextMessageStatusUpdate> OnStatus;

        readonly LinkedList<AmazonTextMessage> Messages = new();

        void AddRecord(AmazonTextMessage rec)
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
        [WebMenuTable(null, "Messages", "AWS SMS", "The list of AWS SMS messages")]
        public TableData MessageTable(TableDataRequest request)
            => TableDataTools.Get(request, 2000, Messages, "AWS SMS messages");

        #endregion//Receiving

        #region Housekeeping

        long LastPurge = DateTime.UtcNow.Ticks;

        /// <summary>
        /// Delivery events can take up to 72 hours
        /// </summary>
        static readonly TimeSpan KeepTime = TimeSpan.FromHours(72);

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
            foreach (var x in Seen)
                if (x.Value < old)
                    Seen.TryRemove(x.Key, out var _);
            foreach (var x in Lookups)
                if (x.Value.Item2 < now)
                    Lookups.TryRemove(x.Key, out var _);
        }

        #endregion//Housekeeping

        #region Stats

        readonly ExceptionTracker SendFails = new ExceptionTracker();
        readonly ExceptionTracker LookupFails = new ExceptionTracker();
        readonly ExceptionTracker PollFails = new ExceptionTracker();
        readonly ExceptionTracker ProcessFails = new ExceptionTracker();

        public IEnumerable<Stats> GetStats()
        {
            const String n = nameof(AmazonEndUserMessagingTextMessageService);
            foreach (var x in SendFails.GetStats(n, "Send."))
                yield return x;
            foreach (var x in LookupFails.GetStats(n, "Lookup."))
                yield return x;
            foreach (var x in PollFails.GetStats(n, "Poll."))
                yield return x;
            foreach (var x in ProcessFails.GetStats(n, "Process."))
                yield return x;
        }

        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(nameof(AmazonEndUserMessagingTextMessageService));

        #endregion//Stats
    }
}
