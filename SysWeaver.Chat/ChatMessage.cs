using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.Chat
{


    /// <summary>
    /// A chat message
    /// </summary>
    public sealed class ChatMessage : ChatMessageBody
    {
        /// <summary>
        /// The unique id of the message (unique in the chat session)
        /// </summary>
        public long Id;
        
        /// <summary>
        /// The user/entity who posted this message
        /// </summary>
        public String From;



        /// <summary>
        /// Set the intended user that should receive this message
        /// </summary>
        /// <param name="to">The authenticated users guid</param>
        public void SetTo(String to) => To = to;


        /// <summary>
        /// Check if this message is intended for a specific user
        /// </summary>
        /// <param name="guid">The authenticated users guid</param>
        /// <returns>True if the message is only visible to the supplied user</returns>
        public bool IsFor(String guid)
        {
            var t = To;
            if (String.IsNullOrEmpty(t))
                return true;
            return t.FastEquals(guid);
        }


        public String GetTo() => To;

        /// <summary>
        /// If non empty, this message is only for that user
        /// </summary>
        internal String To;

        /// <summary>
        /// Url to an image that represents the user/entity that posted this message
        /// </summary>
        public String FromImage;

        /// <summary>
        /// The time when this message was created
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// Flags
        /// </summary>
        public ChatMessageFlags Flags;


        /// <summary>
        /// Optional extra menu items
        /// </summary>
        public ChatMenuItem[] MenuItems;

        static readonly HttpCompressionPriority Comp = HttpCompressionPriority.GetSupportedEncoders("br:Balanced, deflate:Balanced, gzip:Balanced");
        static readonly ICompType CompType = CompManager.GetFromHttp("br") ?? CompManager.GetFromHttp("deflate") ?? CompManager.GetFromHttp("gzip");

        public String AddFileData(String mime, String data, String providerName, String providerChatId, IReadOnlyList<String> auth, HttpServerRequest request, String filename)
            => AddFile(mime, data, default, false, providerName, providerChatId, auth, filename);

        /// <summary>
        /// Add a file from bytes, same as calling <see cref="AddFileData(string, string, string, string, IReadOnlyList{string}, HttpServerRequest, string)"/> with the data url of the bytes
        /// ("data:" + mime + ";base64," + base64 data) without the base64 encoding and decoding
        /// </summary>
        /// <param name="mime">The mime type</param>
        /// <param name="data">The data (copied)</param>
        /// <param name="providerName">The provider name (part of the url)</param>
        /// <param name="providerChatId">The provider chat id (part of the url)</param>
        /// <param name="auth">The auth required to get the file</param>
        /// <param name="request">The request (not used)</param>
        /// <param name="filename">The file name</param>
        /// <returns>The local url of the file</returns>
        public String AddFileData(String mime, ReadOnlyMemory<Byte> data, String providerName, String providerChatId, IReadOnlyList<String> auth, HttpServerRequest request, String filename)
            => AddFile(mime, null, data, true, providerName, providerChatId, auth, filename);

        String AddFile(String mime, String data, ReadOnlyMemory<Byte> bytes, bool isBytes, String providerName, String providerChatId, IReadOnlyList<String> auth, String filename)
        {
            var f = Interlocked.Increment(ref FileId).ToString();
            MimeTypeMap.TryGetExtensions(mime, out var exts);
            var ext = exts?.FirstOrDefault();
            bool canComp = false;
            if (MimeTypeMap.TryGetMimeType(mime, out var info))
                canComp = info.Item2;
            var name = String.Join('/', "file", providerName, providerChatId, Id, f);
            filename = PathExt.SafeFilename(filename?.Trim());
            if (String.IsNullOrEmpty(filename))
                filename = String.Join('_', providerName, providerChatId, Id, f);
            name = String.Join('/', name, filename.LimitLength(128, "").TrimEnd());
            if (!String.IsNullOrEmpty(ext))
                name += ext;
            ReadOnlyMemory<Byte>? mem = null;
            ICompType comp = null;
            if (isBytes)
                mem = bytes;
            else if (data.FastStartsWith("data:"))
            {
                var t = data.FastIndexOf("base64,");
                if (t > 0)
                    mem = FromBase64(data, t + 7);
            }
            if (mem == null)
            {
                mem = Encoding.UTF8.GetBytes(data);
                canComp = true;
            }
            if (canComp)
            {
                comp = CompType;
                var m = mem ?? ReadOnlyMemory<Byte>.Empty;
                mem = comp.GetCompressed(m.Span, m.Length > 8192 ? CompEncoderLevels.Balanced : CompEncoderLevels.Best);
            }
            else if (isBytes)
            {
                //  A copy (the caller may reuse its memory, the data url path decoded into a new array)
                mem = bytes.ToArray();
            }
            var now = DateTime.UtcNow;
            var rh = new StaticMemoryHttpRequestHandler("chat/" + name, filename, mem ?? ReadOnlyMemory<Byte>.Empty, mime, Comp, 30, 15, now, null, comp, auth, 0, false);
            Files.TryAdd(f, rh);
            return name;
        }

        /// <summary>
        /// Same as Convert.FromBase64String(data.Substring(start)) without the copy of the payload (if the decoded length is exact, i.e. no white space).
        /// </summary>
        static Byte[] FromBase64(String data, int start)
        {
            var s = data.AsSpan(start);
            var l = s.Length;
            if ((l > 0) && ((l & 3) == 0))
            {
                var n = (l >> 2) * 3;
                if (s[l - 1] == '=')
                {
                    --n;
                    if (s[l - 2] == '=')
                        --n;
                }
                var b = new Byte[n];
                if (Convert.TryFromBase64Chars(s, b, out var w) && (w == n))
                    return b;
            }
            //  White space, invalid data (throws the same exception) or empty
            return Convert.FromBase64String(data.Substring(start));
        }

        public String AddData(Object o, String providerName, String providerChatId, HttpServerRequest request)
        {
            var f = Interlocked.Increment(ref FileId).ToString();
            var name = String.Join('_', "file", providerName, providerChatId, Id, f);
            Files.TryAdd(f, o);
            return name;

        }

        public Object GetData(String url)
            => Files.TryGetValue(url, out var f) ? f : null;


        public void AddNamedData(String name, Object o)
        {
            Files.TryAdd(name, o);
        }

        internal int FileId;
        internal readonly ConcurrentDictionary<String, Object> Files = new ConcurrentDictionary<string, Object>(StringComparer.Ordinal);

    }

}
