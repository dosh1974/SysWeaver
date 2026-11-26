using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using SysWeaver.Auth;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public sealed partial class McpService
    {
        /// <summary>
        /// A tool call, a row of <see cref="CallLogTable(TableDataRequest)"/>
        /// </summary>
        [TableDataPrimaryKey(nameof(Id))]
        [TableDataDefaultOrder("-" + nameof(Id))]
        public sealed class CallLogRow
        {
            /// <summary>
            /// A sequence number (per MCP end point)
            /// </summary>
            [TableDataHide]
            public long Id;

            /// <summary>
            /// When the call was made (UTC)
            /// </summary>
            public DateTime Time;

            /// <summary>
            /// The name of the tool that was called (as sent by the client)
            /// </summary>
            public String Tool;

            /// <summary>
            /// True if the call failed (the error is the output)
            /// </summary>
            public bool IsError;

            /// <summary>
            /// The time it took to run the tool
            /// </summary>
            [TableDataNumber(1, "{0} ms")]
            public double Duration;

            /// <summary>
            /// The arguments of the call (json, may be truncated)
            /// </summary>
            [TableDataJson(48)]
            public String Input;

            /// <summary>
            /// The text of the result, or the error message (json, a text that isn't json is a json string, may be truncated)
            /// </summary>
            [TableDataJson(48)]
            public String Output;

            /// <summary>
            /// Files (mime type and url) and links that was attached to the result, one per line
            /// </summary>
            [TableDataText]
            public String Attachments;

            /// <summary>
            /// The name of the user that made the call
            /// </summary>
            public String UserName;

            /// <summary>
            /// True if the user was authenticated using an API key
            /// </summary>
            public bool ApiKey;

            /// <summary>
            /// The client address
            /// </summary>
            [TableDataIp]
            public String Ip;

            /// <summary>
            /// The user agent of the client
            /// </summary>
            [TableDataUserAgent]
            public String UserAgent;

            /// <summary>
            /// The guid of the user that made the call
            /// </summary>
            [TableDataHide]
            public String UserGuid;

            /// <summary>
            /// The session id (the hash of the session token)
            /// </summary>
            [TableDataHide]
            public String Session;

            /// <summary>
            /// The MCP end point that served the call
            /// </summary>
            [TableDataHide]
            public String EndPoint;
        }

        /// <summary>
        /// McpParams.CallLogSize
        /// </summary>
        readonly int CallLogSize;

        /// <summary>
        /// McpParams.CallLogMaxLength
        /// </summary>
        readonly int CallLogMaxLength;

        /// <summary>
        /// The latest calls (at most CallLogSize), lock on it when used
        /// </summary>
        readonly Queue<CallLogRow> CallLog = new();

        long CallLogId;
        long TotalCalls;
        long TotalErrors;
        long LastCallTicks;
        long LastErrorTicks;
        String LastError;

        /// <summary>
        /// Log a tool call (and update the call statistics)
        /// </summary>
        void LogCall(HttpServerRequest r, String tool, JsonElement args, String output, bool isError, TimeSpan duration, IReadOnlyList<McpToolFile> files, IReadOnlyList<String> links)
        {
            var now = DateTime.UtcNow;
            Interlocked.Increment(ref TotalCalls);
            Interlocked.Exchange(ref LastCallTicks, now.Ticks);
            if (isError)
            {
                Interlocked.Increment(ref TotalErrors);
                Volatile.Write(ref LastError, String.Concat(tool, ": ", output));
                Interlocked.Exchange(ref LastErrorTicks, now.Ticks);
            }
            var size = CallLogSize;
            if (size <= 0)
                return;
            var max = CallLogMaxLength;
            var s = r.Session;
            var a = s?.Auth;
            String ip = null;
            try
            {
                ip = r.GetIpAddress();
            }
            catch
            {
            }
            var row = new CallLogRow
            {
                Id = Interlocked.Increment(ref CallLogId),
                Time = now,
                Tool = tool,
                IsError = isError,
                Duration = duration.TotalMilliseconds,
                Input = args.ValueKind == JsonValueKind.Undefined ? null : ToLogJson(args.GetRawText(), max),
                Output = ToLogJson(output, max),
                Attachments = GetAttachments(files, links),
                UserName = a?.Username,
                ApiKey = a?.IsApiKey ?? false,
                Ip = ip,
                UserAgent = s?.UserAgent,
                UserGuid = a?.Guid,
                Session = s?.TokenHash,
                EndPoint = "/" + Url,
            };
            var log = CallLog;
            lock (log)
            {
                log.Enqueue(row);
                while (log.Count > size)
                    log.Dequeue();
            }
        }

        /// <summary>
        /// Text that is valid json (an object or an array) is kept as is (cut off at the max length, the json viewer handles that), other text is returned as a json string
        /// </summary>
        static String ToLogJson(String text, int max)
        {
            if (text == null)
                return null;
            var isCut = text.Length > max;
            text = text.LimitLength(max, "");
            var t = text.AsSpan().TrimStart();
            if ((t.Length > 0) && ((t[0] == '{') || (t[0] == '[')))
            {
                if (isCut)
                {
                    if (JsonPretty.Format(text) != null)
                        return text;
                }
                else
                {
                    try
                    {
                        using var _ = JsonDocument.Parse(text);
                        return text;
                    }
                    catch (JsonException)
                    {
                    }
                }
            }
            return JsonSerializer.Serialize(text);
        }

        static String GetAttachments(IReadOnlyList<McpToolFile> files, IReadOnlyList<String> links)
        {
            if (((files?.Count ?? 0) + (links?.Count ?? 0)) <= 0)
                return null;
            var sb = new StringBuilder();
            foreach (var f in files.Nullable())
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(f.Mime).Append(' ').Append(f.Url).Append(" (").Append(f.Data.Length).Append(" bytes)");
            }
            foreach (var l in links.Nullable())
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append("link ").Append(l);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The latest tool calls of the MCP end point, with input, output and caller (at most McpParams.CallLogSize calls are kept in memory).
        /// If the Param of the request is the guid of a temporary server (see CreateTempServer), the calls of that server are returned instead.
        /// </summary>
        /// <param name="r">Table request parameters (paging, sorting, filtering)</param>
        /// <returns>A table with one row per call, latest first</returns>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiClientCache(1)]
        [WebMenuTable(null, "Admin/{0}", "MCP call log", "The latest tool calls of the MCP end point, with input, output and caller", "IconTableLog")]
        public TypedTableData<CallLogRow> CallLogTable(TableDataRequest r)
            => (TryGetTempServer(r?.Param)?.Service ?? this).GetCallLogTable(r);

        TypedTableData<CallLogRow> GetCallLogTable(TableDataRequest r)
        {
            CallLogRow[] rows;
            var log = CallLog;
            lock (log)
                rows = log.ToArray();
            return TableDataTools.GetTyped(r, 10000, rows, "MCP calls at " + ("/" + Url).ToQuoted());
        }
    }
}
