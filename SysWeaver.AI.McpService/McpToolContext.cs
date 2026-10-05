using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// A file that a tool attached to its result
    /// </summary>
    sealed class McpToolFile
    {
        public McpToolFile(String mime, ReadOnlyMemory<Byte> data, String url)
        {
            Mime = mime;
            Data = data;
            Url = url;
        }
        public readonly String Mime;
        public readonly ReadOnlyMemory<Byte> Data;
        public readonly String Url;
    }

    /// <summary>
    /// The tool context used when a tool is called over MCP (the AI chat uses a chat session context).
    /// Files and links added by a tool are collected and returned to the MCP client as part of the tool result.
    /// </summary>
    sealed class McpToolContext : IAiToolContext
    {
        public McpToolContext(HttpServerRequest request, Func<String, ReadOnlyMemory<Byte>, String, String> storeFile)
        {
            Request = request;
            StoreFile = storeFile;
        }

        readonly HttpServerRequest Request;
        readonly Func<String, ReadOnlyMemory<Byte>, String, String> StoreFile;
        readonly List<String> InternalLinks = new();
        readonly List<McpToolFile> InternalFiles = new();

        /// <summary>
        /// Session properties are prefixed so that they don't collide with other session data
        /// </summary>
        const String PropertyPrefix = "Mcp.ToolProperty.";

        /// <summary>
        /// Used when there is no session
        /// </summary>
        ConcurrentDictionary<String, Object> LocalProperties;

        public IReadOnlyList<String> Links
        {
            get
            {
                lock (InternalLinks)
                    return InternalLinks.ToArray();
            }
        }

        public IReadOnlyList<McpToolFile> Files
        {
            get
            {
                lock (InternalFiles)
                    return InternalFiles.ToArray();
            }
        }

        public void AddLink(String url)
        {
            if (String.IsNullOrEmpty(url))
                return;
            lock (InternalLinks)
                InternalLinks.Add(url);
        }

        public String AddMessageFile(String mime, String data, String filename)
            => AddMessageFile(mime, Decode(data), filename);

        public String AddMessageFile(String mime, ReadOnlyMemory<Byte> data, String filename)
        {
            var url = StoreFile(mime, data, filename);
            lock (InternalFiles)
                InternalFiles.Add(new McpToolFile(mime, data, url));
            return url;
        }

        /// <summary>
        /// The data is either a data url ("data:mime;base64,...") or text
        /// </summary>
        static ReadOnlyMemory<Byte> Decode(String data)
        {
            if (String.IsNullOrEmpty(data))
                return ReadOnlyMemory<Byte>.Empty;
            const String b64 = ";base64,";
            if (data.StartsWith("data:", StringComparison.Ordinal))
            {
                var i = data.IndexOf(b64, StringComparison.Ordinal);
                if (i > 0)
                    return Convert.FromBase64String(data.Substring(i + b64.Length));
            }
            return Encoding.UTF8.GetBytes(data);
        }

        public void SetProperty<T>(String key, T value)
        {
            var s = Request.Session;
            if (s != null)
            {
                s.Set(PropertyPrefix + key, value);
                return;
            }
            GetLocal()[key] = value;
        }

        public bool TryGetProperty<T>(String key, out T value)
        {
            var s = Request.Session;
            if (s != null)
                return s.TryGet(PropertyPrefix + key, out value);
            if (GetLocal().TryGetValue(key, out var v) && (v is T t))
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryRemoveProperty<T>(String key, out T value)
        {
            var s = Request.Session;
            if (s != null)
                return s.TryRemove(PropertyPrefix + key, out value);
            if (GetLocal().TryRemove(key, out var v) && (v is T t))
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }

        ConcurrentDictionary<String, Object> GetLocal()
        {
            var l = LocalProperties;
            if (l != null)
                return l;
            lock (this)
                return LocalProperties ??= new ConcurrentDictionary<String, Object>(StringComparer.Ordinal);
        }
    }
}
