using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{
    /// <summary>
    /// Combines multiple text message providers (ex: WhatsApp and one or more SMS providers):
    /// - Sends using the first matching route (channel, country, system), and falls back to the next route if a provider fails (synchronously or asynchronously using status updates).
    /// - Receives messages and status updates from all providers (events for in-process providers, long polling for remote providers).
    /// - Replies (using the response token) are sent using the provider that received the message.
    /// </summary>
    public sealed class TextMessageRouterService : ITextMessageSender, ITextMessageReceiverService, ITextMessageStatusService, ITextMessageEvents, IHaveStats, IPerfMonitored, IDisposable
    {
        const String LogPrefix = "[TextRouter] ";
        const char TokenSep = '|';

        public TextMessageRouterService(ServiceManager sm, TextMessageRouterParams p = null, IMessageHost msg = null)
        {
            p = p ?? new TextMessageRouterParams();
            Manager = sm ?? throw new ArgumentNullException(nameof(sm));
            Msg = msg;
            var routes = p.Routes;
            if ((routes == null) || (routes.Length <= 0))
                throw new Exception(nameof(p.Routes) + " parameter must contain at least one route!");
            var size = Math.Max(p.Size, 10);
            var timeOut = Math.Max(p.TimeOut, 0) * 1000;
            var maxPoll = Math.Max(p.MaxPoll, 10);
            Incoming = new(x => x.Id, x => x.System, x => x.Clone(), size, timeOut, maxPoll);
            Statuses = new(x => x.Id, x => x.System, x => x.Clone(), size, timeOut, maxPoll);
            FallbackOn = new HashSet<TextMessageFailures>(p.FallbackOn ?? []);
            FallbackOnError = p.FallbackOnError;
            NotOnChannelTime = TimeSpan.FromHours(Math.Max(0, p.NotOnChannelHours));
            PollRetryDelay = Math.Max(1, p.PollRetryDelay) * 1000;
            var names = new HashSet<String>(StringComparer.Ordinal);
            Routes = routes.Select(x =>
            {
                var r = new Route(x);
                if (!names.Add(r.Name))
                    throw new Exception("The route name " + r.Name.ToQuoted() + " is used more than once!");
                return r;
            }).ToArray();
            RoutesByName = Routes.ToDictionary(x => x.Name, StringComparer.Ordinal);
            var receiving = new HashSet<String>(StringComparer.Ordinal);
            foreach (var r in Routes)
                r.ReceiveFromInstance = r.CanReceive && receiving.Add(r.Instance);
            sm.OnServiceAdded += Sm_OnServiceAdded;
            sm.OnServiceRemoved += Sm_OnServiceRemoved;
            foreach (var r in Routes)
                Connect(r);
        }

        public override string ToString() => String.Join(", ", Routes.Select(x => x.ToString()));

        public void Dispose()
        {
            Manager.OnServiceAdded -= Sm_OnServiceAdded;
            Manager.OnServiceRemoved -= Sm_OnServiceRemoved;
            foreach (var r in Routes)
                Disconnect(r);
            Incoming.Dispose();
            Statuses.Dispose();
        }

        readonly ServiceManager Manager;
        readonly IMessageHost Msg;
        readonly IReadOnlySet<TextMessageFailures> FallbackOn;
        readonly bool FallbackOnError;
        readonly TimeSpan NotOnChannelTime;
        readonly int PollRetryDelay;

        #region Routes

        sealed class Route
        {
            public Route(TextMessageRouteParams p)
            {
                var instance = p?.Instance?.Trim();
                if (String.IsNullOrEmpty(instance))
                    throw new Exception(nameof(p.Instance) + " parameter may not be empty!");
                Instance = instance;
                var name = String.IsNullOrWhiteSpace(p.Name) ? instance : p.Name.Trim();
                if (name.IndexOf(TokenSep) >= 0)
                    throw new Exception("A route name may not contain " + TokenSep.ToString().ToQuoted() + "!");
                Name = name;
                Channels = p.Channels;
                Countries = ToSet(p.Countries);
                ExcludeCountries = ToSet(p.ExcludeCountries);
                Systems = p.Systems?.Length > 0 ? new HashSet<String>(p.Systems, StringComparer.Ordinal) : null;
                CanSend = p.Send;
                CanReceive = p.Receive;
            }

            static IReadOnlySet<String> ToSet(String[] v)
                => v?.Length > 0 ? new HashSet<String>(v.Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase) : null;

            public override string ToString() => String.Concat(Name, " (", Channels, Sender == null ? ", not found)" : ")");

            public readonly String Name;
            public readonly String Instance;
            public readonly TextMessageChannels Channels;
            public readonly IReadOnlySet<String> Countries;
            public readonly IReadOnlySet<String> ExcludeCountries;
            public readonly IReadOnlySet<String> Systems;
            public readonly bool CanSend;
            public readonly bool CanReceive;

            /// <summary>
            /// True for the first route (that can receive) of each instance, so that a provider used by multiple routes (ex: Twilio for WhatsApp and SMS) is only received from once
            /// </summary>
            public bool ReceiveFromInstance;

            public bool Matches(OutgoingTextMessage msg, String isoCountry)
            {
                if (!CanSend)
                    return false;
                if ((Channels != TextMessageChannels.Any) && !msg.Allows(Channels))
                    return false;
                if ((Countries != null) && !((isoCountry != null) && Countries.Contains(isoCountry)))
                    return false;
                if ((ExcludeCountries != null) && (isoCountry != null) && ExcludeCountries.Contains(isoCountry))
                    return false;
                if ((Systems != null) && !((msg.System != null) && Systems.Contains(msg.System)))
                    return false;
                return true;
            }

            public volatile ITextMessageSender Sender;
            public ITextMessageEvents Events;
            public Action<IncomingTextMessage> IncomingHandler;
            public Action<TextMessageStatusUpdate> StatusHandler;
            public CancellationTokenSource PollCancel;
            public String IncomingMode = "none";
            public String StatusMode = "none";
        }

        readonly Route[] Routes;
        readonly IReadOnlyDictionary<String, Route> RoutesByName;

        void Sm_OnServiceAdded(Object instance, ServiceInfo info)
        {
            if (ReferenceEquals(instance, this))
                return;
            foreach (var r in Routes)
                if (r.Instance == info.Name)
                    Connect(r);
        }

        void Sm_OnServiceRemoved(Object instance, ServiceInfo info)
        {
            foreach (var r in Routes)
                if (r.Instance == info.Name)
                    Disconnect(r);
        }

        /// <summary>
        /// Find the provider of a route and start receiving from it
        /// </summary>
        void Connect(Route r)
        {
            lock (r)
            {
                if (r.Sender != null)
                    return;
                var sender = Manager.TryGet<ITextMessageSender>(r.Instance);
                if ((sender == null) || ReferenceEquals(sender, this))
                    return;
                r.Sender = sender;
                if (!r.ReceiveFromInstance)
                    return;
                if (sender is ITextMessageEvents ev)
                {
                    //  In-process
                    r.Events = ev;
                    r.IncomingHandler = m => OnRouteIncoming(r, m);
                    r.StatusHandler = u => OnRouteStatus(r, u);
                    ev.OnIncoming += r.IncomingHandler;
                    ev.OnStatus += r.StatusHandler;
                    r.IncomingMode = "events";
                    r.StatusMode = "events";
                    return;
                }
                //  Remote (or a provider without events), poll
                var cancel = new CancellationTokenSource();
                r.PollCancel = cancel;
                var receiver = Manager.TryGet<ITextMessageReceiverService>(r.Instance);
                if (receiver != null)
                {
                    r.IncomingMode = "poll";
                    _ = PollIncoming(r, receiver, cancel.Token);
                }
                var statuses = Manager.TryGet<ITextMessageStatusService>(r.Instance);
                if (statuses != null)
                {
                    r.StatusMode = "poll";
                    _ = PollStatuses(r, statuses, cancel.Token);
                }
            }
            Msg?.AddMessage(LogPrefix + "Connected to " + r.Name.ToQuoted() + ", incoming: " + r.IncomingMode + ", statuses: " + r.StatusMode, MessageLevels.Debug);
        }

        void Disconnect(Route r)
        {
            lock (r)
            {
                r.Sender = null;
                var ev = r.Events;
                if (ev != null)
                {
                    ev.OnIncoming -= r.IncomingHandler;
                    ev.OnStatus -= r.StatusHandler;
                }
                r.Events = null;
                r.IncomingHandler = null;
                r.StatusHandler = null;
                r.PollCancel?.Cancel();
                r.PollCancel = null;
                r.IncomingMode = "none";
                r.StatusMode = "none";
            }
        }

        async Task PollIncoming(Route r, ITextMessageReceiverService receiver, CancellationToken cancel)
        {
            String lastId = null;
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var res = await receiver.GetIncoming(new GetIncomingTextMessageRequest { LastId = lastId }).ConfigureAwait(false);
                    if (res == null)
                        continue;
                    foreach (var m in res)
                    {
                        lastId = m.Id;
                        OnRouteIncoming(r, m);
                    }
                }
                catch (Exception ex)
                {
                    if (cancel.IsCancellationRequested)
                        break;
                    PollFails.OnException(ex);
                    await Task.Delay(PollRetryDelay, cancel).ContinueWith(_ => { }).ConfigureAwait(false);
                }
            }
        }

        async Task PollStatuses(Route r, ITextMessageStatusService service, CancellationToken cancel)
        {
            String lastId = null;
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var res = await service.GetStatusUpdates(new GetTextMessageStatusRequest { LastId = lastId }).ConfigureAwait(false);
                    if (res == null)
                        continue;
                    foreach (var u in res)
                    {
                        lastId = u.Id;
                        OnRouteStatus(r, u);
                    }
                }
                catch (Exception ex)
                {
                    if (cancel.IsCancellationRequested)
                        break;
                    PollFails.OnException(ex);
                    await Task.Delay(PollRetryDelay, cancel).ContinueWith(_ => { }).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Get information about the routes
        /// </summary>
        /// <returns>The routes</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        public TextMessageRouteInfo[] GetRoutes()
            => Routes.Select(x => new TextMessageRouteInfo
            {
                Name = x.Name,
                Instance = x.Instance,
                Channels = x.Channels,
                Found = x.Sender != null,
                Incoming = x.IncomingMode,
                Statuses = x.StatusMode,
            }).ToArray();

        #endregion//Routes

        #region Sending

        /// <summary>
        /// A sent message that may fall back to another route
        /// </summary>
        sealed class Pending
        {
            public Pending(OutgoingTextMessage message, String digits, String isoCountry, Route[] candidates, int index)
            {
                Message = message;
                Digits = digits;
                IsoCountry = isoCountry;
                Candidates = candidates;
                Index = index;
            }
            public readonly DateTime Time = DateTime.UtcNow;
            public readonly OutgoingTextMessage Message;
            public readonly String Digits;
            public readonly String IsoCountry;
            public readonly Route[] Candidates;
            public readonly int Index;
        }

        /// <summary>
        /// Key = route name + '|' + provider message id
        /// </summary>
        readonly ConcurrentDictionary<String, Pending> PendingOut = new(StringComparer.Ordinal);

        /// <summary>
        /// Key = route name + '|' + phone number digits, Value = time when the route should be used for the number again
        /// </summary>
        readonly ConcurrentDictionary<String, DateTime> NotOnChannel = new(StringComparer.Ordinal);

        static String Key(Route r, String id) => String.Concat(r.Name, TokenSep, id);

        /// <summary>
        /// Status updates are received from the first route of an instance, so pending messages are keyed by instance
        /// </summary>
        static String PendingKey(Route r, String id) => String.Concat(r.Instance, TokenSep, id);

        static String GetDigits(String s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                if ((c >= '0') && (c <= '9'))
                    sb.Append(c);
            return sb.ToString();
        }

        bool IsNotOnChannel(Route r, String digits)
            => NotOnChannel.TryGetValue(Key(r, digits), out var until) && (DateTime.UtcNow < until);

        void SetNotOnChannel(Route r, String digits)
        {
            if (NotOnChannelTime > TimeSpan.Zero)
                NotOnChannel[Key(r, digits)] = DateTime.UtcNow + NotOnChannelTime;
        }

        /// <summary>
        /// Send a text message using the first matching route, falling back to the next route on failure.
        /// </summary>
        /// <param name="message">The message details</param>
        /// <returns>The result (of the last route tried)</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        public async Task<TextMessageSendResult> Send(OutgoingTextMessage message)
        {
            using var _ = PerfMon.Track(nameof(Send));
            var phone = message?.PhoneNumber?.Trim();
            if (String.IsNullOrEmpty(phone))
                throw new Exception("No phone number supplied!");
            if (String.IsNullOrWhiteSpace(message.Text))
                throw new Exception("No text supplied!");
            PhonePrefix.GetValidatedPhoneNumber(out var _, out var isoCountry, out var pre, out var num, phone);
            var digits = GetDigits(String.Concat(pre, num));
            Purge();
            //  A reply is sent using the provider that received the message (if possible), else using the normal routes
            List<Route> candidates = new(Routes.Length + 1);
            Route tokenRoute = null;
            var token = message.ResponseToken;
            if (!String.IsNullOrEmpty(token))
            {
                var e = token.IndexOf(TokenSep);
                if ((e > 0) && RoutesByName.TryGetValue(token.Substring(0, e), out tokenRoute) && tokenRoute.CanSend)
                    candidates.Add(tokenRoute);
            }
            foreach (var r in Routes)
                if ((r != tokenRoute) && r.Matches(message, isoCountry))
                    candidates.Add(r);
            if (candidates.Count <= 0)
                return message.Failed(TextMessageChannels.Any, TextMessageFailures.NotOnChannel, "No route matches the message");
            return await SendUsing(message, digits, isoCountry, candidates.ToArray(), 0, tokenRoute).ConfigureAwait(false);
        }

        /// <summary>
        /// Send using the candidates starting at an index
        /// </summary>
        async Task<TextMessageSendResult> SendUsing(OutgoingTextMessage message, String digits, String isoCountry, Route[] candidates, int start, Route tokenRoute = null)
        {
            TextMessageSendResult last = null;
            Exception lastEx = null;
            var count = candidates.Length;
            for (int i = start; i < count; ++i)
            {
                var r = candidates[i];
                var sender = r.Sender;
                if (sender == null)
                {
                    lastEx = new Exception("The provider " + r.Instance.ToQuoted() + " of the route " + r.Name.ToQuoted() + " wasn't found!");
                    continue;
                }
                bool isLast = i == (count - 1);
                if (IsNotOnChannel(r, digits) && !isLast)
                    continue;
                var m = message.Clone();
                //  The token is provider specific
                var token = message.ResponseToken;
                var isTokenRoute = (r == tokenRoute) && (token != null);
                m.ResponseToken = isTokenRoute ? token.Substring(r.Name.Length + 1) : null;
                //  Only allow the channels of the route (a provider can have multiple channels, ex: Twilio WhatsApp and SMS as two routes), a reply uses the channel of the received message
                if (!isTokenRoute)
                    m.Channels = NarrowChannels(message.Channels, r.Channels);
                TextMessageSendResult res;
                try
                {
                    res = await sender.Send(m).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    SendFails.OnException(ex);
                    if (!FallbackOnError || isLast)
                        throw;
                    lastEx = ex;
                    continue;
                }
                if (res == null)
                    res = new TextMessageSendResult { Status = TextMessageStatuses.Unknown, Reference = message.Reference };
                res.Provider = r.Name;
                if (res.Status == TextMessageStatuses.Failed)
                {
                    if (res.Failure == TextMessageFailures.NotOnChannel)
                        SetNotOnChannel(r, digits);
                    last = res;
                    if (FallbackOn.Contains(res.Failure))
                        continue;
                    return res;
                }
                if ((res.Id != null) && !res.Status.IsFinal())
                    PendingOut[PendingKey(r, res.Id)] = new Pending(message, digits, isoCountry, candidates, i);
                return res;
            }
            if (last != null)
                return last;
            throw lastEx ?? new Exception("No provider could send the message!");
        }

        /// <summary>
        /// A provider reported that a message failed after it was sent, try the next route
        /// </summary>
        async Task FallbackLater(Pending p, TextMessageStatusUpdate failed)
        {
            TextMessageSendResult res;
            try
            {
                res = await SendUsing(p.Message, p.Digits, p.IsoCountry, p.Candidates, p.Index + 1).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                FallbackFails.OnException(ex);
                Msg?.AddMessage(LogPrefix + "Failed to send a message to " + p.Message.PhoneNumber + " using a fallback route", ex, MessageLevels.Warning);
                return;
            }
            //  Report the new attempt as a status update (the message id changes)
            PublishStatus(new TextMessageStatusUpdate
            {
                MessageId = res.Id,
                Reference = p.Message.Reference,
                System = p.Message.System,
                PhoneNumber = p.Message.PhoneNumber,
                Channel = res.Channel,
                Status = res.Status,
                Failure = res.Failure,
                ErrorCode = res.ErrorCode,
                Error = res.Status == TextMessageStatuses.Failed ? res.Error : "Fallback after: " + failed.Error,
                Provider = res.Provider,
                Time = DateTime.UtcNow,
            });
        }

        #endregion//Sending

        #region Receiving

        readonly TextMessageQueue<IncomingTextMessage> Incoming;
        readonly TextMessageQueue<TextMessageStatusUpdate> Statuses;

        public event Action<IncomingTextMessage> OnIncoming;
        public event Action<TextMessageStatusUpdate> OnStatus;

        static long Counter = DateTime.UtcNow.Ticks - new DateTime(2025, 1, 1).Ticks;

        void OnRouteIncoming(Route r, IncomingTextMessage m)
        {
            if (m == null)
                return;
            m = m.Clone();
            //  Make the id unique between providers, and the token identify the provider
            m.Id = String.Concat(r.Name, TokenSep, m.Id);
            m.ResponseToken = String.Concat(r.Name, TokenSep, m.ResponseToken);
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

        static TextMessageChannels NarrowChannels(TextMessageChannels message, TextMessageChannels route)
        {
            if (route == TextMessageChannels.Any)
                return message;
            if (message == TextMessageChannels.Any)
                return route;
            return message & route;
        }

        void OnRouteStatus(Route r, TextMessageStatusUpdate u)
        {
            if (u == null)
                return;
            u = u.Clone();
            var key = u.MessageId == null ? null : PendingKey(r, u.MessageId);
            Pending p = null;
            if ((key != null) && PendingOut.TryGetValue(key, out p))
            {
                //  The route that sent the message (status updates are received using the first route of the instance)
                r = p.Candidates[p.Index];
                u.Reference ??= p.Message.Reference;
                u.System ??= p.Message.System;
                if (u.Status.IsFinal())
                    PendingOut.TryRemove(key, out var _);
            }
            u.Provider ??= r.Name;
            PublishStatus(u);
            if ((u.Status != TextMessageStatuses.Failed) || (p == null))
                return;
            if (u.Failure == TextMessageFailures.NotOnChannel)
                SetNotOnChannel(r, p.Digits);
            if (FallbackOn.Contains(u.Failure) && (p.Index < (p.Candidates.Length - 1)))
                _ = FallbackLater(p, u);
        }

        void PublishStatus(TextMessageStatusUpdate u)
        {
            u.Id = "S" + Interlocked.Increment(ref Counter);
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
        /// Poll for any new text messages (from all providers), blocks until new messages are available
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new messages</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        public Task<IncomingTextMessage[]> GetIncoming(GetIncomingTextMessageRequest request)
            => Incoming.Poll(request?.LastId, request?.System);

        /// <summary>
        /// Poll for any new status updates (from all providers), blocks until new updates are available
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new updates</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        public Task<TextMessageStatusUpdate[]> GetStatusUpdates(GetTextMessageStatusRequest request)
            => Statuses.Poll(request?.LastId, request?.System);

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
            foreach (var x in NotOnChannel)
                if (x.Value < now)
                    NotOnChannel.TryRemove(x.Key, out var _);
        }

        #endregion//Housekeeping

        #region Stats

        readonly ExceptionTracker SendFails = new ExceptionTracker();
        readonly ExceptionTracker FallbackFails = new ExceptionTracker();
        readonly ExceptionTracker PollFails = new ExceptionTracker();

        public IEnumerable<Stats> GetStats()
        {
            const String n = nameof(TextMessageRouterService);
            foreach (var x in SendFails.GetStats(n, "Send."))
                yield return x;
            foreach (var x in FallbackFails.GetStats(n, "Fallback."))
                yield return x;
            foreach (var x in PollFails.GetStats(n, "Poll."))
                yield return x;
        }

        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(nameof(TextMessageRouterService));

        #endregion//Stats
    }
}
