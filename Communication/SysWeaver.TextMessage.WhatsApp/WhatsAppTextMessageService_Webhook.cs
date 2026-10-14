using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.MicroService;
using SysWeaver.Net;
using SysWeaver.TextMessage.WhatsAppApi;

namespace SysWeaver.TextMessage
{

    public sealed partial class WhatsAppTextMessageService
    {
        readonly String WebhookUrl;
        readonly String VerifyToken;
        readonly Byte[] AppSecret;
        readonly IHttpRequestHandler VerifyHandler;
        readonly IHttpRequestHandler PostHandler;

        const int MaxRequestSize = 4 << 20;
        const String IndexSuffix = "/index.html";

        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
        };

        /// <summary>
        /// Message ids that have been received (Meta may deliver the same notification more than once).
        /// Key = message id (wamid), Value = time received.
        /// </summary>
        readonly ConcurrentDictionary<String, DateTime> SeenIncoming = new(StringComparer.Ordinal);

        #region Http module

        public String[] OnlyForPrefixes { get; }

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var url = context.LocalUrl.AsSpan();
            //  The server adds "index.html" to urls ending with a '/'
            if (context.DidIndex && url.EndsWith(IndexSuffix, StringComparison.Ordinal))
                url = url.Slice(0, url.Length - IndexSuffix.Length);
            if (!url.TrimEnd('/').Equals(WebhookUrl, StringComparison.Ordinal))
                return null;
            return context.HttpMethod switch
            {
                HttpServerMethods.GET => VerifyHandler,
                HttpServerMethods.POST => PostHandler,
                _ => null,
            };
        }

        static HttpRequestData Response(HttpServerRequest r, int statusCode, String text = null)
        {
            r.SetResStatusCode(statusCode);
            r.SetResMime("text/plain; charset=utf-8");
            return new HttpRequestData(text == null ? ReadOnlyMemory<Byte>.Empty : Encoding.UTF8.GetBytes(text), true);
        }

        static bool FixedTimeEquals(String a, String b)
            => (a != null) && (b != null) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

        /// <summary>
        /// GET, sent by Meta when the webhook is configured: "?hub.mode=subscribe&amp;hub.verify_token=[token]&amp;hub.challenge=[challenge]", the challenge must be returned
        /// </summary>
        Task<HttpRequestData> Verify(HttpServerRequest r)
        {
            var q = r.QueryParameters;
            var mode = q["hub.mode"];
            var token = q["hub.verify_token"];
            var challenge = q["hub.challenge"];
            if ((mode != "subscribe") || (challenge == null) || !FixedTimeEquals(token, VerifyToken))
            {
                Msg?.AddMessage(LogPrefix + "Webhook verification failed, check that the verify token in the Meta app matches the " + nameof(WhatsAppTextMessageParams.VerifyToken) + " parameter", MessageLevels.Warning);
                return Task.FromResult(Response(r, 403));
            }
            Msg?.AddMessage(LogPrefix + "Webhook verified", MessageLevels.Info);
            return Task.FromResult(Response(r, 200, challenge));
        }

        static async Task<Byte[]> ReadBody(HttpServerRequest r)
        {
            var len = r.ReqContentLength;
            if (len > MaxRequestSize)
                return null;
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
                    if ((ms.Length + read) > MaxRequestSize)
                        return null;
                    ms.Write(buf, 0, read);
                }
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Check the "X-Hub-Signature-256: sha256=[hex]" header, a HMAC-SHA256 of the body using the app secret
        /// </summary>
        bool IsSignatureValid(HttpServerRequest r, Byte[] body)
        {
            var secret = AppSecret;
            if (secret == null)
                return false;
            var sig = r.GetReqHeader("X-Hub-Signature-256");
            if ((sig == null) || !sig.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
                return false;
            Byte[] given;
            try
            {
                given = Convert.FromHexString(sig.AsSpan(7));
            }
            catch
            {
                return false;
            }
            var expected = HMACSHA256.HashData(secret, body);
            return CryptographicOperations.FixedTimeEquals(expected, given);
        }

        /// <summary>
        /// POST, a notification from Meta (incoming messages and status updates).
        /// Must respond with 200 quickly, else Meta retries (with decreasing frequency for up to 7 days).
        /// </summary>
        async Task<HttpRequestData> Notify(HttpServerRequest r)
        {
            using var _ = PerfMon.Track(nameof(Notify));
            var body = await ReadBody(r).ConfigureAwait(false);
            if (body == null)
                return Response(r, 413);
            if (!IsSignatureValid(r, body))
            {
                WebhookFails.OnException(new Exception("Invalid signature"));
                return Response(r, 401);
            }
            WaWebhook hook;
            try
            {
                hook = JsonSerializer.Deserialize<WaWebhook>(body, JsonOptions);
            }
            catch (Exception ex)
            {
                WebhookFails.OnException(ex);
                return Response(r, 400);
            }
            try
            {
                Purge();
                Process(hook);
            }
            catch (Exception ex)
            {
                //  Don't make Meta retry, it would most likely fail again
                WebhookFails.OnException(ex);
                Msg?.AddMessage(LogPrefix + "Failed to process a webhook notification", ex, MessageLevels.Warning);
            }
            return Response(r, 200);
        }

        #endregion//Http module

        #region Processing

        void Process(WaWebhook hook)
        {
            foreach (var e in hook?.entry ?? [])
            {
                foreach (var c in e?.changes ?? [])
                {
                    if (c?.field != "messages")
                        continue;
                    var v = c.value;
                    if (v == null)
                        continue;
                    var numberId = v.metadata?.phone_number_id;
                    Number n = null;
                    if (numberId != null)
                        NumbersById.TryGetValue(numberId, out n);
                    foreach (var m in v.messages ?? [])
                        HandleIncoming(n, v, m);
                    foreach (var s in v.statuses ?? [])
                        HandleStatus(s);
                }
            }
        }

        static String GetText(WaIncomingMessage m)
        {
            switch (m.type)
            {
                case "text":
                    return m.text?.body;
                case "button":
                    return m.button?.text;
                case "interactive":
                    return m.interactive?.button_reply?.title ?? m.interactive?.list_reply?.title;
                case "image":
                    return m.image?.caption ?? "[image]";
                case "video":
                    return m.video?.caption ?? "[video]";
                case "document":
                    return m.document?.caption ?? m.document?.filename ?? "[document]";
            }
            return String.Concat('[', m.type, ']');
        }

        static DateTime GetTime(String unixSeconds)
            => long.TryParse(unixSeconds, out var s) ? DateTime.UnixEpoch.AddSeconds(s) : DateTime.UtcNow;

        void HandleIncoming(Number n, WaValue v, WaIncomingMessage m)
        {
            var id = m?.id;
            var from = m?.from;
            if ((id == null) || (from == null))
                return;
            if (!SeenIncoming.TryAdd(id, DateTime.UtcNow))
                return;
            var phone = "+" + from;
            String name = null;
            String isoCountry = null;
            try
            {
                PhonePrefix.GetValidatedPhoneNumber(out name, out isoCountry, out var pre, out var num, phone);
                phone = String.Join(' ', pre, num);
            }
            catch
            {
            }
            var numberId = v.metadata?.phone_number_id;
            var rec = new WhatsAppTextMessage
            {
                Incoming = true,
                Id = id,
                Time = GetTime(m.timestamp),
                PhoneNumber = phone,
                Name = name,
                Country = isoCountry,
                Text = GetText(m),
                System = n?.System,
                ResponseToken = String.Concat(numberId, '/', id),
                Channel = Channel,
                To = n?.Display ?? v.metadata?.display_phone_number,
                SenderName = v.contacts?.FirstOrDefault(x => x?.wa_id == from)?.profile?.name,
                Status = "received",
            };
            //  The user is obviously on WhatsApp and the customer service window is open
            NotOnWhatsApp.TryRemove(from, out var _);
            if (numberId != null)
                LastIncoming[WindowKey(numberId, from)] = DateTime.UtcNow;
            AddRecord(rec);
            PublishIncoming(rec.Clone());
            if (MarkAsRead && (n != null))
                _ = MarkRead(n, id);
        }

        static TextMessageStatuses GetStatus(String s) => s switch
        {
            "sent" => TextMessageStatuses.Sent,
            "delivered" => TextMessageStatuses.Delivered,
            "read" => TextMessageStatuses.Read,
            "failed" => TextMessageStatuses.Failed,
            _ => TextMessageStatuses.Unknown,
        };

        void HandleStatus(WaStatus s)
        {
            var id = s?.id;
            if (id == null)
                return;
            var status = GetStatus(s.status);
            if (status == TextMessageStatuses.Unknown)
                return;
            //  Statuses may arrive out of order, don't go back from delivered to sent
            PendingOut.TryGetValue(id, out var p);
            var rec = p?.Record;
            if ((rec != null) && (status == TextMessageStatuses.Sent) && (rec.Status == "delivered"))
                return;
            var u = new TextMessageStatusUpdate
            {
                Id = NewId("Status"),
                MessageId = id,
                Reference = p?.Message.Reference,
                System = p?.Message.System,
                PhoneNumber = rec?.PhoneNumber ?? (s.recipient_id == null ? null : "+" + s.recipient_id),
                Channel = Channel,
                Status = status,
                Time = GetTime(s.timestamp),
            };
            if (status == TextMessageStatuses.Failed)
            {
                var err = s.errors?.FirstOrDefault();
                u.Failure = err == null ? TextMessageFailures.Other : WhatsAppApiException.GetFailure(err.code);
                u.ErrorCode = err?.code.ToString();
                u.Error = err?.ToString();
                if ((err?.code == WhatsAppApiException.Undeliverable) && (s.recipient_id != null))
                    SetNotOnWhatsApp(s.recipient_id);
                if (rec != null)
                    rec.Status = "failed " + err;
            }
            else if (rec != null)
            {
                rec.Status = s.status;
            }
            if (status.IsFinal())
                PendingOut.TryRemove(id, out var _);
            PublishStatus(u);
        }

        #endregion//Processing

        sealed class WebhookRequestHandler : IHttpRequestHandler
        {
            public WebhookRequestHandler(String name, Func<HttpServerRequest, Task<HttpRequestData>> get)
            {
                Name = name;
                GetFn = get;
            }

            readonly Func<HttpServerRequest, Task<HttpRequestData>> GetFn;

            public String Name { get; }
            public HttpServerRequest Redirected { get; set; }
            public int ClientCacheDuration => 0;
            public int RequestCacheDuration => 0;
            public HttpCompressionPriority Compression => null;
            public ICompDecoder Decoder => null;
            //  Called by Meta, authenticated using the signature
            public IReadOnlyList<String> Auth => null;
            public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;

            public String GetEtag(out bool useAsync, HttpServerRequest request)
            {
                useAsync = true;
                return null;
            }

            public HttpRequestData Get(HttpServerRequest request) => throw new NotSupportedException();

            public ValueTask<HttpRequestData> GetAsync(HttpServerRequest request) => new ValueTask<HttpRequestData>(GetFn(request));
        }
    }
}
