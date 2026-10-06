using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SysWeaver.Serialization
{

    /// <summary>
    /// Process wide registry of serializer implementations, keyed by file extension (with and without a leading dot).
    /// </summary>
    /// <remarks>
    /// <see cref="NetJsonSerializer"/> is always registered (by the static constructor).
    /// Other serializers are added by calling their static <c>Register</c> method (or by listing them in a service manifest).
    /// When several serializers share an extension, lookups return the one with the highest <see cref="ISerializerInfo.Prio"/>, on a tie the last registered one.
    /// Lookups are thread safe, registration should be done at startup (<see cref="AddType"/> is not safe to call concurrently).
    /// </remarks>
    public static class SerManager
    {
        static SerManager()
        {
            NetJsonSerializer.Register();
        }

        /// <summary>
        /// Register a serializer.
        /// The serializer is added to <see cref="All"/> and becomes the handler for its extension (both "ext" and ".ext") if no handler with a higher priority exists.
        /// Text based serializers (<see cref="ITextSerializerType"/>) are also registered as text handlers.
        /// </summary>
        /// <param name="type">The serializer to register.</param>
        /// <returns>True if the serializer was added, false if this instance was already registered.</returns>
        /// <remarks>Not thread safe, register serializers at startup.</remarks>
        public static bool AddType(ISerializerType type)
        {
            if (!Unique.Add(type))
                return false;
            SerTypes.Add(type);
            var key = type.Extension;
            var f = FromExts;
            if (!f.TryGetValue(key, out var val) || (val.Prio <= type.Prio))
                f[key] = type;
            key = "." + key;
            if (!f.TryGetValue(key, out val) || (val.Prio <= type.Prio))
                f[key] = type;
            var ttype = type as ITextSerializerType;
            if (ttype != null)
            {
                key = type.Extension;
                var tf = FromTextExts;
                if (!tf.TryGetValue(key, out var tval) || (tval.Prio <= ttype.Prio))
                    tf[key] = ttype;
                key = "." + key;
                if (!tf.TryGetValue(key, out tval) || (tval.Prio <= ttype.Prio))
                    tf[key] = ttype;
            }
            return true;
        }

        /// <summary>
        /// Get all added serializers in the order that they were added (including those that are shadowed by a higher priority serializer).
        /// </summary>
        public static IReadOnlyList<ISerializerType> All => SerTypes;

        /// <summary>
        /// Get all supported "file extensions" (each extension is listed both with and without a leading dot).
        /// </summary>
        public static IEnumerable<String> Extensions => FromExts.Keys;

        /// <summary>
        /// Get all supported "file extensions" that serialize to text (each extension is listed both with and without a leading dot).
        /// </summary>
        public static IEnumerable<String> TextExtensions => FromTextExts.Keys;


        /// <summary>
        /// Get the active (highest priority) handler for every supported "file extension" (each extension is listed both with and without a leading dot).
        /// </summary>
        public static IEnumerable<KeyValuePair<String, ISerializerType>> ExtensionHandlers => FromExts;


        /// <summary>
        /// Get the active (highest priority) text based handler for every supported "file extension" (each extension is listed both with and without a leading dot).
        /// </summary>
        public static IEnumerable<KeyValuePair<String, ITextSerializerType>> TextExtensionHandlers => FromTextExts;


        /// <summary>
        /// Get the implementation for a given "file extension" (the one with highest priority if multiple serializers are available).
        /// </summary>
        /// <param name="ext">The file extension, all lowercase (can include a . prefix, like ".json"). Matching is ordinal (case sensitive). Must not be null.</param>
        /// <returns>A serializer for the given file extension or null if none exist.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ISerializerType Get(String ext)
        {
            FromExts.TryGetValue(ext, out var type);
            return type;
        }


        /// <summary>
        /// Get the text based implementation for a given "file extension" (the one with highest priority if multiple text serializers are available).
        /// Only text based serializers are considered, so this may return a different (lower priority) implementation than <see cref="Get(string)"/>.
        /// </summary>
        /// <param name="ext">The file extension, all lowercase (can include a . prefix, like ".json"). Matching is ordinal (case sensitive). Must not be null.</param>
        /// <returns>A text based serializer for the given file extension or null if none exist.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ITextSerializerType GetText(String ext)
        {
            FromTextExts.TryGetValue(ext, out var type);
            return type;
        }

        static readonly HashSet<ISerializerType> Unique = new();
        static readonly List<ISerializerType> SerTypes = new();
        static readonly SemiFrozenDictionary<String, ISerializerType> FromExts = new(StringComparer.Ordinal);
        static readonly SemiFrozenDictionary<String, ITextSerializerType> FromTextExts = new(StringComparer.Ordinal);


    }
}
