using System;


namespace SysWeaver.MicroService
{

    public enum WebApiCaches
    {
        /// <summary>
        /// Caching is per session if the method takes a HttpServerRequest parameter.
        /// </summary>
        AutoDetect = 0,
        /// <summary>
        /// Caching is independent of sessions (same cache for all sessions).
        /// </summary>
        Globally = 1,
        /// <summary>
        /// Caching is for a session, so request meta data such as user, ip, user agent etc can be used.
        /// </summary>
        PerSession = 2,
    }


    public static class WebApiTools
    {
        /// <summary>
        /// Client side cache duration for static results.
        /// 30 seconds should be ok, is a service restart (update) takes more than this there's no room for error.
        /// </summary>
        public const int CacheClientStatic = 30;


        /// <summary>
        /// Server side cache duration for static results.
        /// 50 years should do it.
        /// </summary>
        public const int CacheServerStatic = 60 * 60 * 24 * 366 * 50;
    }

    /// <summary>
    /// Put this attribute on a type or method to specify the request cache duration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiRequestCacheAttribute : Attribute
    {

        /// <summary>
        /// Put this attribute on a type or method to specify the request cache duration.
        /// </summary>
        /// <param name="duration">The duration in seconds that the same request should be cached on the server, i.e the WriteStream / GetData for the same request from multiple clients within this period will only result in a single call to these methods (reduces server load).</param>
        /// <param name="cache">Specifies cache behaviour</param>
        public WebApiRequestCacheAttribute(int duration, WebApiCaches cache = WebApiCaches.AutoDetect)
        {
            Duration = duration > 0 ? duration : 0;
            if (cache == WebApiCaches.PerSession)
                Duration = -Duration;
            AutoDetectPerSession = cache == WebApiCaches.AutoDetect;
        }

        /// <summary>
        /// The duration in seconds that the same request should be cached on the server, i.e the WriteStream / GetData for the same request from multiple clients within this period will only result in a single call to these methods (reduces server load).
        /// </summary>
        public readonly int Duration;

        /// <summary>
        /// If true, the per session functionality should be auto-detected.
        /// </summary>
        public readonly bool AutoDetectPerSession;
    }


    /// <summary>
    /// Put this attribute on a type or method to specify that the request should be cached as a static resource (not changing during the service lifetime)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiClientCacheStaticAttribute : WebApiClientCacheAttribute
    {

        /// <summary>
        /// Put this attribute on a type or method to specify that the request should be cached forever
        /// </summary>
        public WebApiClientCacheStaticAttribute() : base(WebApiTools.CacheClientStatic)
        {
        }
    }


    /// <summary>
    /// Put this attribute on a type or method to specify that the request should be cached as a static resource (not changing during the service lifetime)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiRequestCacheStaticAttribute : WebApiRequestCacheAttribute
    {

        /// <summary>
        /// Put this attribute on a type or method to specify that the request should be cached forever
        /// </summary>
        /// <param name="cache">Specifies cache behaviour</param>
        public WebApiRequestCacheStaticAttribute(WebApiCaches cache = WebApiCaches.AutoDetect) : base(WebApiTools.CacheServerStatic, cache)
        {
        }
    }



}
