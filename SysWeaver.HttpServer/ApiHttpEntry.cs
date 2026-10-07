using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Docs;
using SysWeaver.MicroService;
using SysWeaver.Translation;

namespace SysWeaver.Net
{




    /// <summary>
    /// A web API end point: an HTTP request handler bound to a single method on a service instance.
    /// Created through <see cref="Create"/>, normally by <see cref="ApiHttpServerModule"/> for every <c>[WebApi]</c> method it discovers.
    /// </summary>
    /// <remarks>
    /// The method may take zero or one argument, optionally followed by a trailing <see cref="HttpServerRequest"/> context parameter,
    /// and may return void, a value, <see cref="Task"/>/<see cref="Task{TResult}"/> or <see cref="ValueTask"/>/<see cref="ValueTask{TResult}"/>.
    /// A strongly typed invoker is compiled (via expression trees) for GET and POST at creation time.
    /// GET reads the argument from the query string using <see cref="ApiIoParams"/>, POST reads it from the request body
    /// (decompressed according to <c>Content-Encoding</c> and deserialized according to <c>Content-Type</c>).
    /// Results are serialized according to the <c>Accept</c> header, unless the method is marked with <see cref="WebApiRawAttribute"/> and returns <see cref="ReadOnlyMemory{T}"/> of bytes.
    /// Authorization, rate limiting and caching are not performed by this class: <see cref="HttpServerBase"/> uses <see cref="Auth"/>,
    /// <see cref="ServiceRateLimiter"/>, <see cref="SessionRateLimiter(HttpSession)"/>, <see cref="RequestCacheDuration"/> and <see cref="GetCacheKey(HttpServerRequest)"/> before invoking it.
    /// Instances are immutable after creation and thread safe.
    /// </remarks>
    public sealed partial class ApiHttpEntry : IHttpRequestHandler, IApiHttpServerEndPoint
    {
        
        /// <summary>
        /// Default required auth tokens for methods without a <see cref="WebApiAuthAttribute"/>, null means no authorization is required.
        /// </summary>
        public const String DefaultAuth = null;
        /// <summary>
        /// Default compression preference for methods with a positive (global) request cache duration, favours ratio since the result is compressed once and reused.
        /// </summary>
        public const String DefaultCachedCompression = "br:Best, deflate:Best, gzip:Best";
        /// <summary>
        /// Default compression preference for uncached (or per session cached) methods, favours speed.
        /// </summary>
        public const String DefaultCompression = "br:Balanced, deflate:Balanced, gzip:Balanced";
        /// <summary>
        /// Default prefix of the <see cref="Location"/> text.
        /// </summary>
        public const String DefaultLocationPrefix = "[API] ";



        /// <summary>
        /// Optional filter applied to the input value before it is audited (from <see cref="WebApiAuditFilterParamsAttribute"/>), used to hide sensitive data. Null if not specified.
        /// </summary>
        internal readonly Func<long, HttpServerRequest, Object, Object> FilterAuditParams;
        /// <summary>
        /// Optional filter applied to the return value before it is audited (from <see cref="WebApiAuditFilterReturnAttribute"/>). Null if not specified.
        /// </summary>
        internal readonly Func<long, HttpServerRequest, Object, Object> FilterAuditReturn;

        /// <summary>
        /// Audit callback invoked with the deserialized input before the method is called. Null unless the method has a <see cref="WebApiAuditAttribute"/>.
        /// </summary>
        internal readonly Action<long, HttpServerRequest, ApiHttpEntry, Object> OnStart;
        /// <summary>
        /// Audit callback invoked with the (unserialized) result after a successful call. Null unless the method is audited.
        /// </summary>
        internal readonly Action<long, HttpServerRequest, ApiHttpEntry, Object> OnEnd;
        /// <summary>
        /// Audit callback invoked when the method (or result encoding) throws. Null unless the method is audited.
        /// </summary>
        internal readonly Action<long, HttpServerRequest, ApiHttpEntry, Exception> OnException;

        static readonly ParameterExpression ValId = Expression.Parameter(typeof(long), "id");
        static readonly ParameterExpression ValRequest = Expression.Parameter(typeof(HttpServerRequest), "request");
        static readonly ParameterExpression ValValue = Expression.Parameter(typeof(Object), "value");


