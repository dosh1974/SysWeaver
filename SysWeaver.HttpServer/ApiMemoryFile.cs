using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// Support for raw API's that returns a <see cref="MemoryFile"/> ([WebApiRaw] without a mime type).
    /// The compiled API delegate wraps the method call with one of these methods, so the existing raw invokers (that returns readonly memory) are used.
    /// The mime type of the file is set on the request per call, and the name of the file (if any) is sent in a "Content-Disposition: inline" header.
    /// </summary>
    static class ApiMemoryFile
    {
        /// <summary>
        /// Set the mime type of the response from a file and return the file data
        /// </summary>
        /// <param name="file">The file returned by the API method</param>
        /// <param name="request">The request</param>
        /// <param name="allowCompression">False if compression is disabled for the API</param>
        /// <returns>The file data</returns>
        /// <exception cref="Exception">The file is null, a link, or has no mime type that can be determined (bad API implementation)</exception>
        public static ReadOnlyMemory<Byte> ToRaw(MemoryFile file, HttpServerRequest request, bool allowCompression)
        {
            if (file == null)
                throw new Exception("The API returned a null " + nameof(MemoryFile) + "!");
            var data = file.Data;
            if (data == null)
                throw new Exception("The API returned a " + nameof(MemoryFile) + " without data (a link to " + file.Name.ToQuoted() + "), that isn't supported!");
            var mime = file.Mime;
            Tuple<String, bool> mi;
            if (String.IsNullOrEmpty(mime))
            {
                var ext = String.IsNullOrEmpty(file.Name) ? null : Path.GetExtension(file.Name);
                if (String.IsNullOrEmpty(ext) || !MimeTypeMap.TryGetMimeType(ext, out mi))
                    throw new Exception("The API returned a " + nameof(MemoryFile) + " without a mime type, and no mime type is known for the file name " + file.Name.ToQuoted() + "!");
            }
            else
            {
                //  Unknown mime types are used as is (and compressed)
                if (!MimeTypeMap.TryGetMimeType(mime, out mi))
                    mi = Tuple.Create(mime, true);
            }
            request.SetResMime(mi.Item1);
            if (!(allowCompression && mi.Item2))
                request.DisableResCompression = true;
            var disp = GetContentDisposition(file.Name);
            if (disp != null)
                request.SetResHeader("Content-Disposition", disp);
            return data;
        }

        /// <summary>
        /// The Content-Disposition header value that names a file without forcing a download ("inline", so a browser still shows it),
        /// ex: inline; filename="cat.png"; filename*=UTF-8''cat.png.
        /// The plain filename is an ASCII fallback (other characters, quotes and backslashes are replaced with '_'), filename* is the
        /// RFC 5987 encoded UTF-8 name.
        /// </summary>
        /// <param name="name">The file name (any path is removed)</param>
        /// <returns>The header value, null if there is no name</returns>
        internal static String GetContentDisposition(String name)
        {
            if (String.IsNullOrWhiteSpace(name))
                return null;
            try
            {
                name = Path.GetFileName(name.Trim());
            }
            catch
            {
                return null;
            }
            if (String.IsNullOrEmpty(name))
                return null;
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (var c in name)
                sb.Append(((c < 32) || (c > 126) || (c == '"') || (c == '\\')) ? '_' : c);
            return String.Concat("inline; filename=\"", sb.ToString(), "\"; filename*=UTF-8''", Uri.EscapeDataString(name));
        }

        /// <summary>
        /// Await a file and return the file data, see <see cref="ToRaw(MemoryFile, HttpServerRequest, bool)"/>
        /// </summary>
        public static Task<ReadOnlyMemory<Byte>> ToRawTask(Task<MemoryFile> file, HttpServerRequest request, bool allowCompression)
        {
            if (file.IsCompletedSuccessfully)
                return Task.FromResult(ToRaw(file.Result, request, allowCompression));
            return ToRawTaskAsync(file, request, allowCompression);
        }

        static async Task<ReadOnlyMemory<Byte>> ToRawTaskAsync(Task<MemoryFile> file, HttpServerRequest request, bool allowCompression)
            => ToRaw(await file.ConfigureAwait(false), request, allowCompression);

        /// <summary>
        /// Await a file and return the file data, see <see cref="ToRaw(MemoryFile, HttpServerRequest, bool)"/>
        /// </summary>
        public static ValueTask<ReadOnlyMemory<Byte>> ToRawValueTask(ValueTask<MemoryFile> file, HttpServerRequest request, bool allowCompression)
        {
            if (file.IsCompletedSuccessfully)
                return new ValueTask<ReadOnlyMemory<Byte>>(ToRaw(file.Result, request, allowCompression));
            return ToRawValueTaskAsync(file, request, allowCompression);
        }

        static async ValueTask<ReadOnlyMemory<Byte>> ToRawValueTaskAsync(ValueTask<MemoryFile> file, HttpServerRequest request, bool allowCompression)
            => ToRaw(await file.ConfigureAwait(false), request, allowCompression);

        static readonly MethodInfo ToRawMethod = typeof(ApiMemoryFile).GetMethod(nameof(ToRaw));
        static readonly MethodInfo ToRawTaskMethod = typeof(ApiMemoryFile).GetMethod(nameof(ToRawTask));
        static readonly MethodInfo ToRawValueTaskMethod = typeof(ApiMemoryFile).GetMethod(nameof(ToRawValueTask));

        /// <summary>
        /// Wrap a call to an API method that returns a MemoryFile (sync, Task or ValueTask) so that it returns the data (the mime type is set on the request)
        /// </summary>
        /// <param name="call">The method call expression</param>
        /// <param name="request">The request parameter</param>
        /// <param name="allowCompression">False if compression is disabled for the API</param>
        /// <returns>An expression of type ReadOnlyMemory&lt;Byte&gt;, Task&lt;ReadOnlyMemory&lt;Byte&gt;&gt; or ValueTask&lt;ReadOnlyMemory&lt;Byte&gt;&gt;</returns>
        public static Expression Wrap(Expression call, ParameterExpression request, bool allowCompression)
        {
            var t = call.Type;
            var ac = Expression.Constant(allowCompression);
            if (t == typeof(MemoryFile))
                return Expression.Call(ToRawMethod, call, request, ac);
            if (t == typeof(Task<MemoryFile>))
                return Expression.Call(ToRawTaskMethod, call, request, ac);
            if (t == typeof(ValueTask<MemoryFile>))
                return Expression.Call(ToRawValueTaskMethod, call, request, ac);
            throw new Exception("Internal error, unsupported return type " + t.FullName);
        }
    }
}
