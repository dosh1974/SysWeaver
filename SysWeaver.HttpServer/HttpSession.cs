using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Data;

namespace SysWeaver.Net
{



    // Do NOT dispose! only uses the dispose pattern to decrement counter
    /// <summary>
    /// A client session, identified by a random token stored in the session cookie (see <see cref="HttpServerBase"/>).
    /// Holds the logged in user (<see cref="Auth"/>), language, per session data, the per session response cache, data references and the push message queue.
    /// </summary>
    /// <remarks>
    /// Thread safe (used concurrently by all requests of the session).
    /// Do NOT dispose to release the session: the dispose pattern is only used to decrement the in progress request counter (see <see cref="IncRequestCounter"/>).
    /// The session token is replaced (rotated) when a user logs in using <see cref="HttpServerBase.RunOnLogin(HttpSession, HttpServerRequest)"/>, the session object (and its data) is kept.
    /// </remarks>
    public sealed class HttpSession : IDisposable
    {
#if DEBUG
        public override string ToString() => String.Concat("Token: ", Token, ", expires: ", new DateTime(ExpirationTime, DateTimeKind.Utc), ", auth: ", Auth);
#endif//DEBUG

        /// <summary>
        /// Create a session (sessions are created by the server).
        /// </summary>
        /// <param name="rateLimiterParams">Optional per session rate limits, null for no limits</param>
        /// <param name="token">The session token (the value of the session cookie)</param>
        /// <param name="utcNowTicks">The current UTC time in ticks</param>
        /// <param name="keepAliveDurationTicks">Number of ticks to keep the session alive after each use</param>
        /// <param name="userAgent">The User-Agent of the client</param>
        /// <param name="address">The IP address of the client</param>
        /// <param name="httpProtocol">The http protocol version</param>
        /// <param name="deviceId">The device id (from the device id cookie)</param>
        /// <param name="siteRoot">The prefix of the request that created the session</param>
        public HttpSession(HttpRateLimiterParams rateLimiterParams, String token, long utcNowTicks, long keepAliveDurationTicks, String userAgent, String address, String httpProtocol, String deviceId, String siteRoot)
        {
            RateLimiter = rateLimiterParams == null ? null : new HttpRateLimiter(rateLimiterParams);
            DeviceId = deviceId;
            Start = utcNowTicks;
            Token = token;
            InternalKeepAliveDurationTicks = keepAliveDurationTicks;
            Exp = utcNowTicks + keepAliveDurationTicks;
            UserAgent = userAgent;
            Address = address;
            HttpProtocol = httpProtocol;
            SiteRoot = siteRoot;
        }

        /// <summary>
        /// The prefix (ex: "https://host/") of the request that created the session.
        /// </summary>
        public readonly String SiteRoot;
        /// <summary>
        /// The per session rate limiter, null if there are no per session limits.
        /// </summary>
        internal readonly HttpRateLimiter RateLimiter;

        /// <summary>
        /// The time zone of the http client (as reported by the client using the "serverTime" end point), may be null.
        /// </summary>
        public String ClientTimeZone { get; internal set; }

        /// <summary>
        /// The language of the http client, may be null.
        /// </summary>
        public String ClientLanguage { get; internal set; }

        /// <summary>
        /// The language to use, initialized from the Accept-Language header (best supported match, else "en") but can be overridden by the user or by the language of a logged in user.
        /// </summary>
        public String Language { get; internal set; }

        /// <summary>
        /// When the language was changed (time stamp)
        /// </summary>
        public DateTime LanguageTimeStamp
        {
            get => InternalLanguageTimeStamp;
            internal set
            {
                if (value == InternalLanguageTimeStamp)
                    return;
                InternalLanguageTimeStamp = value;
            }
        }

        DateTime InternalLanguageTimeStamp;

