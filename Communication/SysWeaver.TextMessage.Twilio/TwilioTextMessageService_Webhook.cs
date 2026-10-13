using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Compression;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.TextMessage
{

    public sealed partial class TwilioTextMessageService
    {
        readonly String WebhookUrl;
        readonly String IncomingUrl;
        readonly String StatusUrl;
        readonly String PublicUrl;
        readonly IHttpRequestHandler IncomingHandler;
        readonly IHttpRequestHandler StatusHandler;

        const int MaxRequestSize = 1 << 20;
        const String IndexSuffix = "/index.html";
        const String EmptyTwiml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response></Response>";

        /// <summary>
        /// Message sids that have been received (Twilio may post the same message more than once).
        /// </summary>
        readonly ConcurrentDictionary<String, DateTime> SeenIncoming = new(StringComparer.Ordinal);

        #region Http module

        public String[] OnlyForPrefixes { get; }

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            if (context.HttpMethod != HttpServerMethods.POST)
                return null;
            var url = context.LocalUrl.AsSpan();
            //  The server adds "index.html" to urls ending with a '/'
            if (context.DidIndex && url.EndsWith(IndexSuffix, StringComparison.Ordinal))
                url = url.Slice(0, url.Length - IndexSuffix.Length);
            url = url.TrimEnd('/');
            if (url.Equals(IncomingUrl, StringComparison.Ordinal))
                return IncomingHandler;
            if (url.Equals(StatusUrl, StringComparison.Ordinal))
                return StatusHandler;
            return null;
        }

        static HttpRequestData Response(HttpServerRequest r, int statusCode, String mime = "text/plain; charset=utf-8", String text = null)
        {
            r.SetResStatusCode(statusCode);
            r.SetResMime(mime);
            return new HttpRequestData(text == null ? ReadOnlyMemory<Byte>.Empty : Encoding.UTF8.GetBytes(text), true);
        }

        static async Task<String> ReadBody(HttpServerRequest r)
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
            return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }

        /// <summary>
        /// The url that Twilio requested (and signed): the public url (if specified) + the local url + the raw query
        /// </summary>
        String GetSignedUrl(HttpServerRequest r, String localUrl)
        {
            var raw = r.RawUrl ?? r.Url;
            var pub = PublicUrl;
            if (pub == null)
                return raw;
            var q = raw.IndexOf('?');
            return String.Concat(pub, localUrl, q < 0 ? "" : raw.Substring(q));
        }

        /// <summary>
        /// Read the form posted by Twilio, and validate the "X-Twilio-Signature" header (HMAC-SHA1 of the url and the parameters using the auth token)
        /// </summary>
        async Task<(NameValueCollection, HttpRequestData)> ReadAndValidate(HttpServerRequest r, String localUrl)
        {
            var body = await ReadBody(r).ConfigureAwait(false);
            if (body == null)
                return (null, Response(r, 413));
            var form = HttpUtility.ParseQueryString(body);
            var v = Validator;
            var sig = r.GetReqHeader("X-Twilio-Signature");
            if ((v == null) || String.IsNullOrEmpty(sig) || !v.Validate(GetSignedUrl(r, localUrl), form, sig))
            {
                WebhookFails.OnException(new Exception("Invalid signature"));
                return (null, Response(r, 403));
            }
            var account = form["AccountSid"];
            if ((account != null) && (account != AccountSid))
            {
                WebhookFails.OnException(new Exception("Unknown account " + account.ToQuoted()));
                return (null, Response(r, 403));
            }
            return (form, null);
        }

        /// <summary>
        /// An incoming message ("A message comes in" webhook), responds with empty TwiML (no automatic reply)
        /// </summary>
        async Task<HttpRequestData> HandleIncoming(HttpServerRequest r)
        {
            using var _ = PerfMon.Track(nameof(HandleIncoming));
            var (form, error) = await ReadAndValidate(r, IncomingUrl).ConfigureAwait(false);
            if (error != null)
                return error;
            try
            {
                Purge();
                OnIncomingMessage(form);
            }
            catch (Exception ex)
            {
                WebhookFails.OnException(ex);
                Msg?.AddMessage(LogPrefix + "Failed to process an incoming message", ex, MessageLevels.Warning);
            }
            return Response(r, 200, "text/xml; charset=utf-8", EmptyTwiml);
        }

        /// <summary>
        /// A status callback of a sent message
        /// </summary>
        async Task<HttpRequestData> HandleStatus(HttpServerRequest r)
        {
            using var _ = PerfMon.Track(nameof(HandleStatus));
            var (form, error) = await ReadAndValidate(r, StatusUrl).ConfigureAwait(false);
            if (error != null)
                return error;
            try
            {
                var q = r.QueryParameters;
                OnStatusCallback(form, q["r"], q["s"]);
            }
            catch (Exception ex)
            {
                WebhookFails.OnException(ex);
                Msg?.AddMessage(LogPrefix + "Failed to process a status callback", ex, MessageLevels.Warning);
            }
            return Response(r, 200);
        }

        #endregion//Http module

        #region Processing

        static TextMessageChannels GetChannel(String address, int numMedia)
        {
            if (address.StartsWith(WhatsAppPrefix, StringComparison.OrdinalIgnoreCase))
                return TextMessageChannels.WhatsApp;
            if (address.StartsWith(RcsPrefix, StringComparison.OrdinalIgnoreCase))
                return TextMessageChannels.Rcs;
            return numMedia > 0 ? TextMessageChannels.Mms : TextMessageChannels.Sms;
        }

        void OnIncomingMessage(NameValueCollection form)
        {
            var sid = form["MessageSid"] ?? form["SmsSid"];
            var from = form["From"];
            if ((sid == null) || (from == null))
                return;
            if (!SeenIncoming.TryAdd(sid, DateTime.UtcNow))
                return;
            int.TryParse(form["NumMedia"], out var numMedia);
            var channel = GetChannel(from, numMedia);
            var phone = StripPrefix(from);
            var digits = GetDigits(phone);
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
            var to = form["To"];
            var s = FindReceiver(to, form["MessagingServiceSid"]);
            var text = form["Body"];
            if (String.IsNullOrEmpty(text) && (numMedia > 0))
                text = "[media]";
            var rec = new TwilioTextMessage
            {
                Incoming = true,
                Id = sid,
                Time = DateTime.UtcNow,
                PhoneNumber = phone,
                Name = name,
                Country = isoCountry,
                Text = text,
                System = s?.System,
                ResponseToken = String.Concat(s?.Key, '/', sid),
                Channel = channel,
                To = s?.Display ?? (to == null ? null : StripPrefix(to)),
                SenderName = form["ProfileName"],
                Status = "received",
            };
            //  The user can obviously be reached on this channel (and the WhatsApp customer service window is open)
            NotOnChannel.TryRemove(ChannelKey(channel, digits), out var _);
            if (s != null)
                LastIncoming[WindowKey(s, digits)] = DateTime.UtcNow;
            AddRecord(rec);
            PublishIncoming(rec.Clone());
        }

        void OnStatusCallback(NameValueCollection form, String reference, String system)
        {
            var sid = form["MessageSid"] ?? form["SmsSid"];
            var statusText = form["MessageStatus"] ?? form["SmsStatus"];
            if ((sid == null) || (statusText == null))
                return;
            var status = TwilioErrors.GetStatus(statusText);
            if (status == TextMessageStatuses.Unknown)
                return;
            PendingOut.TryGetValue(sid, out var p);
            var rec = p?.Record;
            //  Callbacks may arrive out of order, don't go back from delivered
            if ((rec != null) && (status < TextMessageStatuses.Delivered) && (rec.Status == "delivered"))
                return;
            var to = form["To"] ?? "";
            int.TryParse(form["ErrorCode"], out var code);
            var u = new TextMessageStatusUpdate
            {
                Id = NewId("Status"),
                MessageId = sid,
                Reference = reference ?? p?.Message.Reference,
                System = system ?? p?.Message.System,
                PhoneNumber = rec?.PhoneNumber ?? StripPrefix(to),
                Channel = p?.Channel ?? GetChannel(to, 0),
                Status = status,
                Time = DateTime.UtcNow,
            };
            if (status == TextMessageStatuses.Failed)
            {
                u.Failure = code == 0 ? TextMessageFailures.Other : TwilioErrors.GetFailure(code);
                u.ErrorCode = code == 0 ? null : code.ToString();
                u.Error = form["ErrorMessage"] ?? (code == 0 ? statusText : String.Concat(statusText, " (", code, ')'));
                if (u.Failure == TextMessageFailures.NotOnChannel)
                {
                    var digits = p?.Digits ?? GetDigits(StripPrefix(to));
                    if (digits.Length > 0)
                        SetNotOnChannel(u.Channel, digits);
                }
                if (rec != null)
                    rec.Status = code == 0 ? statusText : String.Concat(statusText, " (", code, ')');
            }
            else if (rec != null)
            {
                rec.Status = statusText;
            }
            if (status.IsFinal())
                PendingOut.TryRemove(sid, out var _);
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
            //  Called by Twilio, authenticated using the signature
            public IReadOnlyList<String> Auth => null;
            public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;

            public String GetEtag(out bool useAsync, HttpServerRequest request)
            {
                useAsync = true;
                return null;
            }

            public HttpRequestData Get(HttpServerRequest request) => throw new NotSupportedException();

            public Task<HttpRequestData> GetAsync(HttpServerRequest request) => GetFn(request);
        }
    }
}
