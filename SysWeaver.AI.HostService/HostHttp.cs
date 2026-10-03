using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// An error returned to the client using the OpenAI error format
    /// </summary>
    sealed class HostException : Exception
    {
        public HostException(int statusCode, String message, String type = "invalid_request_error", String code = null, String param = null) : base(message)
        {
            StatusCode = statusCode;
            Type = type;
            Code = code;
            Param = param;
        }

        public readonly int StatusCode;
        public readonly String Type;
        public readonly String Code;
        public readonly String Param;

        public static HostException BadRequest(String message, String param = null, String code = null)
            => new HostException(400, message, "invalid_request_error", code, param);
    }

    /// <summary>
    /// A request handler that calls a function to get the response
    /// </summary>
    sealed class HostRequestHandler : IHttpRequestHandler
    {
        public HostRequestHandler(String name, IReadOnlyList<String> auth, Func<HttpServerRequest, Task<HttpRequestData>> get)
        {
            Name = name;
            Auth = auth;
            GetFn = get;
        }

        readonly Func<HttpServerRequest, Task<HttpRequestData>> GetFn;

        public String Name { get; }
        public HttpServerRequest Redirected { get; set; }
        public int ClientCacheDuration => 0;
        public int RequestCacheDuration => 0;
        //  No compression (it would break streaming)
        public HttpCompressionPriority Compression => null;
        public ICompDecoder Decoder => null;
        public IReadOnlyList<String> Auth { get; }
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;

        public String GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = true;
            return null;
        }

        public HttpRequestData Get(HttpServerRequest request) => throw new NotSupportedException();

        public Task<HttpRequestData> GetAsync(HttpServerRequest request) => GetFn(request);
    }

    /// <summary>
    /// Json and server sent events helpers
    /// </summary>
    static class HostHttp
    {
        const String JsonMime = "application/json; charset=utf-8";
        const String EventStreamMime = "text/event-stream; charset=utf-8";

        static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        /// <summary>
        /// Write json to a byte array
        /// </summary>
        public static Byte[] Json(Action<Utf8JsonWriter> write)
        {
            var b = new ArrayBufferWriter<Byte>(1024);
            using (var w = new Utf8JsonWriter(b, WriterOptions))
                write(w);
            return b.WrittenSpan.ToArray();
        }

        /// <summary>
        /// Create a json response
        /// </summary>
        public static HttpRequestData JsonResponse(HttpServerRequest r, int statusCode, Byte[] json)
        {
            r.SetResStatusCode(statusCode);
            r.SetResMime(JsonMime);
            return new HttpRequestData(json, true);
        }

        /// <summary>
        /// Write an error object (OpenAI format)
        /// </summary>
        public static void WriteError(Utf8JsonWriter w, String message, String type, String code, String param)
        {
            w.WriteStartObject("error");
            w.WriteString("message", message);
            w.WriteString("type", type);
            WriteStringOrNull(w, "param", param);
            WriteStringOrNull(w, "code", code);
            w.WriteEndObject();
        }

        /// <summary>
        /// Create an error response (OpenAI format)
        /// </summary>
        public static HttpRequestData ErrorResponse(HttpServerRequest r, HostException ex)
            => JsonResponse(r, ex.StatusCode, Json(w =>
            {
                w.WriteStartObject();
                WriteError(w, ex.Message, ex.Type, ex.Code, ex.Param);
                w.WriteEndObject();
            }));

        /// <summary>
        /// Convert an exception from an AI service to a host exception
        /// </summary>
        public static HostException ToHostException(Exception ex)
        {
            if (ex is HostException he)
                return he;
            if (ex is AiCompletionException ce)
                return new HostException(ce.StatusCode, ce.Message, ce.StatusCode >= 500 ? "server_error" : "invalid_request_error", ce.Code);
            if (ex is OperationCanceledException)
                return new HostException(499, "The request was cancelled", "server_error", "cancelled");
            return new HostException(500, ex.Message, "server_error");
        }

        public static void WriteStringOrNull(Utf8JsonWriter w, String name, String value)
        {
            if (value == null)
                w.WriteNull(name);
            else
                w.WriteString(name, value);
        }

        /// <summary>
        /// Read a json request body
        /// </summary>
        public static async Task<JsonDocument> ReadJson(HttpServerRequest r, int maxSize)
        {
            var len = r.ReqContentLength;
            if (len > maxSize)
                throw new HostException(413, "The request body is too large", "invalid_request_error", "request_too_large");
            using var ms = new MemoryStream(len > 0 ? (int)len : 4096);
            var input = r.InputStream;
            if (input != null)
            {
                var buf = new Byte[65536];
                for (; ; )
                {
                    var read = await input.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
                    if (read <= 0)
                        break;
                    if ((ms.Length + read) > maxSize)
                        throw new HostException(413, "The request body is too large", "invalid_request_error", "request_too_large");
                    ms.Write(buf, 0, read);
                }
            }
            if (ms.Length <= 0)
                throw HostException.BadRequest("The request body is empty, expected a json object");
            try
            {
                var d = JsonDocument.Parse(ms.GetBuffer().AsMemory(0, (int)ms.Length));
                if (d.RootElement.ValueKind != JsonValueKind.Object)
                {
                    d.Dispose();
                    throw HostException.BadRequest("The request body must be a json object");
                }
                return d;
            }
            catch (JsonException ex)
            {
                throw HostException.BadRequest("The request body isn't valid json: " + ex.Message);
            }
        }

        /// <summary>
        /// Create a streaming (server sent events) response, the producer runs in the background
        /// </summary>
        /// <param name="r">The request</param>
        /// <param name="producer">The function that writes the events</param>
        /// <param name="onError">Called if the producer throws an exception (the stream is still open)</param>
        /// <returns>The response data</returns>
        public static HttpRequestData EventStream(HttpServerRequest r, Func<SseWriter, Task> producer, Func<SseWriter, Exception, Task> onError)
        {
            var pipe = new Pipe();
            r.SetResStatusCode(200);
            r.SetResMime(EventStreamMime);
            r.SetResHeader("X-Accel-Buffering", "no");
            var sse = new SseWriter(pipe.Writer);
            _ = Task.Run(async () =>
            {
                try
                {
                    try
                    {
                        await producer(sse).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (!sse.IsClosed)
                            await onError(sse, ex).ConfigureAwait(false);
                    }
                }
                catch
                {
                }
                finally
                {
                    await pipe.Writer.CompleteAsync().ConfigureAwait(false);
                }
            });
            return new HttpRequestData(pipe.Reader.AsStream());
        }
    }

    /// <summary>
    /// Writes server sent events
    /// </summary>
    sealed class SseWriter
    {
        public SseWriter(PipeWriter w)
        {
            W = w;
        }

        readonly PipeWriter W;

        /// <summary>
        /// True if the client is gone
        /// </summary>
        public bool IsClosed { get; private set; }

        static readonly Byte[] DataPrefix = Encoding.UTF8.GetBytes("data: ");
        static readonly Byte[] EventPrefix = Encoding.UTF8.GetBytes("event: ");
        static readonly Byte[] NewLine = Encoding.UTF8.GetBytes("\n");
        static readonly Byte[] End = Encoding.UTF8.GetBytes("\n\n");
        static readonly Byte[] DoneData = Encoding.UTF8.GetBytes("[DONE]");

        async Task Flush()
        {
            var r = await W.FlushAsync().ConfigureAwait(false);
            if (r.IsCompleted || r.IsCanceled)
                IsClosed = true;
        }

        /// <summary>
        /// Write a data only event
        /// </summary>
        public Task Data(Byte[] json)
        {
            if (IsClosed)
                return Task.CompletedTask;
            W.Write(DataPrefix);
            W.Write(json);
            W.Write(End);
            return Flush();
        }

        /// <summary>
        /// Write a named event
        /// </summary>
        public Task Event(String name, Byte[] json)
        {
            if (IsClosed)
                return Task.CompletedTask;
            W.Write(EventPrefix);
            W.Write(Encoding.UTF8.GetBytes(name));
            W.Write(NewLine);
            W.Write(DataPrefix);
            W.Write(json);
            W.Write(End);
            return Flush();
        }

        /// <summary>
        /// Write the "[DONE]" data event
        /// </summary>
        public Task Done() => Data(DoneData);
    }
}