        /// <summary>
        /// When the language was changed (time stamp) as text, null if the time stamp was never set
        /// </summary>
        public String LanguageTimeStampText
        {
            get
            {
                //  Computed on demand (every new session sets the time stamp, but the text is rarely used)
                var ts = InternalLanguageTimeStamp;
                var ticks = ts.Ticks;
                if (ticks == 0)
                    return null;
                var c = LazyLanguageTimeStampText;
                if ((c != null) && (c.Item1 == ticks))
                    return c.Item2;
                c = Tuple.Create(ticks, HttpServerTools.ToEtag(ts));
                LazyLanguageTimeStampText = c;
                return c.Item2;
            }
        }

        /// <summary>
        /// The time stamp ticks and text, the ticks are stored so that a text is never used for a different time stamp
        /// </summary>
        Tuple<long, String> LazyLanguageTimeStampText;


        /// <summary>
        /// The device id (from the device id cookie, client controlled, created when missing).
        /// </summary>
        public readonly String DeviceId;

        /// <summary>
        /// UTC ticks when the session was created.
        /// </summary>
        public readonly long Start;

        /// <summary>
        /// The session token (the value of the session cookie). This is a secret, anyone knowing it can use the session.
        /// Do NOT assign, the server replaces the token when a user logs in (see <see cref="HttpServerBase.RunOnLogin(HttpSession, HttpServerRequest)"/>).
        /// </summary>
        public String Token;

        /// <summary>
        /// Lock used by the server when changing the <see cref="Token"/> (and removing the session) so that the session map stays consistent.
        /// </summary>
        internal readonly Object TokenLock = new Object();

        /// <summary>
        /// The User-Agent of the client that created the session (empty if not sent).
        /// </summary>
        public readonly String UserAgent;

        /// <summary>
        /// The IP address of the client that created the session (the closest peer, proxies are not resolved).
        /// </summary>
        public readonly String Address;

        /// <summary>
        /// The http protocol version of the latest requests (updated for the first 100 requests).
        /// </summary>
        public String HttpProtocol;


        /// <summary>
        /// Total number of request made in this session
        /// </summary>
        public long RequestCount => Interlocked.Read(ref Count);

        /// <summary>
        /// Number of requests in progress
        /// </summary>
        public long RequestInProgress => Interlocked.Read(ref InProgress);

        long Count = 1;

        long InProgress = 0;

        /// <summary>
        /// Auth of this user
        /// </summary>
        public Authorization Auth => InternalAuth;


        /// <summary>
        /// Check if the session has any of these tokens.
        /// </summary>
        /// <param name="requiredTokens">Lower cased tokens, any of them grants access. Null = no auth required (always true), empty = any logged in user</param>
        /// <returns>True if access is granted</returns>
        public bool IsValid(IReadOnlyList<String> requiredTokens)
        {
            if (requiredTokens == null)
                return true;
            var a = InternalAuth;
            if (a == null)
                return false;
            return a.IsValid(requiredTokens);
        }


        Authorization InternalAuth;

        /// <summary>
        /// True if <see cref="InternalAuth"/> is owned by this session (and should be disposed when replaced)
        /// </summary>
        bool InternalAuthOwned;

        /// <summary>
        /// Set (or clear) the logged in user, the session cache is invalidated and <see cref="OnAuthLogin"/> is raised when a user is set.
        /// If a user is set, the session language is changed to the user's language (if any).
        /// </summary>
        /// <param name="auth">The authorization, null to log out</param>
        /// <remarks>The session takes ownership of <paramref name="auth"/>: the previous authorization (if different) is disposed, unless it was set as not owned by the server
        /// (an authorization from the Authorization / API key header is cached and shared between sessions, so it's never disposed by a session).
        /// Call <see cref="HttpServerBase.RunOnLogin(HttpSession, HttpServerRequest)"/> after setting a user so that the server tracks the user's sessions.</remarks>
        public void SetAuth(Authorization auth) => SetAuth(auth, true);

