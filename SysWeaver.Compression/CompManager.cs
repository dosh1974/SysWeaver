using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SysWeaver.Compression
{


    /// <summary>
    /// The process wide registry of compression implementations, looked up by HTTP content-coding name or file extension.
    /// </summary>
    /// <remarks>
    /// The built-in .NET implementations are registered automatically: <see cref="CompBrotliNETNew"/> ("br"), <see cref="CompDeflateNET"/> ("deflate") and <see cref="CompGZipNET"/> ("gzip").
    /// Plug-ins (like native Brotli or Zstandard) are added using <see cref="AddType(ICompType)"/>, usually through their static <c>Register</c> method.
    /// Registration is thread safe (serialized by a lock), lookups are lock free.
    /// When multiple implementations share an HTTP code or extension, the one with the highest <see cref="ICompInfo.Prio"/> is used (on a tie, the last one added).
    /// </remarks>
    public static class CompManager
    {
        static CompManager()
        {
            CompBrotliNETNew.Register();
            //CompBrotliNET.Register();
            CompDeflateNET.Register();
            CompGZipNET.Register();
        }

        /// <summary>
        /// Add a compression type to the compression manager
        /// </summary>
        /// <param name="type">The type to add</param>
        /// <returns>True if the type was added, false if this instance was already added</returns>
        public static bool AddType(ICompType type)
        {
            lock (Unique)
            {
                if (!Unique.Add(type))
                    return false;
                CompTypes.Add(type);
                var key = type.HttpCode;
                var f = FromHttpCode;
                if (!f.TryGetValue(key, out var val) || (val.Prio <= type.Prio))
                    f[key] = type;
                f = FromExts;
                foreach (var k2 in type.FileExtensions)
                {
                    if (!f.TryGetValue(k2, out val) || (val.Prio <= type.Prio))
                        f[k2] = type;
                    var k3 = "." + k2;
                    if (!f.TryGetValue(k3, out val) || (val.Prio <= type.Prio))
                        f[k3] = type;
                    if (!ExtOrder.Contains(k2))
                    {
                        ExtOrder.Add(k2);
                        ExtOrder.Add(k3);
                    }
                }
                //  Copy on write, so that lookups are lock and allocation free
                var exts = new KeyValuePair<String, ICompType>[ExtOrder.Count];
                for (int i = 0; i < exts.Length; ++i)
                    exts[i] = new KeyValuePair<String, ICompType>(ExtOrder[i], f[ExtOrder[i]]);
                ExtArray = exts;
                return true;
            }
        }

        /// <summary>
        /// Get the implementation for a given file extension (uses the ones with highest prio if multiple compressors are available)
        /// </summary>
        /// <param name="ext">The file extension, all lowercase (can include a . prefix, like ".gzip"), compared ordinally</param>
        /// <returns>A compressor for the given file extension or null if none exist</returns>
        /// <remarks>Allocation free, performs a linear search of the (few) registered extensions.</remarks>
        public static ICompType GetFromExt(ReadOnlySpan<Char> ext)
        {
            foreach (var x in ExtArray)
                if (ext.SequenceEqual(x.Key))
                    return x.Value;
            return null;
        }

        /// <summary>
        /// All file extensions (with and without a "." prefix) and the implementation to use, in the order that they were added (a copy-on-write snapshot, safe to enumerate while types are added)
        /// </summary>
        internal static ReadOnlySpan<KeyValuePair<String, ICompType>> ExtensionArray => ExtArray;

        static readonly List<String> ExtOrder = new();
        static volatile KeyValuePair<String, ICompType>[] ExtArray = [];

        /// <summary>
        /// Get all added compression types in the order that they were added
        /// </summary>
        /// <remarks>This is the live list, don't enumerate it while types may be added concurrently.</remarks>
        public static IReadOnlyList<ICompType> All => CompTypes;

        /// <summary>
        /// Get all supported HTTP codes
        /// </summary>
        public static IEnumerable<String> HttpCodes => FromHttpCode.Keys;

        /// <summary>
        /// Get all supported file extensions (each extension is included both with and without a "." prefix)
        /// </summary>
        public static IEnumerable<String> Extensions => FromExts.Keys;

        /// <summary>
        /// Get the implementation to use for each supported HTTP code
        /// </summary>
        public static IEnumerable<KeyValuePair<String, ICompType>> HttpCodeHandlers => FromHttpCode;

        /// <summary>
        /// Get the implementation to use for each supported file extension (with and without a "." prefix)
        /// </summary>
        public static IEnumerable<KeyValuePair<String, ICompType>> ExtensionHandlers => FromExts;

        /// <summary>
        /// Get the implementation for a given HTTP content-coding (uses the ones with highest prio if multiple compressors are available)
        /// </summary>
        /// <param name="httpCode">The HTTP code, all lowercase (compared ordinally), ex: "br"</param>
        /// <returns>A compressor for the given HTTP code or null if none exist</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ICompType GetFromHttp(String httpCode)
        {
            FromHttpCode.TryGetValue(httpCode, out var type);
            return type;
        }

        /// <summary>
        /// Get the implementation for a given file extension (uses the ones with highest prio if multiple compressors are available)
        /// </summary>
        /// <param name="ext">The file extension, all lowercase (can include a . prefix, like ".gzip"), compared ordinally</param>
        /// <returns>A compressor for the given file extension or null if none exist</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ICompType GetFromExt(String ext)
        {
            FromExts.TryGetValue(ext, out var type);
            return type;
        }

        static readonly HashSet<ICompType> Unique = new ();
        static readonly List<ICompType> CompTypes = new ();
        static readonly SemiFrozenDictionary<String, ICompType> FromHttpCode = new (StringComparer.Ordinal);
        static readonly SemiFrozenDictionary<String, ICompType> FromExts = new (StringComparer.Ordinal);
    }


}
