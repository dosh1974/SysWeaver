using System;
using System.Collections.Generic;

namespace SysWeaver.Compression
{


    /// <summary>
    /// Manages compression implementations
    /// </summary>
    public static class CompManager
    {
        static CompManager()
        {
            CompBrotliNET.Register();
            CompDeflateNET.Register();
            CompGZipNET.Register();
        }

        /// <summary>
        /// Add a compression type to the compression manager
        /// </summary>
        /// <param name="type">The type to add</param>
        /// <exception cref="ArgumentException"></exception>
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
                }
                return true;
            }
        }

        /// <summary>
        /// Get all added compression types in the order that they we're added
        /// </summary>
        public static IReadOnlyList<ICompType> All => CompTypes;

        /// <summary>
        /// Get all supported http codes
        /// </summary>
        public static IReadOnlyCollection<String> HttpCodes => FromHttpCode.Keys;


        /// <summary>
        /// Get all supported file extensions
        /// </summary>
        public static IReadOnlyCollection<String> Extensions => FromExts.Keys;

        /// <summary>
        /// Get a dictionary with all handlers for all suported http codes
        /// </summary>
        public static IReadOnlyDictionary<String, ICompType> HttpCodeHandlers => FromHttpCode;

        /// <summary>
        /// Get a dictionary with all handlers for all suported file extensions
        /// </summary>
        public static IReadOnlyDictionary<String, ICompType> ExtensionHandlers => FromExts;

        /// <summary>
        /// Get the implementation for a given http code (uses the ones with highest prio if multiple compressors are available)
        /// </summary>
        /// <param name="httpCode">The http code, all lowercase</param>
        /// <returns>A compressor for the given http code or null if non exist</returns>
        public static ICompType GetFromHttp(String httpCode)
        {
            FromHttpCode.TryGetValue(httpCode, out var type);
            return type;
        }

        /// <summary>
        /// Get the implementation for a given file extension (uses the ones with highest prio if multiple compressors are available)
        /// </summary>
        /// <param name="ext">The file extension, all lowercase (can include a . prefix, like ".gzip")</param>
        /// <returns>A compressor for the given file extension or null if non exist</returns>
        public static ICompType GetFromExt(String ext)
        {
            FromExts.TryGetValue(ext, out var type);
            return type;
        }

        static readonly HashSet<ICompType> Unique = new ();
        static readonly List<ICompType> CompTypes = new ();
        static readonly Dictionary<String, ICompType> FromHttpCode = new (StringComparer.Ordinal);
        static readonly Dictionary<String, ICompType> FromExts = new (StringComparer.Ordinal);
    }


}