        /// <summary>
        /// Set (or clear) the logged in user, see <see cref="SetAuth(Authorization)"/>.
        /// </summary>
        /// <param name="auth">The authorization, null to log out</param>
        /// <param name="owned">True if the session owns <paramref name="auth"/> (it's disposed when replaced), false if it's shared (ex: cached by the <see cref="AuthManager"/>)</param>
        internal void SetAuth(Authorization auth, bool owned)
        {
            Authorization old;
            bool oldOwned;
            lock (AuthLock)
            {
                old = InternalAuth;
                if (old == auth)
                    return;
                oldOwned = InternalAuthOwned;
                InternalAuthOwned = owned;
                Volatile.Write(ref InternalAuth, auth);
            }
            if (old != null)
            {
                old.OnRequestLogout -= Auth_OnRequestLogout;
                if (oldOwned)
                    old.Dispose();
            }
            if (auth != null)
            {
                if (auth.Language != null)
                    Language = auth.Language;
                auth.OnRequestLogout += Auth_OnRequestLogout;
                OnAuthLogin?.Invoke(this);
            }
            InvalidateCache();
        }

        readonly Object AuthLock = new Object();

        void Auth_OnRequestLogout(string reason)
        {
            OnAuthLogout?.Invoke(this, reason);
            SetAuth(null);
            InvalidateCache();

        }

        /// <summary>
        /// Number of ticks to keep the session alive on each touch, setting it also extends the expiration from now (values of zero or less are ignored).
        /// </summary>
        public long KeepAliveDurationTicks
        {
            get => Interlocked.Read(ref InternalKeepAliveDurationTicks);
            set
            {
                if (value <= 0)
                    return;
                Interlocked.Exchange(ref InternalKeepAliveDurationTicks, value);
                Interlocked.Exchange(ref Exp, DateTime.UtcNow.Ticks + value);
            }
        }
        long InternalKeepAliveDurationTicks;

        /// <summary>
        /// UTC ticks when the session should expire.
        /// Sessions with 3 or fewer requests and no strongly authenticated user expire 30 seconds after the last activity (to quickly remove sessions from clients that don't store cookies).
        /// </summary>
        public long ExpirationTime
        {
            get
            {
                var e = Interlocked.Read(ref Exp);
                if (Interlocked.Read(ref Count) > 3)
                    return e;
                if (Auth?.WeakMethod ?? true)
                    return (e - KeepAliveDurationTicks) + (TimeSpan.TicksPerMinute >> 1);
                return e;
            }
        }

        /// <summary>
        /// True if we should expire this session (no request in progress and the expiration time has passed).
        /// </summary>
        /// <param name="utcNowTick">The current UTC time in ticks</param>
        /// <returns>True if the session can be removed</returns>
        public bool CanExpire(long utcNowTick)
        {
            if (Interlocked.Read(ref InProgress) > 0)
                return false;
            var time = ExpirationTime;
            return utcNowTick > time;
        }

        long Exp;

        /// <summary>
        /// When the session was last used (in a request)
        /// </summary>
        public long LastActivity => Interlocked.Read(ref Exp) - KeepAliveDurationTicks;

        /// <summary>
        /// Whenever the session is used, update the expiration and the request counter.
        /// </summary>
        /// <param name="utcNowTicks">DateTime.UtcNow.Ticks</param>
        /// <param name="req">The request</param>
        public void Touch(long utcNowTicks, HttpServerRequest req)
        {
            Interlocked.Exchange(ref Exp, utcNowTicks + KeepAliveDurationTicks);
            if (Interlocked.Increment(ref Count) < 100)
                HttpProtocol = req.ProtocolVersion;
        }

        /// <summary>
        /// Increment the in progress request counter, dispose the session when the request is done to decrement it.
        /// </summary>
        /// <returns>This session</returns>
        public HttpSession IncRequestCounter()
        {
            Interlocked.Increment(ref InProgress);
            return this;
        }

        // Do NOT dispose! only uses the dispose pattern to decrement counter
        /// <summary>
        /// Decrement the in progress request counter and extend the expiration (does NOT close the session).
        /// </summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref Exp, DateTime.UtcNow.Ticks + KeepAliveDurationTicks);
            Interlocked.Decrement(ref InProgress);
        }


        DataReferenceStorage LazyDataRefs;

