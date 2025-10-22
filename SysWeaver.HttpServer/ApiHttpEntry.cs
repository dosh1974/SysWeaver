using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;


using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Docs;
using SysWeaver.MicroService;
using SysWeaver.Translation;

namespace SysWeaver.Net
{




    public sealed partial class ApiHttpEntry : IHttpRequestHandler, IApiHttpServerEndPoint
    {
        
        public const String DefaultAuth = null;
        public const String DefaultCachedCompression = "br:Best, deflate:Best, gzip:Best";
        public const String DefaultCompression = "br:Balanced, deflate:Balanced, gzip:Balanced";
        public const String DefaultLocationPrefix = "[API] ";



        internal readonly Func<long, HttpServerRequest, Object, Object> FilterAuditParams;
        internal readonly Func<long, HttpServerRequest, Object, Object> FilterAuditReturn;

        internal readonly Action<long, HttpServerRequest, ApiHttpEntry, Object> OnStart;
        internal readonly Action<long, HttpServerRequest, ApiHttpEntry, Object> OnEnd;
        internal readonly Action<long, HttpServerRequest, ApiHttpEntry, Exception> OnException;

        static readonly ParameterExpression ValId = Expression.Parameter(typeof(long), "id");
        static readonly ParameterExpression ValRequest = Expression.Parameter(typeof(HttpServerRequest), "request");
        static readonly ParameterExpression ValValue = Expression.Parameter(typeof(Object), "value");


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

        public readonly ApiIoParams IoParams;

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
            if (serType == null)
                if (pl > 1)
                    serType = typeof(Object[]);
            //  Determine return type and if it's a task
            ParameterInfo ri = method.ReturnParameter;
            Type retSerType = method.ReturnType;
            if (NoRetSerTypes.TryGetValue(retSerType, out var isTask))
            {
                retSerType = null;
                ri = null;
            }
            Type taskType = null;
            if (retSerType != null)
            {
                if (retSerType.IsGenericType)
                {
                    if (GenTaskTypes.Contains(retSerType.GetGenericTypeDefinition()))
                    {
                        taskType = retSerType;
                        isTask = true;
                        retSerType = retSerType.GetGenericArguments()[0];
                    }
                }
            }
            //  Check if we have some raw output instead of serializable content.
            String rawMime = null;
            bool rawCompress = false;
            bool rawIsTranslated = false;
            if (typeof(ReadOnlyMemory<byte>).IsAssignableFrom(retSerType))
            {
                var raw = method.GetCustomAttribute<WebApiRawAttribute>(true);
                if (raw != null)
                {
                    rawMime = raw.Mime;
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
                    if (isTask)
                    {
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
                    }
                    else
                    {
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
                    }
                }
                else
                {
                    if (rawMime != null)
                    {
                        //  Return raw data
                        if (isTask)
                        {
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
                        }
                        else
                        {
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
                        }

                    }
                    else
                    {
                        //  Return data
                        if (isTask)
                        {
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
                        }
                        else
                        {
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
                    if (isTask)
                    {
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
                    }
                    else
                    {
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
                    }
                }
                else
                {
                    if (rawMime != null)
                    {
                        //  Return raw data
                        if (isTask)
                        {
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
                        }
                        else
                        {
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
                        }
                    }
                    else
                    {
                        //  Return data
                        if (isTask)
                        {
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
                        }
                        else
                        {
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
                serviceRateLimiter, sessionRateLimiter
                );
            return e;
        }

        public String AuditGroup { get; init; }


        /// <summary>
        /// Ignore, used internally
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
            HttpRateLimiter serviceRateLimiter, HttpRateLimiterParams sessionRateLimiterParams
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
        }

        public HttpRateLimiter ServiceRateLimiter { get; init; }
        readonly HttpRateLimiterParams SessionRateLimiterParams;

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

        public readonly bool NeedTranslation;
        public readonly bool HaveDynamicSourceLanguage;


        public bool IsLocalized { get; init; }

        public void GetDesc(out Type arg, out Type ret, out String methodDesc, out String argDesc, out String retDesc, out String argName)
        {
            arg = ArgType;
            ret = RetType;
            argName = Pi?.Name;
            methodDesc = Mi.XmlDoc()?.Summary;
            argDesc = Pi?.XmlDoc()?.Param;
            retDesc = Ri?.XmlDoc()?.Param;
        }


        public readonly bool IsApi;
        public readonly MethodInfo Mi;
        public readonly ParameterInfo Pi;
        public readonly ParameterInfo Ri;
        public readonly Type ArgType;
        public readonly Type RetType;
        readonly PerfMonitor Mon;
        readonly String GetKey;
        readonly String PostKey;
        readonly IInvokeApi GetAsync;
        readonly IInvokeApi PostAsync;
        readonly bool HaveArgs;


        public ValueTask<ReadOnlyMemory<Byte>> InvokeAsync(HttpServerRequest request, ReadOnlyMemory<Byte> data)
        {
            request.Custom = data;
            return PostAsync.Run(this, request);
        }

        public HttpServerEndpointTypes Type => HttpServerEndpointTypes.Api;

        public Object Instance { get; private set; }

        public int ClientCacheDuration { get; private set; }

        public int RequestCacheDuration { get; private set; }

        public bool UseStream => false;

        public HttpCompressionPriority Compression { get; private set; }

        public ICompDecoder Decoder => null;

        public IReadOnlyList<string> Auth { get; private set; }


        public MethodInfo MethodInfo => Mi;

        public string Uri { get; private set; }

        public string Method { get; private set; }

        public string CompPreference => Compression?.ToString();

        public string PreCompressed => null;

        public string Location { get; private set; }

        public long? Size => -1;

        public DateTime LastModified => HttpServerTools.StartedTime;

        public string Mime { get; private set; }

        public async ValueTask<String> GetCacheKey(HttpServerRequest request)
        {
            var accept = request.GetReqHeader("Accept");
            if ((request.HttpMethod != HttpServerMethods.POST) || (!HaveArgs))
                return String.Join('\r', request.Url, accept);
            var mem = await Input_POST_Read(this, request).ConfigureAwait(false);
            request.Custom = mem;
            if (mem.Length > 4096)
                return HttpServerTools.PreventCacheKey;
            var ce = request.GetReqHeader("Content-Encoding");
            var ct = request.GetReqHeader("Content-Type");
            return String.Join('\r', Convert.ToBase64String(mem.Span), request.LocalUrl, ce, ct, accept);
        }

        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = true;
            return null;
        }

        public ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public async Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest request)
        {
            request.SetResMime(Mime);
            if (request.HttpMethod == HttpServerMethods.GET)
            {
                using (Mon?.Track(GetKey))
                    return await GetAsync.Run(this, request).ConfigureAwait(false);
            }
            using (Mon?.Track(PostKey))
                return await PostAsync.Run(this, request).ConfigureAwait(false);
        }

        public Stream GetStream(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetStreamAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null)
        {
            throw new NotImplementedException();
        }

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            throw new NotImplementedException();
        }


