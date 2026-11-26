using System;


namespace SysWeaver.MicroService
{

    /// <summary>
    /// Put this attribute on a method to make it available online
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebApiAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on a method to make it available online, optionally specify a name
        /// </summary>
        /// <param name="url">An optional url (name) for the method, by default the method name is used. {0} is replaced with the method name.</param>
        public WebApiAttribute(String url = null)
        {
            Url = url;
        }

        /// <summary>
        /// Optional name instead of the default (method name)
        /// </summary>
        public readonly String Url;
    }




    /// <summary>
    /// The method returns raw data (i.e no serialization will happen):
    ///   With a mime type the method must return some readonly memory (ReadOnlyMemory&lt;Byte&gt;, Task or ValueTask of it).
    ///   Without a mime type (null, the default) the method must return a MemoryFile (MemoryFile, Task or ValueTask of it), the mime type of the file is used per call (derived from the file name if the mime is missing) and the data is the response.
    /// A non-null mime on a method that doesn't return readonly memory, a null mime on a method that doesn't return a MemoryFile, or an empty mime, throws when the API is created.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiRawAttribute : Attribute
    {
        /// <summary>
        /// The mimetype of the raw output data, null if the method returns a MemoryFile (the mime type is taken from the file per call)
        /// </summary>
        public readonly String Mime;
        /// <summary>
        /// True to disable compression of the output, false = compress if the mime type is compressible
        /// </summary>
        public readonly bool DisableCompression;

        /// <summary>
        /// Set to true if the response data is localized (different pending on the specified language)
        /// </summary>
        public readonly bool IsTranslated;

        /// <summary>
        /// The compression method used on the returned data
        /// </summary>
        public readonly String PreCompressedWith;

        /// <summary>
        /// The method returns raw data (i.e no serialization will happen).
        /// </summary>
        /// <param name="mime">The mimetype of the raw output data (the method must return readonly memory), null if the method returns a MemoryFile (the mime type of the file is used per call)</param>
        /// <param name="disableCompression">True to disable compression, else compression is used if the mime type is compressible (for a MemoryFile it's determined per call)</param>
        /// <param name="isTranslated">Set to true if the response data is localized (different pending on the specified language)</param>
        public WebApiRawAttribute(String mime = null, bool disableCompression = false, bool isTranslated = false)
        {
            Mime = mime;
            DisableCompression = disableCompression;
            IsTranslated = isTranslated;
        }

        /// <summary>
        /// The method must return some readonly memory that is already compressed, and in that case the function is treated as raw data (i.e no serialization will happen).
        /// </summary>
        /// <param name="mime">The mimetype of the raw output data</param>
        /// <param name="preCompressedWith">The compression method used on the returned data</param>
        /// <param name="isTranslated">Set to true if the response data is localized (different pending on the specified language)</param>
        public WebApiRawAttribute(String mime, String preCompressedWith, bool isTranslated = false)
        {
            Mime = mime;
            PreCompressedWith = preCompressedWith;
            IsTranslated = isTranslated;
        }


    }

    /// <summary>
    /// The method must return some readonly memory containing UTF8 encoded text (mime type "text/plain; charset=UTF-8"), and in that case the function is treated as raw data (i.e no serialization will happen).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiRawTextAttribute : WebApiRawAttribute
    {
        /// <summary>
        /// The method must returns some readonly memory containg UTF8 encoded text, and in that case the function should be treated as raw data (i.e no serialization will happen).
        /// </summary>
        /// <param name="disableCompression">True to disable compression, else compression is used (the text mime type is compressible)</param>
        public WebApiRawTextAttribute(bool disableCompression = false) : base("text/plain; charset=UTF-8", disableCompression)
        {
        }
    }


    /// <summary>
    /// Limit the call rate to an API on the service process level
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiServiceRateLimitAttribute : Attribute
    {
        /// <summary>
        /// Limit the call rate to an API on the service process level
        /// </summary>
        /// <param name="count">Number of request</param>
        /// <param name="duration">Over this time frame in seconds</param>
        /// <param name="maxQueue">The maximum number of request to keep queued</param>
        /// <param name="maxDelay">The maximum time to delay a request</param>
        public WebApiServiceRateLimitAttribute(int count = 10, int duration = 1, int maxQueue = 10, int maxDelay = 5)
        {
            Count = count;
            Duration = duration;
            MaxQueue = maxQueue;
            MaxDelay = maxDelay;
        }
        /// <summary>Number of requests allowed per duration</summary>
        public readonly int Count;
        /// <summary>The time frame in seconds</summary>
        public readonly int Duration;
        /// <summary>The maximum number of requests to keep queued</summary>
        public readonly int MaxQueue;
        /// <summary>The maximum time to delay a request</summary>
        public readonly int MaxDelay;
    }

    /// <summary>
    /// Limit the call rate to an API per session
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiSessionRateLimitAttribute : Attribute
    {
        /// <summary>
        /// Limit the call rate to an API per session
        /// </summary>
        /// <param name="count">Number of request</param>
        /// <param name="duration">Over this time frame in seconds</param>
        /// <param name="maxQueue">The maximum number of request to keep queued</param>
        /// <param name="maxDelay">The maximum time to delay a request</param>
        public WebApiSessionRateLimitAttribute(int count = 10, int duration = 1, int maxQueue = 10, int maxDelay = 5)
        {
            Count = count;
            Duration = duration;
            MaxQueue = maxQueue;
            MaxDelay = maxDelay;
        }
        /// <summary>Number of requests allowed per duration</summary>
        public readonly int Count;
        /// <summary>The time frame in seconds</summary>
        public readonly int Duration;
        /// <summary>The maximum number of requests to keep queued</summary>
        public readonly int MaxQueue;
        /// <summary>The maximum time to delay a request</summary>
        public readonly int MaxDelay;
    }


    /// <summary>
    /// Limit the call rate to an API per client IP address (IPv6 addresses are grouped by their /64 prefix).
    /// Slows down a single attacker (ex: password guessing), not a distributed one, combine with a permissive <see cref="WebApiServiceRateLimitAttribute"/>.
    /// Keep it permissive enough for many users behind one address (offices, NAT).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiIpRateLimitAttribute : Attribute
    {
        /// <summary>
        /// Limit the call rate to an API per client IP address
        /// </summary>
        /// <param name="count">Number of request</param>
        /// <param name="duration">Over this time frame in seconds</param>
        /// <param name="maxQueue">The maximum number of request to keep queued</param>
        /// <param name="maxDelay">The maximum time to delay a request</param>
        public WebApiIpRateLimitAttribute(int count = 10, int duration = 1, int maxQueue = 10, int maxDelay = 5)
        {
            Count = count;
            Duration = duration;
            MaxQueue = maxQueue;
            MaxDelay = maxDelay;
        }
        /// <summary>Number of requests allowed per duration</summary>
        public readonly int Count;
        /// <summary>The time frame in seconds</summary>
        public readonly int Duration;
        /// <summary>The maximum number of requests to keep queued</summary>
        public readonly int MaxQueue;
        /// <summary>The maximum time to delay a request</summary>
        public readonly int MaxDelay;
    }


    /// <summary>
    /// Track calls to this API in the intrusion detection of the http server (calls per client address, an intrusion is reported above a threshold).
    /// Audited API's (<see cref="WebApiAuditAttribute"/>) are tracked by default, use this for other key API's (ex: data exports, look ups).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebApiIntrusionTrackAttribute : Attribute
    {
        /// <summary>
        /// Track (or with false: don't track, ex: a frequently called audited API) calls to this API in the intrusion detection
        /// </summary>
        /// <param name="track">True to track calls, false to never track them (even if audited)</param>
        public WebApiIntrusionTrackAttribute(bool track = true)
        {
            Track = track;
        }

        /// <summary>
        /// True to track calls, false to never track them
        /// </summary>
        public readonly bool Track;
    }


    /// <summary>
    /// Put this attribute on web api's to report them to any audit loggers
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebApiAuditAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on web api's to report them to any audit loggers
        /// </summary>
        /// <param name="group">An optional group for the method.</param>
        public WebApiAuditAttribute(String group = null)
        {
            Group = group;
        }

        /// <summary>
        /// Optional group
        /// </summary>
        public readonly String Group;
    }


    /// <summary>
    /// Use this to modify the audit parameters value.
    /// The function must NEVER modify the actual value (rather return a new modified value of the same or any other type).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebApiAuditFilterParamsAttribute : Attribute
    {
        /// <summary>
        /// Use this to modify the audit parameters value.
        /// The function must NEVER modify the actual value (rather return a new modified value of the same or any other type).
        /// </summary>
        /// <param name="methodName">
        /// Name of a method in the type where the method is defined with the following signature: 
        /// Object Function(long callId, HttpServerRequest request, Object paramsValue); 
        /// or
        /// Object Function(long callId, Object paramsValue); 
        /// </param>
        public WebApiAuditFilterParamsAttribute(String methodName)
        {
            MethodName = methodName;
        }

        /// <summary>
        /// Name of the filter method
        /// </summary>
        public readonly String MethodName;
    }


    /// <summary>
    /// Use this to modify the audit return value.
    /// The function must NEVER modify the actual value (rather return a new modified value of the same or any other type).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebApiAuditFilterReturnAttribute : Attribute
    {
        /// <summary>
        /// Use this to modify the audit return value.
        /// The function must NEVER modify the actual value (rather return a new modified value of the same or any other type).
        /// </summary>
        /// <param name="methodName">
        /// Name of a method in the type where the method is defined with the following signature: 
        /// Object Function(long callId, HttpServerRequest request, Object returnValue);
        /// or
        /// Object Function(long callId, Object returnValue); 
        /// </param>
        public WebApiAuditFilterReturnAttribute(String methodName)
        {
            MethodName = methodName;
        }


        /// <summary>
        /// Name of the filter method
        /// </summary>
        public readonly String MethodName;
    }
}
