using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Data;

namespace SysWeaver.Net
{


    // Do NOT dispose! only uses the dispose pattern to decrement counter
    public sealed class HttpSession : IDisposable
    {
#if DEBUG
        public override string ToString() => String.Concat("Token: ", Token, ", expires: ", new DateTime(ExpirationTime, DateTimeKind.Utc), ", auth: ", Auth);
#endif//DEBUG

        public HttpSession(HttpRateLimiterParams rateLimiterParams, String token, long utcNowTicks, long expirationDurationTicks, long keepAliveDurationTicks, String userAgent, String address, String httpProtocol, String deviceId)
        {
            RateLimiter = rateLimiterParams == null ? null : new HttpRateLimiter(rateLimiterParams);
            DeviceId = deviceId;
            Start = utcNowTicks;
            Token = token;
            ExpirationTick = utcNowTicks + expirationDurationTicks;
            InternalKeepAliveDurationTicks = keepAliveDurationTicks;
            Exp = utcNowTicks + keepAliveDurationTicks;
            UserAgent = userAgent;
            Address = address;
            HttpProtocol = httpProtocol;
        }

        internal readonly HttpRateLimiter RateLimiter;

        /// <summary>
        /// The time zone of the http client
        /// </summary>
        public String ClientTimeZone { get; internal set; }

        /// <summary>
        /// The language of the http client
        /// </summary>
        public String ClientLanguage { get; internal set; }

