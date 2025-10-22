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

    [Flags]
    public enum ChatMessageFlags
    {
        IsWorking = 1,

        CanRemove = 256,
    }


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
            if (data.FastStartsWith("data:"))
            {
                var t = data.IndexOf("base64,");
                if (t > 0)
                    mem = Convert.FromBase64String(data.Substring(t + 7));
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
            var rh = new StaticMemoryHttpRequestHandler("chat/" + name, filename, mem ?? ReadOnlyMemory<Byte>.Empty, mime, Comp, 30, 15, HttpServerTools.ToEtag(DateTime.UtcNow), comp, auth);
            Files.TryAdd(f, rh);
            return name;
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