        /// <summary>
        /// The session scoped data references (created on first use).
        /// </summary>
        internal DataReferenceStorage DataRefs
        {
            get
            {
                var l = LazyDataRefs;
                if (l != null)
                    return l;
                l = new DataReferenceStorage(DataScopes.Session);
                var t = Interlocked.CompareExchange(ref LazyDataRefs, l, null);
                if (t == null)
                    return l;
                l.Dispose();
                return t;
            }
        }


        #region Session data

        /// <summary>
        /// Get or create session data.
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="create">The function to call if the data wasn't found (will only be executed once in a concurrent environment, unless the value is added using <see cref="TryAdd{T}"/> or <see cref="Set{T}"/> concurrently)</param>
        /// <returns>The found or created value</returns>
        /// <exception cref="InvalidCastException">Thrown if the existing value isn't a <typeparamref name="T"/></exception>
        public T GetOrCreate<T>(String key, Func<T> create)
        {
            var v = Values;
            if (v.TryGetValue(key, out var val))
                return (T)val;
            lock (v)
            {
                if (v.TryGetValue(key, out val))
                    return (T)val;
                var vv = create();
                v[key] = vv;
                return vv;
            }
        }

        /// <summary>
        /// Try to add some session data
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="val">The value to add</param>
        /// <returns>True if the value was added to the session</returns>
        public bool TryAdd<T>(String key, T val) => Values.TryAdd(key, val);

        /// <summary>
        /// Try to get some session data.
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="val">The data (if present)</param>
        /// <returns>True if the data exists, else false</returns>
        /// <exception cref="InvalidCastException">Thrown if the existing value isn't a <typeparamref name="T"/></exception>
        public bool TryGet<T>(String key, out T val)
        {
            var vals = LazyValues;
            if (vals != null)
            {
                if (vals.TryGetValue(key, out var v))
                {
                    val = (T)v;
                    return true;
                }
            }
            val = default;
            return false;
        }

        /// <summary>
        /// Try to remove some session data.
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="val">The removed data (if present)</param>
        /// <returns>True if the data was removed, else false</returns>
        /// <exception cref="InvalidCastException">Thrown if the removed value isn't a <typeparamref name="T"/> (the value is removed anyway)</exception>
        public bool TryRemove<T>(String key, out T val)
        {
            var vals = LazyValues;
            if (vals != null)
            {
                if (vals.TryRemove(key, out var v))
                {
                    val = (T)v;
                    return true;
                }
            }
            val = default;
            return false;
        }

        /// <summary>
        /// Try to remove some session data
        /// </summary>
        /// <param name="key">The unique key for this data</param>
        /// <returns>True if the data was removed, else false</returns>
        public bool TryRemove(String key)
            => LazyValues?.TryRemove(key, out var v) ?? false;

        /// <summary>
        /// Set some session data (add or replace)
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="val">The value to set (or add)</param>
        public void Set<T>(String key, T val)
        {
            Values[key] = val;
        }

        ConcurrentDictionary<String, Object> LazyValues;

        /// <summary>
        /// The session data (created on first use). Cleared when a user logs in and when the session is removed.
        /// </summary>
        ConcurrentDictionary<String, Object> Values
        {
            get
            {
                var l = LazyValues;
                if (l != null)
                    return l;
                l = new(StringComparer.Ordinal);
                return Interlocked.CompareExchange(ref LazyValues, l, null) ?? l;
            }
        }

        /// <summary>
        /// Returns a change counter for the cache.
        /// Changes every time the cache is cleared.
        /// Can be used to track changes.
        /// </summary>
        public long CacheTimeStamp => Interlocked.Read(ref InternalCacheTimeStamp);

        long InternalCacheTimeStamp;

        /// <summary>
        /// Invalidates the session response cache and increments <see cref="CacheTimeStamp"/>.
        /// </summary>
        public void InvalidateCache()
        {
            Cache?.Clear();
            Interlocked.Increment(ref InternalCacheTimeStamp);
        }

