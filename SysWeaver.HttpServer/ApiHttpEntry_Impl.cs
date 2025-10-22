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

        readonly ExceptionTracker TransExceptions;

        static async ValueTask<ReadOnlyMemory<Byte>> EncodeResult<T>(ApiHttpEntry api, HttpServerRequest request, T value)
        {
            var io = api.IoParams;
            if (request == null) 
                return io.DefaultOutput.Serialize(value);
            var ser = api.IoParams.GetSerializer(request.GetReqHeader("Accept"));
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
                        await TypeTranslator.Translate(t, lang, copy).ConfigureAwait(false);
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


        interface IInvokeApi
        {
            ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request);
        }

        #region One argument

        #region Return value

        sealed class RetGetAsyncTaskA1<T, R> : IInvokeApi
        {
            public RetGetAsyncTaskA1(Func<T, Task<R>> f)
            {
                F = f;

            }
            readonly Func<T, Task<R>> F;

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

        sealed class RetPostAsyncTaskA1<T, R> : IInvokeApi
        {
            public RetPostAsyncTaskA1(Func<T, Task<R>> f)
            {
                F = f;

            }
            readonly Func<T, Task<R>> F;

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

        sealed class RetGetA1<T, R> : IInvokeApi
        {
            public RetGetA1(Func<T, R> f)
            {
                F = f;

            }
            readonly Func<T, R> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class RetPostAsyncA1<T, R> : IInvokeApi
        {
            public RetPostAsyncA1(Func<T, R> f)
            {
                F = f;

            }
            readonly Func<T, R> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class RawRetGetAsyncTaskA1<T> : IInvokeApi
        {
            public RawRetGetAsyncTaskA1(Func<T, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, Task<ReadOnlyMemory<Byte>>> F;

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

        sealed class RawRetPostAsyncTaskA1<T> : IInvokeApi
        {
            public RawRetPostAsyncTaskA1(Func<T, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, Task<ReadOnlyMemory<Byte>>> F;

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
                    return ValueTask.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        sealed class RawRetPostAsyncA1<T> : IInvokeApi
        {
            public RawRetPostAsyncA1(Func<T, ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<T, ReadOnlyMemory<Byte>> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class GetAsyncTaskA1<T> : IInvokeApi
        {
            public GetAsyncTaskA1(Func<T, Task> f)
            {
                F = f;

            }
            readonly Func<T, Task> F;

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

        sealed class PostAsyncTaskA1<T> : IInvokeApi
        {
            public PostAsyncTaskA1(Func<T, Task> f)
            {
                F = f;

            }
            readonly Func<T, Task> F;

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
                    return ValueTask.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        sealed class PostAsyncA1<T> : IInvokeApi
        {
            public PostAsyncA1(Action<T> f)
            {
                F = f;

            }
            readonly Action<T> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class RetGetAsyncTaskA0<R> : IInvokeApi
        {
            public RetGetAsyncTaskA0(Func<Task<R>> f)
            {
                F = f;

            }
            readonly Func<Task<R>> F;

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

        sealed class RetPostAsyncTaskA0<R> : IInvokeApi
        {
            public RetPostAsyncTaskA0(Func<Task<R>> f)
            {
                F = f;

            }
            readonly Func<Task<R>> F;

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

        sealed class RetGetA0<R> : IInvokeApi
        {
            public RetGetA0(Func<R> f)
            {
                F = f;

            }
            readonly Func<R> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class RetPostA0<R> : IInvokeApi
        {
            public RetPostA0(Func<R> f)
            {
                F = f;

            }
            readonly Func<R> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class RawRetGetAsyncTaskA0 : IInvokeApi
        {
            public RawRetGetAsyncTaskA0(Func<Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<Task<ReadOnlyMemory<Byte>>> F;

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

        sealed class RawRetPostAsyncTaskA0 : IInvokeApi
        {
            public RawRetPostAsyncTaskA0(Func<Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<Task<ReadOnlyMemory<Byte>>> F;

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
                    return ValueTask.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

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
                    return ValueTask.FromResult(oo);
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

        sealed class GetAsyncTaskA0 : IInvokeApi
        {
            public GetAsyncTaskA0(Func<Task> f)
            {
                F = f;

            }
            readonly Func<Task> F;

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

        sealed class PostAsyncTaskA0 : IInvokeApi
        {
            public PostAsyncTaskA0(Func<Task> f)
            {
                F = f;

            }
            readonly Func<Task> F;

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
                    return ValueTask.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

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
                    return ValueTask.FromResult(oo);
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

        sealed class ContextRetGetAsyncTaskA1<T, R> : IInvokeApi
        {
            public ContextRetGetAsyncTaskA1(Func<T, HttpServerRequest, Task<R>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task<R>> F;

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

        sealed class ContextRetPostAsyncTaskA1<T, R> : IInvokeApi
        {
            public ContextRetPostAsyncTaskA1(Func<T, HttpServerRequest, Task<R>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task<R>> F;

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

        sealed class ContextRetGetA1<T, R> : IInvokeApi
        {
            public ContextRetGetA1(Func<T, HttpServerRequest, R> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, R> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class ContextRetPostAsyncA1<T, R> : IInvokeApi
        {
            public ContextRetPostAsyncA1(Func<T, HttpServerRequest, R> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, R> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class RawContextRetGetAsyncTaskA1<T> : IInvokeApi
        {
            public RawContextRetGetAsyncTaskA1(Func<T, HttpServerRequest, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task<ReadOnlyMemory<Byte>>> F;

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

        sealed class RawContextRetPostAsyncTaskA1<T> : IInvokeApi
        {
            public RawContextRetPostAsyncTaskA1(Func<T, HttpServerRequest, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task<ReadOnlyMemory<Byte>>> F;

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
                    return ValueTask.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        sealed class RawContextRetPostAsyncA1<T> : IInvokeApi
        {
            public RawContextRetPostAsyncA1(Func<T, HttpServerRequest, ReadOnlyMemory<Byte>> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, ReadOnlyMemory<Byte>> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class ContextGetAsyncTaskA1<T> : IInvokeApi
        {
            public ContextGetAsyncTaskA1(Func<T, HttpServerRequest, Task> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task> F;

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

        sealed class ContextPostAsyncTaskA1<T> : IInvokeApi
        {
            public ContextPostAsyncTaskA1(Func<T, HttpServerRequest, Task> f)
            {
                F = f;

            }
            readonly Func<T, HttpServerRequest, Task> F;

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
                    return ValueTask.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

        sealed class ContextPostAsyncA1<T> : IInvokeApi
        {
            public ContextPostAsyncA1(Action<T, HttpServerRequest> f)
            {
                F = f;

            }
            readonly Action<T, HttpServerRequest> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class ContextRetGetAsyncTaskA0<R> : IInvokeApi
        {
            public ContextRetGetAsyncTaskA0(Func<HttpServerRequest, Task<R>> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, Task<R>> F;

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

        sealed class ContextRetPostAsyncTaskA0<R> : IInvokeApi
        {
            public ContextRetPostAsyncTaskA0(Func<HttpServerRequest, Task<R>> f)
            {
                F = f;
            }

            readonly Func<HttpServerRequest, Task<R>> F;

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

        sealed class ContextRetGetA0<R> : IInvokeApi
        {
            public ContextRetGetA0(Func<HttpServerRequest, R> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, R> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class ContextRetPostA0<R> : IInvokeApi
        {
            public ContextRetPostA0(Func<HttpServerRequest, R> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, R> F;

            public async ValueTask<ReadOnlyMemory<Byte>> Run(ApiHttpEntry api, HttpServerRequest request)
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

        sealed class RawContextRetGetAsyncTaskA0 : IInvokeApi
        {
            public RawContextRetGetAsyncTaskA0(Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> F;

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

        sealed class RawContextRetPostAsyncTaskA0 : IInvokeApi
        {
            public RawContextRetPostAsyncTaskA0(Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> f)
            {
                F = f;
            }

            readonly Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> F;

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
                    return ValueTask.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

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
                    return ValueTask.FromResult(oo);
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

        sealed class ContextGetAsyncTaskA0 : IInvokeApi
        {
            public ContextGetAsyncTaskA0(Func<HttpServerRequest, Task> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, Task> F;

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

        sealed class ContextPostAsyncTaskA0 : IInvokeApi
        {
            public ContextPostAsyncTaskA0(Func<HttpServerRequest, Task> f)
            {
                F = f;

            }
            readonly Func<HttpServerRequest, Task> F;

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
                    return ValueTask.FromResult(oo);
                }
                catch (Exception ex)
                {
                    if (track)
                        api.OnException(trackId, request, api, ex);
                    throw;
                }
            }
        }

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
                    return ValueTask.FromResult(oo);
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

    }
}
