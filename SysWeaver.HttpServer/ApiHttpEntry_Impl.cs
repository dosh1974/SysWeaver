using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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
        /// <remarks>
        /// Only translated results are encoded asynchronously, the others complete synchronously (no task allocation).
        /// The callers await the result inside their try block, so an exception thrown synchronously is handled like a faulted task.
        /// </remarks>
        static ValueTask<ReadOnlyMemory<Byte>> EncodeResult<T>(ApiHttpEntry api, HttpServerRequest request, T value)
        {
            var io = api.IoParams;
            if (request == null)
                return new ValueTask<ReadOnlyMemory<Byte>>(io.DefaultOutput.Serialize(value));
            String accept;
            if (request.ApiAcceptRead)
            {
                //  Read by GetCacheKey, used once
                accept = request.ApiAccept;
                request.ApiAccept = null;
                request.ApiAcceptRead = false;
            }else
            {
                accept = request.GetReqHeader("Accept");
            }
            var ser = io.GetSerializer(accept);
            if (!api.NeedTranslation)
                return new ValueTask<ReadOnlyMemory<Byte>>(ser.Serialize(value));
            var t = request.Translator;
            if (t == null)
                return new ValueTask<ReadOnlyMemory<Byte>>(ser.Serialize(value));
            return EncodeTranslatedResult(api, request, value, ser, t);
        }

        /// <summary>
        /// The translating part of <see cref="EncodeResult{T}(ApiHttpEntry, HttpServerRequest, T)"/>.
        /// </summary>
        /// <remarks>A pooled box when the translation is pending (the invokers await it once).</remarks>
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<ReadOnlyMemory<Byte>> EncodeTranslatedResult<T>(ApiHttpEntry api, HttpServerRequest request, T value, ISerializer ser, ITranslator t)
        {
            var io = api.IoParams;
            var lang = request.Session.Language;
            //if (api.HaveDynamicSourceLanguage || (!lang.FastEquals("en")))
            {
                try
                {
                    //  Same as copySer.Create<T>(copySer.Serialize(value)), the json is written to a pooled buffer
                    var copy = io.CopySerializer.DeepCopy(value);
                    copy = await TypeTranslator.TranslateValue(t, lang, copy).ConfigureAwait(false);
                    return ser.Serialize(copy);
                }
                catch (Exception ex)
                {
                    api.TransExceptions.OnException(ex);
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
            /// <returns>The response body, empty for methods without a return value (a value task: synchronous API's complete without a task allocation).</returns>
            /// <remarks>The async invokers use pooled boxes (<see cref="PoolingAsyncValueTaskMethodBuilder{TResult}"/>): the value task must be awaited (consumed) exactly once.</remarks>
            ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request);
        }

        #region Synchronous invoker helpers

        /// <summary>
        /// The end of a synchronous invoker (called inside its try block, after the method returned): encode the result and raise the audit end.
        /// A result that is encoded synchronously (no translation) is returned without a state machine, a pending encoding is awaited by <see cref="RunEncodeAwait{R}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ValueTask<ReadOnlyMemory<Byte>> RunEnd<R>(ApiHttpEntry api, HttpServerRequest request, bool track, long trackId, R oo)
        {
            var t = EncodeResult(api, request, oo);
            if (!t.IsCompletedSuccessfully)
                return RunEncodeAwait(api, request, track, trackId, oo, t);
            var od = t.Result;
            if (track)
                api.OnEnd(trackId, request, api, oo);
            return new ValueTask<ReadOnlyMemory<Byte>>(od);
        }

        /// <summary>
        /// Await a pending (or faulted) encoding of a synchronous invoker, then raise the audit end (the audit exception on failure), the same as the rest of the async invoker.
        /// </summary>
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<ReadOnlyMemory<Byte>> RunEncodeAwait<R>(ApiHttpEntry api, HttpServerRequest request, bool track, long trackId, R oo, ValueTask<ReadOnlyMemory<Byte>> t)
        {
            try
            {
                var od = await t.ConfigureAwait(false);
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

        /// <summary>
        /// Return a value task for an exception thrown by a synchronous invoker, after raising the audit exception (if tracked).
        /// The same states as the async invoker (faulted, canceled for an <see cref="OperationCanceledException"/>), an exception thrown by the audit callback faults the task instead (as before).
        /// </summary>
#pragma warning disable CS1998 // Async method lacks 'await' operators (the async method builder creates the task)
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<ReadOnlyMemory<Byte>> RunThrow(ApiHttpEntry api, HttpServerRequest request, bool track, long trackId, ExceptionDispatchInfo ex)
        {
            if (track)
                api.OnException(trackId, request, api, ex.SourceException);
            ex.Throw();
            return default;
        }
#pragma warning restore CS1998

        /// <summary>
        /// A synchronous POST invoker: the part after the input was read.
        /// </summary>
        interface IInvokePostRest<T>
        {
            /// <summary>
            /// Invoke the API method with the input and return the encoded response body (the same as the rest of the async invoker after the input was awaited).
            /// Never throws synchronously, exceptions are returned as faulted / canceled value tasks.
            /// </summary>
            ValueTask<ReadOnlyMemory<Byte>> RunRest(ApiHttpEntry api, HttpServerRequest request, T io);
        }

        /// <summary>
        /// Run a synchronous POST invoker: an input that is read and deserialized synchronously (the usual case) is passed on without a state machine,
        /// a pending input is awaited by <see cref="RunPostAwait{T, I}"/>, an input exception is returned as a faulted / canceled value task (the same as the async invoker).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ValueTask<ReadOnlyMemory<Byte>> RunPost<T, I>(I invoker, ApiHttpEntry api, HttpServerRequest request) where I : class, IInvokePostRest<T>
        {
            ValueTask<T> input;
            try
            {
                input = Input_POST<T>(api, request);
            }
            catch (Exception ex)
            {
                return RunThrow(api, request, false, 0, ExceptionDispatchInfo.Capture(ex));
            }
            if (!input.IsCompletedSuccessfully)
                return RunPostAwait<T, I>(invoker, api, request, input);
            return invoker.RunRest(api, request, input.Result);
        }

        /// <summary>
        /// Await a pending (or failed) input of a synchronous POST invoker, then run the rest of the invoker.
        /// </summary>
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<ReadOnlyMemory<Byte>> RunPostAwait<T, I>(I invoker, ApiHttpEntry api, HttpServerRequest request, ValueTask<T> input) where I : class, IInvokePostRest<T>
        {
            var io = await input.ConfigureAwait(false);
            return await invoker.RunRest(api, request, io).ConfigureAwait(false);
        }

        #endregion//Synchronous invoker helpers

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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            /// <remarks>Not async: a synchronous encoding completes without a state machine, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                T io;
                try
                {
                    io = Input_GET<T>(api, request);
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, false, 0, ExceptionDispatchInfo.Capture(ex));
                }
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    return RunEnd(api, request, track, trackId, F(io));
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
                }
            }



        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class RetPostAsyncA1<T, R> : IInvokeApi, IInvokePostRest<T>
        {
            public RetPostAsyncA1(Func<T, R> f)
            {
                F = f;

            }
            readonly Func<T, R> F;

            /// <remarks>Not async when the input completed: a synchronous encoding completes without a state machine, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
                => RunPost<T, RetPostAsyncA1<T, R>>(this, api, request);

            public ValueTask<ReadOnlyMemory<Byte>> RunRest(ApiHttpEntry api, HttpServerRequest request, T io)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    return RunEnd(api, request, track, trackId, F(io));
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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
        sealed class RawRetPostAsyncA1<T> : IInvokeApi, IInvokePostRest<T>
        {
            public RawRetPostAsyncA1(Func<T, ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<T, ReadOnlyMemory<Byte>> F;

            /// <remarks>Not async when the input completed, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
                => RunPost<T, RawRetPostAsyncA1<T>>(this, api, request);

            public ValueTask<ReadOnlyMemory<Byte>> RunRest(ApiHttpEntry api, HttpServerRequest request, T io)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = F(io);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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
        sealed class PostAsyncA1<T> : IInvokeApi, IInvokePostRest<T>
        {
            public PostAsyncA1(Action<T> f)
            {
                F = f;

            }
            readonly Action<T> F;

            /// <remarks>Not async when the input completed, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
                => RunPost<T, PostAsyncA1<T>>(this, api, request);

            public ValueTask<ReadOnlyMemory<Byte>> RunRest(ApiHttpEntry api, HttpServerRequest request, T io)
            {
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            /// <remarks>Not async: a synchronous encoding completes without a state machine, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    return RunEnd(api, request, track, trackId, F());
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
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

            /// <remarks>Not async: a synchronous encoding completes without a state machine, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    return RunEnd(api, request, track, trackId, F());
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            /// <remarks>Not async: a synchronous encoding completes without a state machine, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                T io;
                try
                {
                    io = Input_GET<T>(api, request);
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, false, 0, ExceptionDispatchInfo.Capture(ex));
                }
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    return RunEnd(api, request, track, trackId, F(io, request));
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
                }
            }
        }

        /// <summary>
        /// Invoker for a synchronous one-argument API method that also receives the <see cref="HttpServerRequest"/>, used for Post requests. The argument is deserialized from the (optionally compressed) request body, default when empty. The result is serialized according to the Accept header (and translated when needed). Audit callbacks are invoked when configured; input deserialization happens before the audit start (a deserialization failure is audited as a start with a null value followed by the exception).
        /// </summary>
        sealed class ContextRetPostAsyncA1<T, R> : IInvokeApi, IInvokePostRest<T>
        {
            public ContextRetPostAsyncA1(Func<T, HttpServerRequest, R> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, R> F;

            /// <remarks>Not async when the input completed: a synchronous encoding completes without a state machine, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
                => RunPost<T, ContextRetPostAsyncA1<T, R>>(this, api, request);

            public ValueTask<ReadOnlyMemory<Byte>> RunRest(ApiHttpEntry api, HttpServerRequest request, T io)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    return RunEnd(api, request, track, trackId, F(io, request));
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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
        sealed class RawContextRetPostAsyncA1<T> : IInvokeApi, IInvokePostRest<T>
        {
            public RawContextRetPostAsyncA1(Func<T, HttpServerRequest, ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, ReadOnlyMemory<Byte>> F;

            /// <remarks>Not async when the input completed, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
                => RunPost<T, RawContextRetPostAsyncA1<T>>(this, api, request);

            public ValueTask<ReadOnlyMemory<Byte>> RunRest(ApiHttpEntry api, HttpServerRequest request, T io)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, io);
                    var oo = F(io, request);
                    if (track)
                        api.OnEnd(trackId, request, api, oo);
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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
        sealed class ContextPostAsyncA1<T> : IInvokeApi, IInvokePostRest<T>
        {
            public ContextPostAsyncA1(Action<T, HttpServerRequest> f)
            {
                F = f;

            }
            readonly Action<T, HttpServerRequest> F;

            /// <remarks>Not async when the input completed, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
                => RunPost<T, ContextPostAsyncA1<T>>(this, api, request);

            public ValueTask<ReadOnlyMemory<Byte>> RunRest(ApiHttpEntry api, HttpServerRequest request, T io)
            {
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            /// <remarks>Not async: a synchronous encoding completes without a state machine, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    return RunEnd(api, request, track, trackId, F(request));
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
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

            /// <remarks>Not async: a synchronous encoding completes without a state machine, exceptions are returned as faulted / canceled value tasks (the same as the async invoker).</remarks>
            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
            {
                var track = api?.OnStart != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                try
                {
                    if (track)
                        api.OnStart(trackId, request, api, null);
                    return RunEnd(api, request, track, trackId, F(request));
                }
                catch (Exception ex)
                {
                    return RunThrow(api, request, track, trackId, ExceptionDispatchInfo.Capture(ex));
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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

            public ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
                    return new ValueTask<ReadOnlyMemory<Byte>>(oo);
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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