        /// <summary>
        /// Invalidate the session cache entries for which the predicate returns true (<see cref="CacheTimeStamp"/> is not changed).
        /// </summary>
        /// <param name="shouldInvalidate">A function to determine if the entry should be cleared, the string is the local url</param>
        public void InvalidateCache(Func<String, bool> shouldInvalidate)
        {
            var c = Cache;
            if (c == null)
                return;
            List<String> l = [];
            foreach (var x in c)
            {
                if (!shouldInvalidate(x.Value.LocalUrl))
                    continue;
                l.Add(x.Key);
            }
            foreach (var x in l)
                c.TryRemove(x, out var _);
        }

        /// <summary>
        /// Called when a user logs in, clears the session data and wakes up any waiting message requests.
        /// </summary>
        internal void DoNewLogin()
        {
            LazyValues?.Clear();
            Interlocked.Increment(ref WaiterId);
            LazyMessagesAdded?.Change();
        }

        /// <summary>
        /// Called when a session is "closed" and moved to the removed sessions queue
        /// Clear up as much data as possible here
        /// </summary>
        internal void OnRemove()
        {
            LazyValues?.Clear();
            Cache?.Clear();
            Interlocked.Increment(ref WaiterId);
            LazyMessagesAdded?.Change();
            SetAuth(null);
            //Messages.Clear();
        }


        /// <summary>
        /// The per session response cache, this can be null, if you want to store something use the SaveCache property instead.
        /// </summary>
        internal LowAllocConcurrentDictionary<String, HttpCacheEntry> Cache;


        /// <summary>
        /// Return Cache or create a new Cache
        /// </summary>
        internal LowAllocConcurrentDictionary<String, HttpCacheEntry> SaveCache
        {
            get
            {
                var l = Cache;
                if (l != null)
                    return l;
                l = new(StringComparer.Ordinal);
                return Interlocked.CompareExchange(ref Cache, l, null) ?? l;
            }
        }

        /// <summary>
        /// A queued push message.
        /// </summary>
        sealed class Message
        {
            public readonly long Added;
            public readonly String Auth;
            public readonly PushMessage Msg;
            public readonly bool OnlyLatest;
            public readonly long Id;
            public readonly bool ValidateAuth;

            public Message(long now, string auth, PushMessage msg, bool onlyLatest, long id, bool validateAuth)
            {
                Added = now;
                Auth = auth;
                Msg = msg;
                Id = id;
                OnlyLatest = onlyLatest;
                ValidateAuth = validateAuth;
            }
        };

        /// <summary>
        /// Messages are kept in the queue for at least this long (old messages are removed when new messages are pushed).
        /// </summary>
        const long QueueMessage = TimeSpan.TicksPerSecond * 15;

        /// <summary>
        /// Can be null
        /// </summary>
        ConcurrentQueue<Message> LazyMessages;

        /// <summary>
        /// Create a new message queue if it doesn't exist
        /// </summary>
        ConcurrentQueue<Message> Messages
        {
            get
            {
                var l = LazyMessages;
                if (l != null)
                    return l;
                l = new();
                return Interlocked.CompareExchange(ref LazyMessages, l, null) ?? l;
            }
        }

        BlockUntilChange LazyMessagesAdded;

        BlockUntilChange MessagesAdded
        {
            get
            {
                var l = LazyMessagesAdded;
                if (l != null)
                    return l;
                l = new(false);
                return Interlocked.CompareExchange(ref LazyMessagesAdded, l, null) ?? l;
            }
        }


        long MessageId = 1;


        /// <summary>
        /// Add a message to be sent to all clients polling this session (see <see cref="GetMessages"/>).
        /// Messages are queued for at least 15 seconds (old messages are only removed when new ones are pushed).
        /// </summary>
        /// <param name="b">The message to send, its type is lower cased (the instance is modified)</param>
        /// <param name="onlyLatest">If true, only the latest message of this type will be sent, else all queued messages will be sent</param>
        /// <param name="validateAuth">If true, the message is only delivered to pollers with the same logged in user (or no user) as when it was pushed</param>
        public void PushMessage(PushMessage b, bool onlyLatest = true, bool validateAuth = true)
        {
            b.Type = b.Type.FastToLower();
            var m = Messages;
        //  Remove old messages
            var now = DateTime.UtcNow.Ticks;
            var expire = now - QueueMessage;
            if (m.TryPeek(out var x))
            {
                if (x.Added < expire)
                {
                    lock (m)
                    {
                        while (m.TryPeek(out x))
                        {
                            if (x.Added >= expire)
                                break;
                            m.TryDequeue(out x);
                        }
                    }
                }
            }
#if DEBUG
//            Console.WriteLine("Pushing message: " + b);
#endif//DEBUG
            var auth = Auth?.Guid;
            // The id must be assigned and the message enqueued atomically, else a poller may see a newer id first and skip this message
            lock (m)
                m.Enqueue(new Message(now, auth, b, onlyLatest, Interlocked.Increment(ref MessageId), validateAuth));
            MessagesAdded.Change();
        }

