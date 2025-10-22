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
    /// The method must returns some readonly memory, and in that case the function should be treated as raw data (i.e no serialization will happen).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiRawAttribute : Attribute
    {
        /// <summary>
        /// The mimetype of the raw output data
        /// </summary>
        public readonly String Mime;
        /// <summary>
        /// True to compress the output, null = use mime default
        /// </summary>
        public readonly bool DisableCompression;

        /// <summary>
        /// Set to true if the response data is localized (different pending on the specified language)
        /// </summary>
        public readonly bool IsTranslated;

        /// <summary>
        /// The method must returns some readonly memory, and in that case the function should be treated as raw data (i.e no serialization will happen).
        /// </summary>
        /// <param name="mime">The mimetype of the raw output data</param>
        /// <param name="disableCompression">True to automatically detect if compression should happen, else the default behaviour is used</param>
        /// <param name="isTranslated">Set to true if the response data is localized (different pending on the specified language)</param>
        public WebApiRawAttribute(String mime, bool disableCompression = false, bool isTranslated = false)
        {
            Mime = mime;
            DisableCompression = disableCompression;
            IsTranslated = isTranslated;
        }
    }

    /// <summary>
    /// The method must returns some readonly memory containg UTF8 encoded text, and in that case the function should be treated as raw data (i.e no serialization will happen).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiRawTextAttribute : WebApiRawAttribute
    {
        /// <summary>
        /// The method must returns some readonly memory containg UTF8 encoded text, and in that case the function should be treated as raw data (i.e no serialization will happen).
        /// </summary>
        /// <param name="disableCompression">True to automatically detect if compression should happen, else the default behaviour is used</param>
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
        public readonly int Count;
        public readonly int Duration;
        public readonly int MaxQueue;
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
        public readonly int Count;
        public readonly int Duration;
        public readonly int MaxQueue;
        public readonly int MaxDelay;
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


        public readonly String MethodName;
    }
}