        static readonly IReadOnlyDictionary<Type, bool> NoRetSerTypes = new Dictionary<Type, bool>
        {
            { typeof(void), false },
            { typeof(Task), true },
            { typeof(ValueTask), true },
        }.Freeze();

        static readonly IReadOnlySet<Type> GenTaskTypes = ReadOnlyData.Set(
            typeof(Task<>),
            typeof(ValueTask<>)
        );



        static readonly Type ContextType = typeof(HttpServerRequest);
        static readonly ParameterExpression ContextParam = Expression.Parameter(ContextType);

        #region Helpers

        static T Input_GET<T>(ApiHttpEntry api, HttpServerRequest request)
        {
            //  Decode get request
            var qs = request.QueryStringStart;
            if (qs <= 0)
                return default;
            return api.IoParams.Get<T>(request.Url.Substring(qs));
        }


        static async ValueTask<ReadOnlyMemory<Byte>> Input_POST_Read(ApiHttpEntry api, HttpServerRequest request)
        {
            using var ms = new MemoryStream((int)request.ReqContentLength + 1024);
            await request.InputStream.CopyToAsync(ms).ConfigureAwait(false);
            return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }


        static async ValueTask<T> Input_POST<T>(ApiHttpEntry api, HttpServerRequest request)
        {
            ReadOnlyMemory<Byte> data = null;
            var c = request.Custom;
            if (c != null)
            {
                data = (ReadOnlyMemory<Byte>)c;
            }else
            {
                using var ms = new MemoryStream((int)request.ReqContentLength + 1024);
                await request.InputStream.CopyToAsync(ms).ConfigureAwait(false);
                data = new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
            if (data.Length <= 0)
                return default(T);
            //  Decompress data
            var ce = request.GetReqHeader("Content-Encoding");
            if (!String.IsNullOrEmpty(ce))
            {
                var comp = CompManager.GetFromHttp(ce);
                if (comp == null)
                    throw new Exception("Don't know how to decompress \"" + ce + "\"!");
                data = comp.GetDecompressed(data.Span);
            }
            //  Find serializer
            var iop = api.IoParams;
            var ct = request.GetReqHeader("Content-Type");
            var deser = iop.DefaultInput;
            if (!String.IsNullOrEmpty(ct))
            {
                ct = ct.Trim().FastToLower();
                if (!iop.InputSerializers.TryGetValue(ct, out deser))
                    throw new Exception(String.Concat("Don't know how to deserialize using \"", ct, '"'));
            }
            var encoding = deser.Encoding;
            if (encoding != null)
            {
                // TODO: Validate that text encoding matches?
                /*                var renc = request.ReqTextEncoding;
                                if (renc.WebName != encoding.WebName)
                                    throw new Exception("Invalid data encoding \"" + renc.WebName + "\", expected \"" + encoding.WebName + "\"");
                */
            }
            var v = deser.Create<T>(data);
            return v;
        }
        /*
        static async ValueTask<ReadOnlyMemory<Byte>> Input_POST(ApiHttpEntry api, HttpServerRequest request, ISerializerType ser)
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