        /// <summary>
        /// Get the queued messages newer than the change counter that the poller asked for (and is allowed to see).
        /// </summary>
        /// <param name="cc">The change counter, updated to the id of the newest message seen</param>
        /// <param name="auth">The guid of the poller's user, null if none</param>
        /// <param name="returnOn">The (lower cased) message types to return</param>
        /// <param name="messages">The message queue</param>
        /// <returns>The messages, null if there are none</returns>
        MessageStreamResponse GetValidMessage(ref long cc, String auth, HashSet<String> returnOn, ConcurrentQueue<Message> messages)
        {
            List<PushMessage> ret = null;
            Dictionary<String, PushMessage> latest = null;  
            foreach (var m in messages)
            {
                var newId = m.Id;
                var msg = m.Msg;
                if (newId <= cc)
                    continue;
                var key = msg.Type;
                cc = newId;
                if (!returnOn.Contains(key))
                {
#if DEBUG
//                    Console.WriteLine("Reject message: " + m.Msg + " [not valid key]");
#endif//DEBUG
                    continue;
                }
                if (m.ValidateAuth)
                {
                    if (m.Auth != auth)
                    {
#if DEBUG
                        //                  Console.WriteLine("Reject message: " + m.Msg.Type + " [different auth]");
#endif//DEBUG
                        continue;
                    }
                }
                ret = ret ?? new List<PushMessage>();
                latest = latest ?? new Dictionary<string, PushMessage>(StringComparer.Ordinal);
                if (m.OnlyLatest)
                    latest[key] = msg;
#if DEBUG
//                Console.WriteLine("Accepted message: " + m.Msg);
#endif//DEBUG
                ret.Add(msg);
            }
            if (ret == null)
                return null;
            var c = ret.Count;
            int o = 0;
            for (int i = 0; i < c; ++ i)
            {
                var m = ret[i];
                if (latest.TryGetValue(m.Type, out var lm))
                    if (lm != m)
                        continue;
                ret[o] = m;
                ++o;
            }
#if DEBUG
            if (o <= 0)
                throw new Exception("Internal error!");
#endif//DEBUG
            return new MessageStreamResponse
            {
                Cc = cc,
                Messages = ret.ToArray(o),
            };
        }

        long WaiterId;

        /// <summary>
        /// The maximum number of seconds a <see cref="GetMessages"/> call waits for messages (at least 5).
        /// </summary>
        public long MessageKeepAliveSeconds = 90;

