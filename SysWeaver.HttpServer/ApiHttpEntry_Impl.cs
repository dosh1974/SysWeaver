using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Serialization;
using SysWeaver.Translation;

namespace SysWeaver.Net
{
    public sealed partial class ApiHttpEntry 
    {

        /// <summary>
        /// Tracks exceptions thrown while translating results, only created when <see cref="NeedTranslation"/> is true.
        /// </summary>
        readonly ExceptionTracker TransExceptions;

        /// <summary>
        /// Serialize a method result for the response.
        /// </summary>
        /// <typeparam name="T">The declared result type.</typeparam>
        /// <param name="api">The end point that produced the value.</param>
        /// <param name="request">The request, the serializer is picked from its Accept header. If null the <see cref="ApiIoParams.DefaultOutput"/> serializer is used and no translation is performed.</param>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The serialized bytes.</returns>
        /// <remarks>
        /// When the result type needs translation and the request has a translator, the value is deep copied (round tripped through
        /// <see cref="ApiIoParams.CopySerializer"/>) and the copy is translated to the session language, so the original instance is never modified.
        /// Translation failures are recorded in <see cref="TransExceptions"/> and the untranslated value is returned instead.
        /// </remarks>
        static async Task<ReadOnlyMemory<Byte>> EncodeResult<T>(ApiHttpEntry api, HttpServerRequest request, T value)
        {
            var io = api.IoParams;
            if (request == null) 
                return io.DefaultOutput.Serialize(value);
            var ser = io.GetSerializer(request.GetReqHeader("Accept"));
            if (!api.NeedTranslation)
                return ser.Serialize(value);
            var t = request.Translator;
            if (t != null)
            {
                var lang = request.Session.Language;
                //if (api.HaveDynamicSourceLanguage || (!lang.FastEquals("en")))
                {
                    try
                    {
                        var copySer = io.CopySerializer;
                        var copy = copySer.Create<T>(copySer.Serialize(value));
                        copy = await TypeTranslator.TranslateValue(t, lang, copy).ConfigureAwait(false);
                        return ser.Serialize(copy);
                    }
                    catch (Exception ex)
                    {
                        api.TransExceptions.OnException(ex);
                    }
                }
            }
            return ser.Serialize(value);
        }


        /// <summary>
        /// A compiled invoker for one HTTP method (GET or POST) of an API method signature.
        /// </summary>
        interface IInvokeApi
        {
            /// <summary>
            /// Read the input from the request, invoke the API method and return the encoded response body.
            /// </summary>
            /// <param name="api">The end point being invoked (supplies io params and audit callbacks).</param>
            /// <param name="request">The request to read input from and pass to context aware methods.</param>
            /// <returns>The response body, empty for methods without a return value.</returns>
            Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request);
        }

        #region One argument