        /// <summary>
        /// Compile a delegate calling an audit filter method declared on the service type.
        /// </summary>
        /// <param name="o">The service instance (used for instance methods).</param>
        /// <param name="methodName">Name of a static or instance method with the signature <c>Object (long, HttpServerRequest, Object)</c> or <c>Object (long, Object)</c>.</param>
        /// <param name="filterType">Text used in the error message.</param>
        /// <returns>A delegate invoking the filter.</returns>
        /// <exception cref="Exception">No method with a matching name and signature was found.</exception>
        static Func<long, HttpServerRequest, Object, Object>  BuildFilter(Object o, String methodName, String filterType = "input")
        {
            var valId = ValId;
            var valRequest = ValRequest;
            var valValue = ValValue;
            var ot = o.GetType();
            Expression prog;
            var filterM = ot.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                [typeof(long), typeof(HttpServerRequest), typeof(Object)]);
            if (filterM == null)
            {
                filterM = ot.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                [typeof(long), typeof(Object)]);
                if (filterM == null)
                    throw new Exception("The audit " + filterType + " method named " + methodName.ToQuoted() + " is not found in the type " + ot.FullName.ToQuoted());
                if (filterM.IsStatic)
                    prog = Expression.Call(filterM, valId, valValue);
                else
                    prog = Expression.Call(Expression.Constant(o, ot), filterM, valId, valValue);
            }
            else
            {
                if (filterM.IsStatic)
                    prog = Expression.Call(filterM, valId, valRequest, valValue);
                else
                    prog = Expression.Call(Expression.Constant(o, ot), filterM, valId, valRequest, valValue);

            }
            return Expression.Lambda<Func<long, HttpServerRequest, Object, Object>>(prog, valId, valRequest, valValue).Compile();
        }

        /// <summary>
        /// The serializers used to read input and write output.
        /// </summary>
        public readonly ApiIoParams IoParams;

        /// <summary>
        /// Create an API end point for a method.
        /// </summary>
        /// <param name="ioParams">The serializers to use for input and output.</param>
        /// <param name="o">The instance to invoke the method on (must not be null, static methods are not supported).</param>
        /// <param name="method">The method to expose. It may take zero or one parameter, optionally followed by a <see cref="HttpServerRequest"/> parameter.</param>
        /// <param name="url">The local url of the end point, also used as the rate limiter key and in perf monitoring keys.</param>
        /// <param name="perfMonitor">Optional performance monitor, every call is tracked as <c>&quot;url [GET]&quot;</c> or <c>&quot;url [POST]&quot;</c>.</param>
        /// <param name="defaultAuth">Required auth tokens if neither the method nor the declaring type has a <see cref="WebApiAuthAttribute"/>.</param>
        /// <param name="defaultCachedCompression">Compression preference used when the method has a positive request cache duration.</param>
        /// <param name="defaultCompression">Compression preference used otherwise.</param>
        /// <param name="locationPrefix">Prefix for the <see cref="Location"/> text.</param>
        /// <param name="onStart">Audit start callback, ignored unless the method has a <see cref="WebApiAuditAttribute"/>.</param>
        /// <param name="onEnd">Audit end callback, ignored unless the method is audited.</param>
        /// <param name="onException">Audit exception callback, ignored unless the method is audited.</param>
        /// <returns>The new end point.</returns>
        /// <remarks>
        /// Settings are read from attributes on the method, falling back to the declaring type: <see cref="WebApiAuthAttribute"/>, <see cref="WebApiClientCacheAttribute"/>,
        /// <see cref="WebApiRequestCacheAttribute"/> (stored negated, i.e. per session, when auto detect is used and the method takes a request context),
        /// <see cref="WebApiCompressionAttribute"/>. Rate limiting (<see cref="WebApiServiceRateLimitAttribute"/>, <see cref="WebApiSessionRateLimitAttribute"/>),
        /// auditing and <see cref="WebApiRawAttribute"/> are read from the method only.
        /// If <paramref name="o"/> implements <see cref="IRunTimeWebApiAuth"/>, an entry in <see cref="IRunTimeWebApiAuth.MethodAuths"/> for the method name (or <c>&quot;*&quot;</c>) overrides any attribute.
        /// Compiles expression trees, so this is relatively expensive and intended to be called once per method.
        /// </remarks>
        /// <exception cref="Exception">Unknown pre-compression decoder or audit filter method not found.</exception>
        /// <exception cref="ArgumentException">The method signature isn't supported (e.g. more than one input parameter).</exception>
        public static ApiHttpEntry Create(ApiIoParams ioParams, Object o, MethodInfo method, String url, 
            PerfMonitor perfMonitor = null, 
            String defaultAuth = DefaultAuth, 
            String defaultCachedCompression = DefaultCachedCompression, 
            String defaultCompression = DefaultCompression, 
            String locationPrefix = DefaultLocationPrefix,
            Action<long, HttpServerRequest, ApiHttpEntry, Object> onStart = null,
            Action<long, HttpServerRequest, ApiHttpEntry, Object> onEnd = null,
            Action<long, HttpServerRequest, ApiHttpEntry, Exception> onException = null
            )
        {
            //  Determine paramaters type
            var p = method.GetParameters();
            var pl = p.Length;
#if DEBUG
            foreach (var x in p)
            {
                if (x.IsOut)
                    throw new Exception("Methods with out paramaters may not be used! Found in method \"" + method + "\"");
            }
#endif//DEBUG
            bool hasContext = false;
            if (pl > 0)
            {
                hasContext = p[pl - 1].ParameterType == typeof(HttpServerRequest);
                if (hasContext)
                {
                    --pl;
                }
            }


            Type serType = null;
            ParameterInfo pi = null;
            if (pl == 1)
            {
                pi = p[0];
                serType = pi.ParameterType;
            }
#if DEBUG
            //  Would fail anyway when the call expression is built (with a less clear message)
            if (pl > 1)
                throw new ArgumentException("WebApi methods may take at most one parameter (plus an optional trailing " + nameof(HttpServerRequest) + "), found in method \"" + method + "\" of \"" + method.DeclaringType?.FullName + "\"", nameof(method));
#endif//DEBUG
            if (serType == null)
                if (pl > 1)
                    serType = typeof(Object[]);
            //  Determine return type and if it's a task
            ParameterInfo ri = method.ReturnParameter;
            Type retSerType = method.ReturnType;
            if (NoRetSerTypes.TryGetValue(retSerType, out var callType))
            {
                retSerType = null;
                ri = null;
            }
            Type taskType = null;
            if (retSerType != null)
            {
                if (retSerType.IsGenericType)
                {
                    if (GenTaskTypes.TryGetValue(retSerType.GetGenericTypeDefinition(), out var ct))
                    {
                        taskType = retSerType;
                        callType = ct;
                        retSerType = retSerType.GetGenericArguments()[0];
                    }
                }
            }
            //  Check if we have some raw output instead of serializable content.
            String rawMime = null;
            bool rawCompress = false;
            bool rawIsTranslated = false;
            ICompDecoder decoder = null;
            if (typeof(ReadOnlyMemory<byte>).IsAssignableFrom(retSerType))
            {
                var raw = method.GetCustomAttribute<WebApiRawAttribute>(true);
                if (raw != null)
                {
                    rawMime = raw.Mime;
                    var dec = raw.PreCompressedWith;
                    if (!String.IsNullOrEmpty(dec))
                    {
                        decoder = CompManager.GetFromHttp(dec);
                        if (decoder == null)
                            throw new Exception("Decoder \"" + dec + "\" isn't found, when generating API for \"" + method.Name + "\" in \"" + method.DeclaringType.FullName + "\"");
                    }
                    if (MimeTypeMap.TryGetMimeType(rawMime, out var mi))
                    {
                        rawMime = mi.Item1;
                        rawCompress = raw.DisableCompression ? false : mi.Item2;
                    }
                    else
                    {
                        rawCompress = raw.DisableCompression ? false : true;
                    }
                    rawIsTranslated = raw.IsTranslated;
                }
            }
            var objExp = Expression.Constant(o);
            //  Get methods
            IInvokeApi get = null;
            IInvokeApi getAsync = null;
            IInvokeApi post = null;
            IInvokeApi postAsync = null;
            bool haveArgs = serType != null;
            var contextType = ContextType;
            var contextParam = ContextParam;
            if (haveArgs)
            {
                var arg = Expression.Parameter(serType);
                //  Have arguments
                if (retSerType == null)
                {
                    //  No return data
                    switch (callType)
                    {
                        case CallTypes.AsyncTask:
                            //  Is async call
                            if (hasContext)
                            {
                                var ft = typeof(Func<,,>).MakeGenericType(serType, contextType, typeof(Task));
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg, contextParam), arg, contextParam).Compile();
                                getAsync = Activator.CreateInstance(typeof(ContextGetAsyncTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                postAsync = Activator.CreateInstance(typeof(ContextPostAsyncTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;

                            }
                            else
                            {
                                var ft = typeof(Func<,>).MakeGenericType(serType, typeof(Task));
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg), arg).Compile();
                                getAsync = Activator.CreateInstance(typeof(GetAsyncTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                postAsync = Activator.CreateInstance(typeof(PostAsyncTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                            }
                            break;
                        case CallTypes.AsyncValueTask:
                            //  Is async call
                            if (hasContext)
                            {
                                var ft = typeof(Func<,,>).MakeGenericType(serType, contextType, typeof(ValueTask));
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg, contextParam), arg, contextParam).Compile();
                                getAsync = Activator.CreateInstance(typeof(ContextGetAsyncValueTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                postAsync = Activator.CreateInstance(typeof(ContextPostAsyncValueTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;

                            }
                            else
                            {
                                var ft = typeof(Func<,>).MakeGenericType(serType, typeof(ValueTask));
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg), arg).Compile();
                                getAsync = Activator.CreateInstance(typeof(GetAsyncValueTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                postAsync = Activator.CreateInstance(typeof(PostAsyncValueTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                            }
                            break;
                        case CallTypes.Sync:
                            //  Is sync call
                            if (hasContext)
                            {
                                var ft = typeof(Action<,>).MakeGenericType(serType, contextType);
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg, contextParam), arg, contextParam).Compile();
                                get = Activator.CreateInstance(typeof(ContextGetA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                postAsync = Activator.CreateInstance(typeof(ContextPostAsyncA1<>).MakeGenericType(serType), lambda) as IInvokeApi;

                            }
                            else
                            {
                                var ft = typeof(Action<>).MakeGenericType(serType);
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg), arg).Compile();
                                get = Activator.CreateInstance(typeof(GetA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                postAsync = Activator.CreateInstance(typeof(PostAsyncA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                            }
                            break;
                        default:
                            throw new Exception("Invalid call type!");
                    }
                }
                else
                {
                    if (rawMime != null)
                    {
                        //  Return raw data
                        switch (callType)
                        {
                            case CallTypes.AsyncTask:
                                if (taskType == null)
                                    throw new Exception("Internal error!");
                                //  Is async call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,,>).MakeGenericType(serType, contextType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg, contextParam), arg, contextParam).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RawContextRetGetAsyncTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RawContextRetPostAsyncTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(serType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg), arg).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RawRetGetAsyncTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RawRetPostAsyncTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                }
                                break;
                            case CallTypes.AsyncValueTask:
                                if (taskType == null)
                                    throw new Exception("Internal error!");
                                //  Is async call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,,>).MakeGenericType(serType, contextType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg, contextParam), arg, contextParam).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RawContextRetGetAsyncValueTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RawContextRetPostAsyncValueTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(serType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg), arg).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RawRetGetAsyncValueTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RawRetPostAsyncValueTaskA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                }
                                break;
                            case CallTypes.Sync:
                                //  Is sync call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,,>).MakeGenericType(serType, contextType, retSerType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg, contextParam), arg, contextParam).Compile();
                                    get = Activator.CreateInstance(typeof(RawContextRetGetA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RawContextRetPostAsyncA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(serType, retSerType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg), arg).Compile();
                                    get = Activator.CreateInstance(typeof(RawRetGetA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RawRetPostAsyncA1<>).MakeGenericType(serType), lambda) as IInvokeApi;
                                }
                                break;
                            default:
                                throw new Exception("Invalid call type!");
                        }
                    }
                    else
                    {
                        //  Return data
                        switch (callType)
                        {
                            case CallTypes.AsyncTask:
                                if (taskType == null)
                                    throw new Exception("Internal error!");
                                //  Is async call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,,>).MakeGenericType(serType, contextType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg, contextParam), arg, contextParam).Compile();
                                    getAsync = Activator.CreateInstance(typeof(ContextRetGetAsyncTaskA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(ContextRetPostAsyncTaskA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(serType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg), arg).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RetGetAsyncTaskA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RetPostAsyncTaskA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                }
                                break;
                            case CallTypes.AsyncValueTask:
                                if (taskType == null)
                                    throw new Exception("Internal error!");
                                //  Is async call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,,>).MakeGenericType(serType, contextType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg, contextParam), arg, contextParam).Compile();
                                    getAsync = Activator.CreateInstance(typeof(ContextRetGetAsyncValueTaskA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(ContextRetPostAsyncValueTaskA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(serType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg), arg).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RetGetAsyncValueTaskA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RetPostAsyncValueTaskA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                }
                                break;
                            case CallTypes.Sync:
                                //  Is sync call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,,>).MakeGenericType(serType, contextType, retSerType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg, contextParam), arg, contextParam).Compile();
                                    get = Activator.CreateInstance(typeof(ContextRetGetA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(ContextRetPostAsyncA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(serType, retSerType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, arg), arg).Compile();
                                    get = Activator.CreateInstance(typeof(RetGetA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RetPostAsyncA1<,>).MakeGenericType(serType, retSerType), lambda) as IInvokeApi;
                                }
                                break;
                            default:
                                throw new Exception("Invalid call type!");
                        }
                    }
                }
            }
            else
            {
                //  No arguments
                if (retSerType == null)
                {
                    //  No return data
                    switch (callType)
                    {
                        case CallTypes.AsyncTask:
                            //  Is async call
                            if (hasContext)
                            {
                                var ft = typeof(Func<HttpServerRequest, Task>);
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, contextParam), contextParam).Compile();
                                getAsync = Activator.CreateInstance(typeof(ContextGetAsyncTaskA0), lambda) as IInvokeApi;
                                postAsync = Activator.CreateInstance(typeof(ContextPostAsyncTaskA0), lambda) as IInvokeApi;
                            }
                            else
                            {
                                var ft = typeof(Func<Task>);
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method)).Compile();
                                getAsync = Activator.CreateInstance(typeof(GetAsyncTaskA0), lambda) as IInvokeApi;
                                postAsync = Activator.CreateInstance(typeof(PostAsyncTaskA0), lambda) as IInvokeApi;
                            }
                            break;
                        case CallTypes.AsyncValueTask:
                            //  Is async call
                            if (hasContext)
                            {
                                var ft = typeof(Func<HttpServerRequest, ValueTask>);
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, contextParam), contextParam).Compile();
                                getAsync = Activator.CreateInstance(typeof(ContextGetAsyncValueTaskA0), lambda) as IInvokeApi;
                                postAsync = Activator.CreateInstance(typeof(ContextPostAsyncValueTaskA0), lambda) as IInvokeApi;
                            }
                            else
                            {
                                var ft = typeof(Func<ValueTask>);
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method)).Compile();
                                getAsync = Activator.CreateInstance(typeof(GetAsyncValueTaskA0), lambda) as IInvokeApi;
                                postAsync = Activator.CreateInstance(typeof(PostAsyncValueTaskA0), lambda) as IInvokeApi;
                            }
                            break;
                        case CallTypes.Sync:
                            //  Is sync call
                            if (hasContext)
                            {
                                var ft = typeof(Action<HttpServerRequest>);
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, contextParam), contextParam).Compile();
                                get = Activator.CreateInstance(typeof(ContextGetA0), lambda) as IInvokeApi;
                                post = Activator.CreateInstance(typeof(ContextPostA0), lambda) as IInvokeApi;
                            }
                            else
                            {
                                var ft = typeof(Action);
                                var lambda = Expression.Lambda(ft, Expression.Call(objExp, method)).Compile();
                                get = Activator.CreateInstance(typeof(GetA0), lambda) as IInvokeApi;
                                post = Activator.CreateInstance(typeof(PostA0), lambda) as IInvokeApi;
                            }
                            break;
                        default:
                            throw new Exception("Invalid call type!");
                    }
                }
                else
                {
                    if (rawMime != null)
                    {
                        //  Return raw data
                        switch (callType)
                        {
                            case CallTypes.AsyncTask:
                                if (taskType == null)
                                    throw new Exception("Internal error!");
                                //  Is async call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(contextType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, contextParam), contextParam).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RawContextRetGetAsyncTaskA0), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RawContextRetPostAsyncTaskA0), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<>).MakeGenericType(taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method)).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RawRetGetAsyncTaskA0), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RawRetPostAsyncTaskA0), lambda) as IInvokeApi;
                                }
                                break;
                            case CallTypes.AsyncValueTask:
                                if (taskType == null)
                                    throw new Exception("Internal error!");
                                //  Is async call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(contextType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, contextParam), contextParam).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RawContextRetGetAsyncValueTaskA0), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RawContextRetPostAsyncValueTaskA0), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<>).MakeGenericType(taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method)).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RawRetGetAsyncValueTaskA0), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RawRetPostAsyncValueTaskA0), lambda) as IInvokeApi;
                                }
                                break;
                            case CallTypes.Sync:
                                //  Is sync call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(contextType, retSerType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, contextParam), contextParam).Compile();
                                    get = Activator.CreateInstance(typeof(RawContextRetGetA0), lambda) as IInvokeApi;
                                    post = Activator.CreateInstance(typeof(RawContextRetPostA0), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<>).MakeGenericType(retSerType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method)).Compile();
                                    get = Activator.CreateInstance(typeof(RawRetGetA0), lambda) as IInvokeApi;
                                    post = Activator.CreateInstance(typeof(RawRetPostA0), lambda) as IInvokeApi;
                                }
                                break;
                            default:
                                throw new Exception("Invalid call type!");
                        }
                    }
                    else
                    {
                        //  Return data
                        switch (callType)
                        {
                            case CallTypes.AsyncTask:
                                if (taskType == null)
                                    throw new Exception("Internal error!");
                                //  Is async call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(contextType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, contextParam), contextParam).Compile();
                                    getAsync = Activator.CreateInstance(typeof(ContextRetGetAsyncTaskA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(ContextRetPostAsyncTaskA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<>).MakeGenericType(taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method)).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RetGetAsyncTaskA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RetPostAsyncTaskA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                }
                                break;
                            case CallTypes.AsyncValueTask:
                                if (taskType == null)
                                    throw new Exception("Internal error!");
                                //  Is async call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(contextType, taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, contextParam), contextParam).Compile();
                                    getAsync = Activator.CreateInstance(typeof(ContextRetGetAsyncValueTaskA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(ContextRetPostAsyncValueTaskA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<>).MakeGenericType(taskType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method)).Compile();
                                    getAsync = Activator.CreateInstance(typeof(RetGetAsyncValueTaskA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                    postAsync = Activator.CreateInstance(typeof(RetPostAsyncValueTaskA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                }
                                break;
                            case CallTypes.Sync:
                                //  Is sync call
                                if (hasContext)
                                {
                                    var ft = typeof(Func<,>).MakeGenericType(contextType, retSerType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method, contextParam), contextParam).Compile();
                                    get = Activator.CreateInstance(typeof(ContextRetGetA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                    post = Activator.CreateInstance(typeof(ContextRetPostA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                }
                                else
                                {
                                    var ft = typeof(Func<>).MakeGenericType(retSerType);
                                    var lambda = Expression.Lambda(ft, Expression.Call(objExp, method)).Compile();
                                    get = Activator.CreateInstance(typeof(RetGetA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                    post = Activator.CreateInstance(typeof(RetPostA0<>).MakeGenericType(retSerType), lambda) as IInvokeApi;
                                }
                                break;
                            default:
                                throw new Exception("Invalid call type!");
                        }
                    }
                }
            }

            //  Get auth
            String auth = null;
            {
                var aa = method.GetCustomAttribute<WebApiAuthAttribute>(true);
                if (aa != null)
                {
                    auth = aa.Auth;
                }
                else
                {
                    aa = method.DeclaringType.GetCustomAttribute<WebApiAuthAttribute>(true);
                    if (aa != null)
                    {
                        auth = aa.Auth;
                    }
                    else
                    {
                        auth = defaultAuth;
                    }
                }
                var rtAuth = o as IRunTimeWebApiAuth;
                if (rtAuth != null)
                {
                    var ad = rtAuth.MethodAuths;
                    if (ad.TryGetValue(method.Name, out var newA))
                    {
                        auth = newA;
                    }
                    else
                    {
                        if (ad.TryGetValue("*", out newA))
                            auth = newA;
                    }
                }
            }
            //  Get client cache duration
            int clientCacheDuration = 0;
            {
                var aa = method.GetCustomAttribute<WebApiClientCacheAttribute>(true);
                if (aa != null)
                {
                    clientCacheDuration = aa.Duration;
                }
                else
                {
                    aa = method.DeclaringType.GetCustomAttribute<WebApiClientCacheAttribute>(true);
                    if (aa != null)
                        clientCacheDuration = aa.Duration;
                }
            }
            //  Get request cache duration
            int requestCacheDuration = 0;
            {
                var aa = method.GetCustomAttribute<WebApiRequestCacheAttribute>(true);
                if (aa != null)
                {
                    requestCacheDuration = aa.Duration;
                    if (aa.AutoDetectPerSession && hasContext)
                        requestCacheDuration = -requestCacheDuration;
                }
                else
                {
                    aa = method.DeclaringType.GetCustomAttribute<WebApiRequestCacheAttribute>(true);
                    if (aa != null)
                    {
                        requestCacheDuration = aa.Duration;
                        if (aa.AutoDetectPerSession && hasContext)
                            requestCacheDuration = -requestCacheDuration;
                    }
                }
            }

            //  Get compression method overrides
            String compression = requestCacheDuration > 0 ? defaultCachedCompression : defaultCompression;
            {
                var aa = method.GetCustomAttribute<WebApiCompressionAttribute>(true);
                if (aa != null)
                {
                    compression = aa.Compression;
                }
                else
                {
                    aa = method.DeclaringType.GetCustomAttribute<WebApiCompressionAttribute>(true);
                    if (aa != null)
                        compression = aa.Compression;
                }
            }
            //  Disable comrpession for raw data (if disabled)
            if ((rawMime != null) && (!rawCompress))
                compression = null;
            //  Audit
            Func<long, HttpServerRequest, Object, Object> fixAuditParams = null;
            Func<long, HttpServerRequest, Object, Object> fixAuditReturn = null;
            var audit = method.GetCustomAttribute<WebApiAuditAttribute>(true);
            String auditGroup = null;
            if (audit != null)
            {
                auditGroup = audit.Group;
                if (String.IsNullOrEmpty(auditGroup))
                    auditGroup = "Default";
                var ot = o.GetType();
                ParameterExpression valId = Expression.Parameter(typeof(long), "id");
                ParameterExpression valRequest = Expression.Parameter(typeof(HttpServerRequest), "request");
                ParameterExpression valValue = Expression.Parameter(typeof(Object), "value");
                ConstantExpression valInstance = Expression.Constant(o, ot);

                var filterParams = method.GetCustomAttribute<WebApiAuditFilterParamsAttribute>(true)?.MethodName;
                if (filterParams != null)
                    fixAuditParams = BuildFilter(o, filterParams, "params");

                var filterReturn = method.GetCustomAttribute<WebApiAuditFilterReturnAttribute>(true)?.MethodName;
                if (filterReturn != null)
                    fixAuditReturn = BuildFilter(o, filterReturn, "return");

            }
            else
            {
                onStart = null;
                onEnd = null;
                onException = null;
            }


            HttpRateLimiter serviceRateLimiter = null;
            var slimiter = method.GetCustomAttribute<WebApiServiceRateLimitAttribute>(true);
            if (slimiter != null)
            {
                var pp = new HttpRateLimiterParams
                {
                    Count = slimiter.Count,
                    Duration = slimiter.Duration,
                    MaxDelay = slimiter.MaxDelay,
                    MaxQueue = slimiter.MaxQueue,
                };
                pp.Validate();
                serviceRateLimiter = new HttpRateLimiter(pp);
            }
            HttpRateLimiterParams sessionRateLimiter = null;
            var slimiterP = method.GetCustomAttribute<WebApiSessionRateLimitAttribute>(true);
            if (slimiterP != null)
            {
                sessionRateLimiter = new HttpRateLimiterParams
                {
                    Count = slimiterP.Count,
                    Duration = slimiterP.Duration,
                    MaxDelay = slimiterP.MaxDelay,
                    MaxQueue = slimiterP.MaxQueue,
                };
                sessionRateLimiter.Validate();
            }


            var location = String.Concat(locationPrefix, "using mapped to method \"", method.Name, "\" in type \"", method.DeclaringType?.Name, '"');


            var e = new ApiHttpEntry(ioParams, perfMonitor, o, method, get ?? getAsync, post ?? postAsync, rawMime, rawIsTranslated, url, 
                location, auth, pi, ri, serType, retSerType, clientCacheDuration, requestCacheDuration, compression, 
                onStart, onEnd, onException, auditGroup, fixAuditParams, fixAuditReturn,
                serviceRateLimiter, sessionRateLimiter, decoder
                );
            return e;
        }

        /// <summary>
        /// The audit group from <see cref="WebApiAuditAttribute"/> (<c>&quot;Default&quot;</c> if empty), null if the method isn't audited.
        /// </summary>
        public String AuditGroup { get; init; }


        /// <summary>
        /// Not used by API entries (always null), part of <see cref="IHttpRequestHandler"/>.
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        ApiHttpEntry(ApiIoParams ioParams, PerfMonitor mon, Object instance, MethodInfo mi, IInvokeApi getAsync, IInvokeApi postAsync, String mime, bool isTranslatedRaw,
            String url, String location, String auth, ParameterInfo pi, ParameterInfo ri, Type argType, Type retType, int clientCacheDuration, int requestCacheDuration, String compression,
            Action<long, HttpServerRequest, ApiHttpEntry, Object> onStart,
            Action<long, HttpServerRequest, ApiHttpEntry, Object> onEnd,
            Action<long, HttpServerRequest, ApiHttpEntry, Exception> onException,
            String auditGroup,
            Func<long, HttpServerRequest, Object, Object> filterParams, 
            Func<long, HttpServerRequest, Object, Object> filterReturn,
            HttpRateLimiter serviceRateLimiter, HttpRateLimiterParams sessionRateLimiterParams,
            ICompDecoder decoder
            )
        {
            IoParams = ioParams;
            Instance = instance;
            ServiceRateLimiter = serviceRateLimiter;
            SessionRateLimiterParams = sessionRateLimiterParams;
            Mi = mi;
            Mon = mon;
            Location = location;
            Uri = url;
            GetAsync = getAsync;
            PostAsync = postAsync;
            bool isApi = mime == null;
            IsApi = isApi;
            Mime = mime ?? ioParams.DefaultOutput.Mime;
            Auth = Authorization.GetRequiredTokens(auth);
            HaveArgs = argType != null;
            ClientCacheDuration = clientCacheDuration;
            RequestCacheDuration = requestCacheDuration;
            Compression = HttpCompressionPriority.GetSupportedEncoders(compression);
            GetKey = url + " [GET]";
            PostKey = url + " [POST]";
            Pi = pi;
            Ri = ri;
            ArgType = argType;
            RetType = retType;
            OnStart = onStart;
            OnEnd = onEnd;
            OnException = onException;
            if (auditGroup != null)
                AuditGroup = auditGroup;
            FilterAuditParams = filterParams;
            FilterAuditReturn = filterReturn;
            ITypeTranslator tr = null;
            var nt = isApi && (retType != null) && TypeTranslator.TryGetTranslator(retType, out tr);
            NeedTranslation = nt;
            HaveDynamicSourceLanguage = tr?.HaveDynamicSourceLanguage ?? false;
            IsLocalized = nt || isTranslatedRaw;
            if (nt)
                TransExceptions = new ExceptionTracker();
            var lwt = mi.DeclaringType.Assembly.GetLastWriteTimerUtc();
            LastModified = lwt;
            Decoder = decoder;
        }

        /// <summary>
        /// Rate limiter shared by all callers of this end point (from <see cref="WebApiServiceRateLimitAttribute"/>), null if not limited.
        /// </summary>
        public HttpRateLimiter ServiceRateLimiter { get; init; }
        /// <summary>
        /// Parameters for per session rate limiters (from <see cref="WebApiSessionRateLimitAttribute"/>), null if not limited.
        /// </summary>
        readonly HttpRateLimiterParams SessionRateLimiterParams;

        /// <summary>
        /// Get (or lazily create) the rate limiter for this end point in the given session.
        /// </summary>
        /// <param name="session">The session of the caller.</param>
        /// <returns>The limiter, or null if the method doesn't have a <see cref="WebApiSessionRateLimitAttribute"/>.</returns>
        /// <remarks>Limiters are stored in the session under the key <c>&quot;ApiRateLimits&quot;</c>, keyed by <see cref="Uri"/>. Thread safe.</remarks>
        public HttpRateLimiter SessionRateLimiter(HttpSession session)
        {
            var l = SessionRateLimiterParams;
            if (l == null)
                return null;
            var t = session.GetOrCreate("ApiRateLimits", () => new ConcurrentDictionary<String, HttpRateLimiter>(StringComparer.Ordinal));
            var k = Uri;
            if (t.TryGetValue(k, out var limiter))
                return limiter;
            limiter = new HttpRateLimiter(l);
            if (!t.TryAdd(k, limiter))
                limiter = t[k];
            return limiter;
        }

        /// <summary>
        /// True if the result type has a type translator, results are then translated to the session language.
        /// </summary>
        public readonly bool NeedTranslation;
        /// <summary>
        /// True if the result type's translator reads the source language from the value itself.
        /// </summary>
        public readonly bool HaveDynamicSourceLanguage;


        /// <summary>
        /// True if the response depends on the session language (translated result or translated raw output), used to make cache entries language specific.
        /// </summary>
        public bool IsLocalized { get; init; }

        /// <inheritdoc/>
        public void GetDesc(out Type arg, out Type ret, out String methodDesc, out String argDesc, out String retDesc, out String argName)
        {
            arg = ArgType;
            ret = RetType;
            argName = Pi?.Name;
            methodDesc = Mi.XmlDoc()?.Summary;
            argDesc = Pi?.XmlDoc()?.Param;
            retDesc = Ri?.XmlDoc()?.Param;
        }


        /// <summary>
        /// True if the result is serialized, false for raw output (<see cref="WebApiRawAttribute"/>).
        /// </summary>
        public readonly bool IsApi;
        /// <summary>
        /// The exposed method.
        /// </summary>
        public readonly MethodInfo Mi;
        /// <summary>
        /// The input parameter (excluding any request context parameter), null if the method takes no input.
        /// </summary>
        public readonly ParameterInfo Pi;
        /// <summary>
        /// The return parameter, null if the method doesn't return a value.
        /// </summary>
        public readonly ParameterInfo Ri;
        /// <summary>
        /// The type the input is deserialized to, null if the method takes no input.
        /// </summary>
        public readonly Type ArgType;
        /// <summary>
        /// The result type (unwrapped from <see cref="Task{TResult}"/> / <see cref="ValueTask{TResult}"/>), null if the method doesn't return a value.
        /// </summary>
        public readonly Type RetType;
        readonly PerfMonitor Mon;
        readonly String GetKey;
        readonly String PostKey;
        readonly IInvokeApi GetAsync;
        readonly IInvokeApi PostAsync;
        readonly bool HaveArgs;


        /// <summary>
        /// Invoke the method directly, as a POST with the supplied body.
        /// </summary>
        /// <param name="request">The request context, its <see cref="HttpServerRequest.Custom"/> is overwritten with the input. Its headers select the input deserializer, decompression and output serializer.</param>
        /// <param name="data">The serialized input (compressed only if the request has a <c>Content-Encoding</c> header), empty for no input (default value).</param>
        /// <returns>The serialized result, empty for methods without a return value.</returns>
        /// <remarks>No authorization, rate limiting or caching is performed, the caller is responsible for that. Used by AI tools, the API explorer and chart services.</remarks>
        public Task<ReadOnlyMemory<Byte>> InvokeAsync(HttpServerRequest request, ReadOnlyMemory<Byte> data)
        {
            request.Custom = UnmanagedMemory.Create(data);
            return PostAsync.Run(this, request);
        }

        /// <inheritdoc/>
        public HttpServerEndpointTypes Type => HttpServerEndpointTypes.Api;

        /// <summary>
        /// The instance the method is invoked on.
        /// </summary>
        public Object Instance { get; init; }

        /// <summary>
        /// Client cache duration in seconds (from <see cref="WebApiClientCacheAttribute"/>), 0 for no client caching.
        /// </summary>
        public int ClientCacheDuration { get; init; }

        /// <summary>
        /// Server side response cache duration in seconds (from <see cref="WebApiRequestCacheAttribute"/>), 0 for no caching.
        /// A negative value means the cache is per session (duration is the absolute value), otherwise the response is shared by all callers.
        /// </summary>
        public int RequestCacheDuration { get; init; }

        /// <summary>
        /// The supported response encoders in priority order, null if compression is disabled.
        /// </summary>
        public HttpCompressionPriority Compression { get; init; }

        /// <summary>
        /// For raw methods returning pre-compressed data (<see cref="WebApiRawAttribute.PreCompressedWith"/>), the decoder for that compression, else null.
        /// </summary>
        public ICompDecoder Decoder { get; init; }

        /// <summary>
        /// The required auth tokens as returned by <see cref="Authorization.GetRequiredTokens(String)"/>, null if no authorization is required.
        /// </summary>
        public IReadOnlyList<string> Auth { get; init; }


        /// <inheritdoc/>
        public MethodInfo MethodInfo => Mi;

        /// <summary>
        /// The local url of the end point.
        /// </summary>
        public string Uri { get; init; }

        /// <summary>
        /// The http methods accepted by the end point, &quot;GET, POST&quot; (API entries accept both GET and POST).
        /// </summary>
        public string Method { get; init; } = "GET, POST";

        /// <inheritdoc/>
        public string CompPreference => Compression?.ToString();

        /// <summary>
        /// Always null.
        /// </summary>
        public string PreCompressed => null;

        /// <summary>
        /// Human readable description of the method this end point maps to.
        /// </summary>
        public string Location { get; init; }

        /// <summary>
        /// Always -1 (size unknown).
        /// </summary>
        public long? Size => -1;

        /// <summary>
        /// The last write time (UTC) of the assembly declaring the method.
        /// </summary>
        public DateTime LastModified { get; init; }

        /// <summary>
        /// Always null, API responses have no ETag.
        /// </summary>
        public string ETag => null;

        /// <summary>
        /// The response mime type: the raw mime for <see cref="WebApiRawAttribute"/> methods, else the mime of <see cref="ApiIoParams.DefaultOutput"/>.
        /// </summary>
        public string Mime { get; init; }

        /// <summary>
        /// Compute the server cache key for a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>For GET (or methods without input): url and Accept header. For POST with input: the base64 encoded body, local url, Content-Encoding, Content-Type and Accept headers;
        /// <see cref="HttpServerTools.PreventCacheKey"/> if the body is larger than 4096 bytes.</returns>
        /// <remarks>For POST with input the body is read here and stored in <see cref="HttpServerRequest.Custom"/> so the invoker can reuse it (disposed with the request).</remarks>
        public async ValueTask<String> GetCacheKey(HttpServerRequest request)
        {
            var accept = request.GetReqHeader("Accept");
            if ((request.HttpMethod != HttpServerMethods.POST) || (!HaveArgs))
                return String.Join('\r', request.Url, accept);
            var memP = await Input_POST_Read(this, request).ConfigureAwait(false);
            var mem = memP.Memory;
            request.Custom = memP;
            if (mem.Length > 4096)
                return HttpServerTools.PreventCacheKey;
            var ce = request.GetReqHeader("Content-Encoding");
            var ct = request.GetReqHeader("Content-Type");
            return String.Join('\r', Convert.ToBase64String(mem.Span), request.LocalUrl, ce, ct, accept);
        }

        /// <summary>
        /// Sets the response mime type and requests asynchronous handling.
        /// </summary>
        /// <param name="useAsync">Always true.</param>
        /// <param name="request">The request.</param>
        /// <returns>Always null.</returns>
        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            request.SetResMime(Mime);
            useAsync = true;
            return null;
        }


        /// <summary>
        /// Not supported, API entries are always handled asynchronously (see <see cref="GetEtag"/>).
        /// </summary>
        /// <param name="request">Unused.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotImplementedException">Always.</exception>
        public HttpRequestData Get(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Run the GET invoker for GET requests, otherwise the POST invoker, tracking the call in the perf monitor.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response body.</returns>
        async Task<HttpRequestData> IHttpRequestHandler.GetAsync(HttpServerRequest request)
        {
            if (request.HttpMethod == HttpServerMethods.GET)
            {
                using (Mon?.Track(GetKey))
                    return new HttpRequestData(await GetAsync.Run(this, request).ConfigureAwait(false));
            }
            using (Mon?.Track(PostKey))
                return new HttpRequestData(await PostAsync.Run(this, request).ConfigureAwait(false));
        }


        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="root">Unused.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotImplementedException">Always.</exception>
        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="context">Unused.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotImplementedException">Always.</exception>
        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            throw new NotImplementedException();
        }


        enum CallTypes
        {
            Sync,
            AsyncTask,
            AsyncValueTask,

        }

        static readonly IReadOnlyDictionary<Type, CallTypes> NoRetSerTypes = new Dictionary<Type, CallTypes>
        {
            { typeof(void), CallTypes.Sync },
            { typeof(Task), CallTypes.AsyncTask },
            { typeof(ValueTask), CallTypes.AsyncValueTask },
        }.Freeze();

        static readonly IReadOnlyDictionary<Type, CallTypes> GenTaskTypes = new Dictionary<Type, CallTypes>
        { 
            { typeof(Task<>), CallTypes.AsyncTask  },
            { typeof(ValueTask<>), CallTypes.AsyncValueTask },
        }.Freeze();



        static readonly Type ContextType = typeof(HttpServerRequest);
        static readonly ParameterExpression ContextParam = Expression.Parameter(ContextType);

        #region Helpers


        /// <summary>
        /// Called when reading / deserializing the input of an audited API fails (before the audit start was raised).
        /// Raises the audit start with a null value followed by the audit exception, so that the failed attempt is recorded.
        /// Exceptions thrown by the audit handlers are ignored (the original exception is rethrown by the caller).
        /// </summary>
        static void AuditInputFailed(ApiHttpEntry api, HttpServerRequest request, Exception ex)
        {
            var onStart = api.OnStart;
            if (onStart == null)
                return;
            try
            {
                var trackId = ApiAudit.GetId();
                onStart(trackId, request, api, null);
                api.OnException?.Invoke(trackId, request, api, ex);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Deserialize the input from the query string (everything after '?') using <see cref="ApiIoParams"/>, default if there is no query string.
        /// If deserialization fails for an audited API, the failure is audited (see <see cref="AuditInputFailed"/>).
        /// </summary>
        static T Input_GET<T>(ApiHttpEntry api, HttpServerRequest request)
        {
            //  Decode get request
            var qs = request.QueryStringStart;
            if (qs <= 0)
                return default;
            try
            {
                return api.IoParams.Get<T>(request.Url.AsSpan().Slice(qs));
            }
            catch (Exception ex)
            {
                AuditInputFailed(api, request, ex);
                throw;
            }
        }

        static ICompType ThrowDecompress(String ce)
            => throw new Exception(String.Concat("Don't know how to decompress \"", ce, '"'));

        static void ThrowSerializer(String ct)
            => throw new Exception(String.Concat("Don't know how to deserialize using \"", ct, '"'));

        /// <summary>
        /// Read the entire request body into pooled memory (the caller must dispose the result).
        /// </summary>
        /// <exception cref="HttpResponseException">A 413 response if the body exceeds <see cref="ApiIoParams.MaxRequestSize"/>.</exception>
        static Task<IUnmanagedReadOnlyMemory<Byte>> Input_POST_Read(ApiHttpEntry api, HttpServerRequest request)
            => api.IoParams.ReadRequestBodyAsync(request);

        /// <summary>
        /// Deserialize the input from the request body (or from a body previously stored in <see cref="HttpServerRequest.Custom"/>).
        /// The body is decompressed according to Content-Encoding and deserialized using the serializer registered for the Content-Type (or <see cref="ApiIoParams.DefaultInput"/> if no Content-Type).
        /// Returns default for an empty body.
        /// </summary>
        /// <exception cref="Exception">Unknown Content-Encoding or Content-Type.</exception>
        /// <exception cref="HttpResponseException">A 413 response if the body exceeds <see cref="ApiIoParams.MaxRequestSize"/> or decompresses to more than <see cref="ApiIoParams.MaxDecompressedSize"/>.</exception>
        /// <remarks>If reading or deserializing fails for an audited API, the failure is audited (see <see cref="AuditInputFailed"/>).</remarks>
        static async Task<T> Input_POST<T>(ApiHttpEntry api, HttpServerRequest request)
        {
            try
            {
                return await Input_POST_Deserialize<T>(api, request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AuditInputFailed(api, request, ex);
                throw;
            }
        }

        static async Task<T> Input_POST_Deserialize<T>(ApiHttpEntry api, HttpServerRequest request)
        {
            IUnmanagedReadOnlyMemory<Byte> dataMem = null;
            var c = request.Custom;
            if (c != null)
            {
                dataMem = (IUnmanagedReadOnlyMemory<Byte>)c;
                request.Custom = null;
            }
            else
            {
                dataMem = await Input_POST_Read(api, request).ConfigureAwait(false);
            }
            using (dataMem)
            {
                var data = dataMem.Memory;
                if (data.Length <= 0)
                    return default;
                //  Decompress data
                var ce = request.GetReqHeader("Content-Encoding");
                IUnmanagedReadOnlyMemory<Byte> compMem = null;
                if (!String.IsNullOrEmpty(ce))
                {
                    var comp = CompManager.GetFromHttp(ce) ?? ThrowDecompress(ce);
                    compMem = api.IoParams.GetDecompressed(comp, data.Span);
                    data = compMem.Memory;
                }
                using (compMem)
                {
                    //  Find serializer
                    var iop = api.IoParams;
                    var ct = request.GetReqHeader("Content-Type");
                    var deser = iop.DefaultInput;
                    if (!String.IsNullOrEmpty(ct))
                    {
                        ct = ct.FastTrimToLower();
                        if (!iop.InputSerializers.TryGetValue(ct, out deser))
                            ThrowSerializer(ct);
                    }
                    /*
                    var encoding = deser.Encoding;
                    if (encoding != null)
                    {
                        // TODO: Validate that text encoding matches?
                                        var renc = request.ReqTextEncoding;
                                        if (renc.WebName != encoding.WebName)
                                            throw new Exception("Invalid data encoding \"" + renc.WebName + "\", expected \"" + encoding.WebName + "\"");
                        
                    }*/
                    var v = deser.Create<T>(data);
                    return v;
                }
            }
        }
        /*
        static async Task<ReadOnlyMemory<Byte>> Input_POST(ApiHttpEntry api, HttpServerRequest request, ISerializerType ser)
        {
            var custom = request.Custom;
            if (custom != null)
                return (ReadOnlyMemory<Byte>)custom;

            //  Decode get request
            var ce = request.GetReqHeader("Content-Encoding");
            var encoding = ser.Encoding;
            if (encoding != null)
            {
                // TODO: Validate that text is UTF8?
                //                var renc = request.ReqTextEncoding;
                 //               if (renc.WebName != encoding.WebName)
                   //                 throw new Exception("Invalid data encoding \"" + renc.WebName + "\", expected \"" + encoding.WebName + "\"");
                
            }
            if (!String.IsNullOrEmpty(ce))
            {
                //  Compressed
                var comp = CompManager.GetFromHttp(ce);
                if (comp == null)
                    throw new Exception("Don't know how to decompress \"" + ce + "\"!");
                using (var ms = new MemoryStream((int)request.ReqContentLength * 4 + 1024))
                {
                    await comp.DecompressAsync(request.InputStream, ms).ConfigureAwait(false);
                    return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
                }
            }
            else
            {
                //  Uncompressed
                using (var ms = new MemoryStream((int)request.ReqContentLength + 1024))
                {
                    await request.InputStream.CopyToAsync(ms).ConfigureAwait(false);
                    return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
                }
            }
        }

        */
        #endregion//Helpers


    }





}
