using System;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;

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
            CreateTorClient = () => null;
            if (torType != null)
            {
                CreateTorClient = Expression.Lambda<Func<HttpClient>>(Expression.Call(torType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public))).Compile();
                Proxy = (WebProxy)torType.GetField("Proxy", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                IsAvailable = true;
            }
        }

        /// <summary>
        /// True if the tor tools are avialble
        /// </summary>
        public static bool IsAvailable;

        /// <summary>
        /// Create a tor client (will return null if Tor tools isn't available)
        /// </summary>
        public static Func<HttpClient> CreateTorClient;

        /// <summary>
        /// The proxy to use to route through tor
        /// </summary>
        public static readonly WebProxy Proxy;


    }


}
