using System;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;

namespace SysWeaver
{

    /// <summary>
    /// Check and exposes the SysWeaver.Tor services (if available)
    /// </summary>
    public static class TorService
    {

        static TorService()
        {
            var torType = TypeFinder.Get("SysWeaver.Tor.TorHttpClient, SysWeaver.Tor");
            CreateTorClient = (x) => null;
            if (torType != null)
            {
                try
                {
                    var mi = torType.GetMethod("Init", BindingFlags.Static | BindingFlags.NonPublic);
                    Init = Expression.Lambda<Action>(Expression.Call(mi)).Compile();
                    var m = torType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
                    var p = Expression.Parameter(typeof(Boolean));
                    var ce = Expression.Call(m, p);
                    var lce = Expression.Lambda<Func<bool, HttpClient>>(ce, p);
                    CreateTorClient = lce.Compile();
                    InternalProxy = (WebProxy)torType.GetField("Proxy", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                    IsAvailable = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to init TOR client: " + ex);
                }
            }
        }

        /// <summary>
        /// True if the tor tools are avialble
        /// </summary>
        public static bool IsAvailable;

        /// <summary>
        /// Create a tor client (will return null if Tor tools isn't available)
        /// </summary>
        public static Func<bool, HttpClient> CreateTorClient;

        /// <summary>
        /// The proxy to use to route through tor
        /// </summary>
        public static WebProxy Proxy
        {
            get
            {
                var l = InitLock;
                var p = InternalProxy;
                if (l == null)
                    return p;
                lock(l)
                {
                    var i = Interlocked.Exchange(ref Init, null);
                    if (i == null)
                        return p;
                    i();
                    Interlocked.Exchange(ref InitLock, null);
                    return p;
                }
            }
        }

        static Action Init;
        static Object InitLock = new object();

        static readonly WebProxy InternalProxy;


    }


}