        /// <summary>
        /// Long poll for push messages.
        /// A change counter of 0 returns immediately with a "server.connect" message, a change counter newer than the session's returns a "server.reconnect" message (the server has restarted).
        /// Else waits up to <see cref="MessageKeepAliveSeconds"/> for messages of the requested types (and the types in <see cref="HttpServerBase.ForcedMessages"/>).
        /// </summary>
        /// <param name="req">The request (message types and change counter)</param>
        /// <returns>The messages and the change counter to use next, null if nothing changed (timed out)</returns>
        public async Task<MessageStreamResponse> GetMessages(MessageStreamRequest req)
        {
            var auth = Auth?.Guid;
            var maxWaitSeconds = Math.Max(5, MessageKeepAliveSeconds);
            var returnOn = new HashSet<String>(HttpServerBase.ForcedMessages, StringComparer.Ordinal);
            var returnOnMessages = req.MessageTypes;
            if (returnOnMessages != null)
                foreach (var x in returnOnMessages)
                    returnOn.Add(x.FastToLower());   


            var ma = MessagesAdded;
            long waiterId = 0;
            var shared = false;// !req.NonShared;
            if (shared)
            {
            //  Shared pool, get an id and trigger a change to abort exisiting shared connections
                waiterId = Interlocked.Increment(ref WaiterId);
                ma.Change();
            }
            long cid = 0;
            var end = DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond * maxWaitSeconds;
            var cc = req.Cc;
            var current = Interlocked.Read(ref MessageId);
            if (cc == 0)
            {
                //  First message stream, return immediately
                return new MessageStreamResponse
                {
                    Cc = current,
                    Messages = HttpServerBase.MessageServerConnects,
                };
            }
            if (cc > current)
            {
                // Assume that service have restarted, send a reconnect, clients should reload their page since credentials etc will be invalidated
                return new MessageStreamResponse
                {
                    Cc = current,
                    Messages = HttpServerBase.MessageServerReconnects,
                };
            }
            var messages = Messages;
            if (messages != null)
            {
                for (; ; )
                {
                    var v = GetValidMessage(ref cc, auth, returnOn, messages);
                    if (v != null)
                        return v;
                    var wait = (end - DateTime.UtcNow.Ticks) / TimeSpan.TicksPerMillisecond;
                    if (wait <= 0)
                        break;
                    var prev = cid;
                    cid = await ma.WaitForChange(cid, (int)wait).ConfigureAwait(false);
                    if (prev == cid)
                        break;
                    //  Wait a small amount since there are cases where more than one message is pushed (this will give us a chance of sending all at once)
                    await Task.Delay(1).ConfigureAwait(false);
                    //  If auth have changed
                    if (Auth?.Guid != auth)
                        break;
                    //  If there is a newer shared request, abort this waiter
                    if (shared)
                        if (waiterId != Interlocked.Read(ref WaiterId))
                            break;
                }
            }
            if (cc == req.Cc)
                return null;
            return new MessageStreamResponse
            {
                Cc = cc,
            };
        }

        #endregion//Session data

        /// <summary>
        /// Raise <see cref="OnClose"/> and dispose the session data references.
        /// </summary>
        internal void InvokeOnClose()
        {
            OnClose?.Invoke(this);
            Interlocked.Exchange(ref LazyDataRefs, null)?.Dispose();
        }

        /// <summary>
        /// Event fired when a session is closed
        /// </summary>
        public event Action<HttpSession> OnClose;

        /// <summary>
        /// Event fired when the authorization of the session requests a logout (the argument is the reason), the auth is cleared after the event.
        /// </summary>
        public event Action<HttpSession, String> OnAuthLogout;

        /// <summary>
        /// Event fired when a user has been set on the session (see <see cref="SetAuth(Authorization)"/>).
        /// </summary>
        public event Action<HttpSession> OnAuthLogin;

    }






    /// <summary>
    /// The response of a push message long poll (see <see cref="HttpSession.GetMessages"/>).
    /// </summary>
    public class MessageStreamResponse
    {
        /// <summary>
        /// Messages from the server, this can be null if there are no new messages but the change counter have been updated
        /// </summary>
        public PushMessage[] Messages;

        /// <summary>
        /// The base url, prepend any relative urls with this to get the absolute url.
        /// </summary>
        public String Prefix;

        /// <summary>
        /// The change counter to use for the next request
        /// </summary>
        public long Cc;
    }


    /// <summary>
    /// A push message long poll request (see <see cref="HttpSession.GetMessages"/>).
    /// </summary>
    public class MessageStreamRequest
    {
        /// <summary>
        /// The message types that should be returned (case insensitive), the forced message types are always returned.
        /// </summary>
        public String[] MessageTypes;


        /// <summary>
        /// The change counter, use 0 for first request, then use the Cc from the response (if you don't get a response continue using the last cc).
        /// </summary>
        public long Cc;


        /// <summary>
        /// Not used.
        /// </summary>
        public bool NonShared;

    }


}