        /// <summary>
        /// The language to use, default to client language but can be overridden
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
                LanguageTimeStampText = HttpServerTools.ToEtag(value);
            }
        }

        DateTime InternalLanguageTimeStamp;

        /// <summary>
        /// When the language was changed (time stamp) as text
        /// </summary>
        public String LanguageTimeStampText { get; private set; }


        public readonly String DeviceId;

        public readonly long Start;

        public readonly String Token;

        public readonly String UserAgent;

        public readonly String Address;

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
        /// Check is the session have any of these tokens
        /// </summary>
        /// <param name="requiredTokens">Tokens required to continue</param>
        /// <returns></returns>
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

        public void SetAuth(Authorization auth)
        {
            var old = Interlocked.Exchange(ref InternalAuth, auth);
            if (old == auth)
                return;
            if (old != null)
            {
                old.OnRequestLogout -= Auth_OnRequestLogout;
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

        void Auth_OnRequestLogout(string reason)
        {
            OnAuthLogout?.Invoke(this, reason);
            SetAuth(null);
            InvalidateCache();

        }

        /// <summary>
        /// Session expiration tick (can expire earlier unless it's kept alive)
        /// </summary>
        public readonly long ExpirationTick;

        /// <summary>
        /// Number of ticks to keep the session alive on each touch
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
        /// When the session should expire 
        /// </summary>
        public long ExpirationTime
        {
            get
            {
                var e = Interlocked.Read(ref Exp);
                var r = ExpirationTick;
                return e < r ? e : r;
            }
        }
        /// <summary>
        /// True if we should expire this session
        /// </summary>
        /// <param name="utcNowTick"></param>
        /// <returns></returns>
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
        /// Whenever the session is used, update the expiration
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
        /// Increment the reuqest counter
        /// </summary>
        /// <returns></returns>
        public HttpSession IncRequestCounter()
        {
            Interlocked.Increment(ref InProgress);
            return this;
        }

        // Do NOT dispose! only uses the dispose pattern to decrement counter
        public void Dispose()
        {
            Interlocked.Decrement(ref InProgress);
            Interlocked.Exchange(ref Exp, DateTime.UtcNow.Ticks + KeepAliveDurationTicks);
        }

        internal readonly DataReferenceStorage DataRefs = new DataReferenceStorage(DataScopes.Session);


        #region Session data

        /// <summary>
        /// Get or create session data
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="create">The function to call if the data wasn't found (will only be executed once in a concurrent environment)</param>
        /// <returns>The found or created value</returns>
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
        /// Try to get some session data
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="val">The data (if present)</param>
        /// <returns>True if the data exists, else false</returns>
        public bool TryGet<T>(String key, out T val)
        {
            if (Values.TryGetValue(key, out var v))
            {
                val = (T)v;
                return true;
            }
            val = default;
            return false;
        }

        /// <summary>
        /// Try to remove some session data
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="val">The removed data (if present)</param>
        /// <returns>True if the data was removed, else false</returns>
        public bool TryRemove<T>(String key, out T val)
        {
            if (Values.TryRemove(key, out var v))
            {
                val = (T)v;
                return true;
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
            => Values.TryRemove(key, out var v);

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


        readonly ConcurrentDictionary<String, Object> Values = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);

        /// <summary>
        /// Returns a change counter for the cache.
        /// Changes every time the cache is cleared.
        /// Can be used to track changes.
        /// </summary>
        public long CacheTimeStamp => Interlocked.Read(ref InternalCacheTimeStamp);

        long InternalCacheTimeStamp;

        /// <summary>
        /// Invalidates the session cache
        /// </summary>
        public void InvalidateCache()
        {
            Cache.Clear();
            Interlocked.Increment(ref InternalCacheTimeStamp);
        }

        /// <summary>
        /// Invalidate the session caches if the predicate returns true
        /// </summary>
        /// <param name="shouldInvalidate">A function to determine if the entry shgould be cleared, the string is the local url</param>
        public void InvalidateCache(Func<String, bool> shouldInvalidate)
        {
            var c = Cache;
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

        internal void DoNewLogin()
        {
            Values.Clear();
            Interlocked.Increment(ref WaiterId);
            MessagesAdded.Change();
        }

        /// <summary>
        /// Called when a session is "closed" and moved to the removed sessions queue
        /// Clear up as much data as possible here
        /// </summary>
        internal void OnRemove()
        {
            Values.Clear();
            Cache.Clear();
            Interlocked.Increment(ref WaiterId);
            MessagesAdded.Change();
            SetAuth(null);
            //Messages.Clear();
        }


        internal readonly ConcurrentDictionary<String, HttpCacheEntry> Cache = new(StringComparer.Ordinal);


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

        const long QueueMessage = TimeSpan.TicksPerSecond * 15;


        readonly ConcurrentQueue<Message> Messages = new ConcurrentQueue<Message>();
        readonly BlockUntilChange MessagesAdded = new BlockUntilChange(false);


        long MessageId = 1;


        /// <summary>
        /// Add a message to be sent to all clients in this this session.
        /// If no session is polling messages, they are queued for a maximum of 15 seconds.
        /// </summary>
        /// <param name="b">The message to send</param>
        /// <param name="onlyLatest">If true, only the latest message of this type will be sent, else all queued messaged will be sent</param>
        /// <param name="validateAuth">If true, auth must be the same when sending response as when it was pushed</param>
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
            m.Enqueue(new Message(now, Auth?.Guid, b, onlyLatest, Interlocked.Increment(ref MessageId), validateAuth));
            MessagesAdded.Change();
        }

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

        public long MessageKeepAliveSeconds = 90;

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
            var shared = !req.NonShared;
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
            if (cc == req.Cc)
                return null;
            return new MessageStreamResponse
            {
                Cc = cc,
            };
        }

        #endregion//Session data

        internal void InvokeOnClose()
        {
            OnClose?.Invoke(this);
            DataRefs.Dispose();
        }

        /// <summary>
        /// Event fired when a session is closed
        /// </summary>
        public event Action<HttpSession> OnClose;

        /// <summary>
        /// Event fired when a an auth wan't to logout
        /// </summary>
        public event Action<HttpSession, String> OnAuthLogout;

        /// <summary>
        /// Event fired when a an auth has logged in
        /// </summary>
        public event Action<HttpSession> OnAuthLogin;

    }






    public class MessageStreamResponse
    {
        /// <summary>
        /// Messages from the server, this can be null if there are no new messages but the change counter have been updated
        /// </summary>
        public PushMessage[] Messages;
        
        /// <summary>
        /// The change counter to use for the next request
        /// </summary>
        public long Cc;
    }


    public class MessageStreamRequest
    {
        /// <summary>
        /// The messages types that should be returned
        /// </summary>
        public String[] MessageTypes;


        /// <summary>
        /// The change counter, use 0 for first request, then use the Cc from the response (if you you don't get a response continue using the last cc)
        /// </summary>
        public long Cc;


        public bool NonShared;

    }


}