        #region Return value

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method, used for Get requests. The argument is deserialized from the query string (default when absent). The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetGetAsyncTaskA1<T, R> : IInvokeApi
        {
            public RetGetAsyncTaskA1(Func<T, Task<R>> f)
            {
                F = f;

            }
            readonly Func<T, Task<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }




        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetPostAsyncTaskA1<T, R> : IInvokeApi
        {
            public RetPostAsyncTaskA1(Func<T, Task<R>> f)
            {
                F = f;

            }
            readonly Func<T, Task<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method, used for Get requests. The argument is deserialized from the query string (default when absent). The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetGetA1<T, R> : IInvokeApi
        {
            public RetGetA1(Func<T, R> f)
            {
                F = f;

            }
            readonly Func<T, R> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = F(io);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }



        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetPostAsyncA1<T, R> : IInvokeApi
        {
            public RetPostAsyncA1(Func<T, R> f)
            {
                F = f;

            }
            readonly Func<T, R> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = F(io);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return value


        #region Return raw value

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method, used for Get requests. The argument is deserialized from the query string (default when absent). The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetGetAsyncTaskA1<T> : IInvokeApi
        {
            public RawRetGetAsyncTaskA1(Func<T, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, Task<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetPostAsyncTaskA1<T> : IInvokeApi
        {
            public RawRetPostAsyncTaskA1(Func<T, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, Task<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method, used for Get requests. The argument is deserialized from the query string (default when absent). The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetGetA1<T> : IInvokeApi
        {
            public RawRetGetA1(Func<T, ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<T, ReadOnlyMemory<Byte>> F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = F(io);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetPostAsyncA1<T> : IInvokeApi
        {
            public RawRetPostAsyncA1(Func<T, ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<T, ReadOnlyMemory<Byte>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = F(io);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return raw value


        #region No return

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method, used for Get requests. The argument is deserialized from the query string (default when absent). Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class GetAsyncTaskA1<T> : IInvokeApi
        {
            public GetAsyncTaskA1(Func<T, Task> f)
            {
                F = f;

            }
            readonly Func<T, Task> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    await F(io).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }

            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class PostAsyncTaskA1<T> : IInvokeApi
        {
            public PostAsyncTaskA1(Func<T, Task> f)
            {
                F = f;

            }
            readonly Func<T, Task> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    await F(io).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method, used for Get requests. The argument is deserialized from the query string (default when absent). Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class GetA1<T> : IInvokeApi
        {
            public GetA1(Action<T> f)
            {
                F = f;

            }
            readonly Action<T> F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    F(io);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class PostAsyncA1<T> : IInvokeApi
        {
            public PostAsyncA1(Action<T> f)
            {
                F = f;

            }
            readonly Action<T> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    F(io);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }


        #endregion//No return

        #endregion//One argument

        #region No argument

        #region Return value

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method, used for Get requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetGetAsyncTaskA0<R> : IInvokeApi
        {
            public RetGetAsyncTaskA0(Func<Task<R>> f)
            {
                F = f;

            }
            readonly Func<Task<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F().ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method, used for Post requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetPostAsyncTaskA0<R> : IInvokeApi
        {
            public RetPostAsyncTaskA0(Func<Task<R>> f)
            {
                F = f;

            }
            readonly Func<Task<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F().ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method, used for Get requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetGetA0<R> : IInvokeApi
        {
            public RetGetA0(Func<R> f)
            {
                F = f;

            }
            readonly Func<R> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = F();
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method, used for Post requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetPostA0<R> : IInvokeApi
        {
            public RetPostA0(Func<R> f)
            {
                F = f;

            }
            readonly Func<R> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = F();
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return value

        #region Return raw value

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method, used for Get requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetGetAsyncTaskA0 : IInvokeApi
        {
            public RawRetGetAsyncTaskA0(Func<Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<Task<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F().ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method, used for Post requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetPostAsyncTaskA0 : IInvokeApi
        {
            public RawRetPostAsyncTaskA0(Func<Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<Task<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F().ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method, used for Get requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetGetA0 : IInvokeApi
        {
            public RawRetGetA0(Func<ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<ReadOnlyMemory<Byte>> F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = F();
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method, used for Post requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetPostA0 : IInvokeApi
        {
            public RawRetPostA0(Func<ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<ReadOnlyMemory<Byte>> F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = F();
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return raw value

        #region No return

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method, used for Get requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class GetAsyncTaskA0 : IInvokeApi
        {
            public GetAsyncTaskA0(Func<Task> f)
            {
                F = f;

            }
            readonly Func<Task> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    await F().ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }

            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method, used for Post requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class PostAsyncTaskA0 : IInvokeApi
        {
            public PostAsyncTaskA0(Func<Task> f)
            {
                F = f;

            }
            readonly Func<Task> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    await F().ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method, used for Get requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class GetA0 : IInvokeApi
        {
            public GetA0(Action f)
            {
                F = f;

            }
            readonly Action F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    F();
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method, used for Post requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class PostA0 : IInvokeApi
        {
            public PostA0(Action f)
            {
                F = f;

            }
            readonly Action F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    F();
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }


        #endregion//No return

        #endregion//No argument

        #region With request context

        #region One argument

        #region Return value

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The argument is deserialized from the query string (default when absent). The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetGetAsyncTaskA1<T, R> : IInvokeApi
        {
            public ContextRetGetAsyncTaskA1(Func<T, HttpServerRequest, Task<R>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io, request).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }

            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetPostAsyncTaskA1<T, R> : IInvokeApi
        {
            public ContextRetPostAsyncTaskA1(Func<T, HttpServerRequest, Task<R>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io, request).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The argument is deserialized from the query string (default when absent). The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetGetA1<T, R> : IInvokeApi
        {
            public ContextRetGetA1(Func<T, HttpServerRequest, R> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, R> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = F(io, request);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetPostAsyncA1<T, R> : IInvokeApi
        {
            public ContextRetPostAsyncA1(Func<T, HttpServerRequest, R> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, R> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = F(io, request);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return value


        #region Return raw value

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The argument is deserialized from the query string (default when absent). The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetGetAsyncTaskA1<T> : IInvokeApi
        {
            public RawContextRetGetAsyncTaskA1(Func<T, HttpServerRequest, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io, request).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetPostAsyncTaskA1<T> : IInvokeApi
        {
            public RawContextRetPostAsyncTaskA1(Func<T, HttpServerRequest, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io, request).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The argument is deserialized from the query string (default when absent). The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetGetA1<T> : IInvokeApi
        {
            public RawContextRetGetA1(Func<T, HttpServerRequest, ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, ReadOnlyMemory<Byte>> F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = F(io, request);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetPostAsyncA1<T> : IInvokeApi
        {
            public RawContextRetPostAsyncA1(Func<T, HttpServerRequest, ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, ReadOnlyMemory<Byte>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = F(io, request);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return raw value





        #region No return

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The argument is deserialized from the query string (default when absent). Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextGetAsyncTaskA1<T> : IInvokeApi
        {
            public ContextGetAsyncTaskA1(Func<T, HttpServerRequest, Task> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    await F(io, request).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }

            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextPostAsyncTaskA1<T> : IInvokeApi
        {
            public ContextPostAsyncTaskA1(Func<T, HttpServerRequest, Task> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    await F(io, request).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The argument is deserialized from the query string (default when absent). Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextGetA1<T> : IInvokeApi
        {
            public ContextGetA1(Action<T, HttpServerRequest> f)
            {
                F = f;

            }
            readonly Action<T, HttpServerRequest> F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    F(io, request);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextPostAsyncA1<T> : IInvokeApi
        {
            public ContextPostAsyncA1(Action<T, HttpServerRequest> f)
            {
                F = f;

            }
            readonly Action<T, HttpServerRequest> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    F(io, request);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }


        #endregion//No return

        #endregion//One argument

        #region No argument

        #region Return value

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetGetAsyncTaskA0<R> : IInvokeApi
        {
            public ContextRetGetAsyncTaskA0(Func<HttpServerRequest, Task<R>> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, Task<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F(request).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetPostAsyncTaskA0<R> : IInvokeApi
        {
            public ContextRetPostAsyncTaskA0(Func<HttpServerRequest, Task<R>> f)
            {
                F = f;
            }

            readonly Func<HttpServerRequest, Task<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F(request).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetGetA0<R> : IInvokeApi
        {
            public ContextRetGetA0(Func<HttpServerRequest, R> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, R> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = F(request);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetPostA0<R> : IInvokeApi
        {
            public ContextRetPostA0(Func<HttpServerRequest, R> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, R> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = F(request);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return value

        #region Return raw value

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetGetAsyncTaskA0 : IInvokeApi
        {
            public RawContextRetGetAsyncTaskA0(Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F(request).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetPostAsyncTaskA0 : IInvokeApi
        {
            public RawContextRetPostAsyncTaskA0(Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;
            }

            readonly Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F(request).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetGetA0 : IInvokeApi
        {
            public RawContextRetGetA0(Func<HttpServerRequest, ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, ReadOnlyMemory<Byte>> F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = F(request);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetPostA0 : IInvokeApi
        {
            public RawContextRetPostA0(Func<HttpServerRequest, ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, ReadOnlyMemory<Byte>> F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = F(request);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return value

        #region No return

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextGetAsyncTaskA0 : IInvokeApi
        {
            public ContextGetAsyncTaskA0(Func<HttpServerRequest, Task> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, Task> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    await F(request).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="Task"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextPostAsyncTaskA0 : IInvokeApi
        {
            public ContextPostAsyncTaskA0(Func<HttpServerRequest, Task> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, Task> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    await F(request).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextGetA0 : IInvokeApi
        {
            public ContextGetA0(Action<HttpServerRequest> f)
            {
                F = f;

            }
            readonly Action<HttpServerRequest> F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    F(request);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextPostA0 : IInvokeApi
        {
            public ContextPostA0(Action<HttpServerRequest> f)
            {
                F = f;

            }
            readonly Action<HttpServerRequest> F;

            public Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    F(request);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return Task.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }


        #endregion//No return

        #endregion//No argument

        #endregion//With request context









        #region ValueTask

        #region One argument

        #region Return value

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method, used for Get requests. The argument is deserialized from the query string (default when absent). The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetGetAsyncValueTaskA1<T, R> : IInvokeApi
        {
            public RetGetAsyncValueTaskA1(Func<T, ValueTask<R>> f)
            {
                F = f;

            }
            readonly Func<T, ValueTask<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }




        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetPostAsyncValueTaskA1<T, R> : IInvokeApi
        {
            public RetPostAsyncValueTaskA1(Func<T, ValueTask<R>> f)
            {
                F = f;

            }
            readonly Func<T, ValueTask<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }
          
        #endregion//Return value


        #region Return raw value

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method, used for Get requests. The argument is deserialized from the query string (default when absent). The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetGetAsyncValueTaskA1<T> : IInvokeApi
        {
            public RawRetGetAsyncValueTaskA1(Func<T, ValueTask<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, ValueTask<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetPostAsyncValueTaskA1<T> : IInvokeApi
        {
            public RawRetPostAsyncValueTaskA1(Func<T, ValueTask<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, ValueTask<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return raw value


        #region No return

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method, used for Get requests. The argument is deserialized from the query string (default when absent). Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class GetAsyncValueTaskA1<T> : IInvokeApi
        {
            public GetAsyncValueTaskA1(Func<T, ValueTask> f)
            {
                F = f;

            }
            readonly Func<T, ValueTask> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    await F(io).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }

            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class PostAsyncValueTaskA1<T> : IInvokeApi
        {
            public PostAsyncValueTaskA1(Func<T, ValueTask> f)
            {
                F = f;

            }
            readonly Func<T, ValueTask> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    await F(io).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//No return

        #endregion//One argument

        #region No argument

        #region Return value

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method, used for Get requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetGetAsyncValueTaskA0<R> : IInvokeApi
        {
            public RetGetAsyncValueTaskA0(Func<ValueTask<R>> f)
            {
                F = f;

            }
            readonly Func<ValueTask<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F().ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method, used for Post requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetPostAsyncValueTaskA0<R> : IInvokeApi
        {
            public RetPostAsyncValueTaskA0(Func<ValueTask<R>> f)
            {
                F = f;

            }
            readonly Func<ValueTask<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F().ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return value

        #region Return raw value

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method, used for Get requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetGetAsyncValueTaskA0 : IInvokeApi
        {
            public RawRetGetAsyncValueTaskA0(Func<ValueTask<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<ValueTask<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F().ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method, used for Post requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawRetPostAsyncValueTaskA0 : IInvokeApi
        {
            public RawRetPostAsyncValueTaskA0(Func<ValueTask<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<ValueTask<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F().ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return raw value

        #region No return

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method, used for Get requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class GetAsyncValueTaskA0 : IInvokeApi
        {
            public GetAsyncValueTaskA0(Func<ValueTask> f)
            {
                F = f;

            }
            readonly Func<ValueTask> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    await F().ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }

            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method, used for Post requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class PostAsyncValueTaskA0 : IInvokeApi
        {
            public PostAsyncValueTaskA0(Func<ValueTask> f)
            {
                F = f;

            }
            readonly Func<ValueTask> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    await F().ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//No return

        #endregion//No argument

        #region With request context

        #region One argument

        #region Return value

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The argument is deserialized from the query string (default when absent). The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetGetAsyncValueTaskA1<T, R> : IInvokeApi
        {
            public ContextRetGetAsyncValueTaskA1(Func<T, HttpServerRequest, ValueTask<R>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, ValueTask<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io, request).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }

            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetPostAsyncValueTaskA1<T, R> : IInvokeApi
        {
            public ContextRetPostAsyncValueTaskA1(Func<T, HttpServerRequest, ValueTask<R>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, ValueTask<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io, request).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return value


        #region Return raw value

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The argument is deserialized from the query string (default when absent). The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetGetAsyncValueTaskA1<T> : IInvokeApi
        {
            public RawContextRetGetAsyncValueTaskA1(Func<T, HttpServerRequest, ValueTask<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, ValueTask<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io, request).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetPostAsyncValueTaskA1<T> : IInvokeApi
        {
            public RawContextRetPostAsyncValueTaskA1(Func<T, HttpServerRequest, ValueTask<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, ValueTask<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = await F(io, request).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return raw value





        #region No return

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The argument is deserialized from the query string (default when absent). Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextGetAsyncValueTaskA1<T> : IInvokeApi
        {
            public ContextGetAsyncValueTaskA1(Func<T, HttpServerRequest, ValueTask> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, ValueTask> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = Input_GET<T>(api, request);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    await F(io, request).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }

            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextPostAsyncValueTaskA1<T> : IInvokeApi
        {
            public ContextPostAsyncValueTaskA1(Func<T, HttpServerRequest, ValueTask> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, ValueTask> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var io = await Input_POST<T>(api, request).ConfigureAwait(false);
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    await F(io, request).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//No return

        #endregion//One argument

        #region No argument

        #region Return value

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetGetAsyncValueTaskA0<R> : IInvokeApi
        {
            public ContextRetGetAsyncValueTaskA0(Func<HttpServerRequest, ValueTask<R>> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, ValueTask<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F(request).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetPostAsyncValueTaskA0<R> : IInvokeApi
        {
            public ContextRetPostAsyncValueTaskA0(Func<HttpServerRequest, ValueTask<R>> f)
            {
                F = f;
            }

            readonly Func<HttpServerRequest, ValueTask<R>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F(request).ConfigureAwait(false);
                    var od = await EncodeResult(api, request, oo).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return od;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return value

        #region Return raw value

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetGetAsyncValueTaskA0 : IInvokeApi
        {
            public RawContextRetGetAsyncValueTaskA0(Func<HttpServerRequest, ValueTask<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, ValueTask<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F(request).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The returned raw bytes are sent as-is. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RawContextRetPostAsyncValueTaskA0 : IInvokeApi
        {
            public RawContextRetPostAsyncValueTaskA0(Func<HttpServerRequest, ValueTask<ReadOnlyMemory<Byte>>> f)
            {
                F = f;
            }

            readonly Func<HttpServerRequest, ValueTask<ReadOnlyMemory<Byte>>> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    var oo = await F(request).ConfigureAwait(false);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//Return value

        #region No return

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Get requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextGetAsyncValueTaskA0 : IInvokeApi
        {
            public ContextGetAsyncValueTaskA0(Func<HttpServerRequest, ValueTask> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, ValueTask> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    await F(request).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoker for a <see cref="ValueTask"/>-returning parameterless API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. Returns an empty response. Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextPostAsyncValueTaskA0 : IInvokeApi
        {
            public ContextPostAsyncValueTaskA0(Func<HttpServerRequest, ValueTask> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, ValueTask> F;

            public async Task<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    await F(request).ConfigureAwait(false);
                    var oo = ReadOnlyMemory<Byte>.Empty;
                    if (track)
                        api.OnEnd(trackId, request, api, null);
                    return oo;
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        #endregion//No return

        #endregion//No argument

        #endregion//With request context

        #endregion//ValueTask
    }
}
