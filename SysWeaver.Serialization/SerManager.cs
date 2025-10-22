using System;
using System.Collections.Generic;

namespace SysWeaver.Serialization
{

    public static class SerManager
    {
        static SerManager()
        {
            NetJsonSerializer.Register();
        }

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
        /// Get all added serializers in the order that they we're added
        /// </summary>
        public static IReadOnlyList<ISerializerType> All => SerTypes;

        /// <summary>
        /// Get all supported "file extensions"
        /// </summary>
        public static IReadOnlyCollection<String> Extensions => FromExts.Keys;

        /// <summary>
        /// Get all supported "file extensions" that serialize to text
        /// </summary>
        public static IReadOnlyCollection<String> TextExtensions => FromTextExts.Keys;


        /// <summary>
        /// Get a dictionary with all handlers for all suported "file extensions"
        /// </summary>
        public static IReadOnlyDictionary<String, ISerializerType> ExtensionHandlers => FromExts;


        /// <summary>
        /// Get a dictionary with all handlers for all suported "file extensions" that serialize to text
        /// </summary>
        public static IReadOnlyDictionary<String, ITextSerializerType> TextExtensionHandlers => FromTextExts;


        /// <summary>
        /// Get the implementation for a given "file extension" (uses the ones with highest prio if multiple serializers are available)
        /// </summary>
        /// <param name="ext">The file extension, all lowercase (can include a . prefix, like ".json")</param>
        /// <returns>A serializer for the given file extension or null if non exist</returns>
        public static ISerializerType Get(String ext)
        {
            FromExts.TryGetValue(ext, out var type);
            return type;
        }


        /// <summary>
        /// Get the implementation for a given "file extension" (uses the ones with highest prio if multiple serializers are available).
        /// Only serializers that is text based is supported.
        /// </summary>
        /// <param name="ext">The file extension, all lowercase (can include a . prefix, like ".json")</param>
        /// <returns>A text based serializer for the given file extension or null if non exist</returns>
        public static ITextSerializerType GetText(String ext)
        {
            FromTextExts.TryGetValue(ext, out var type);
            return type;
        }

        static readonly HashSet<ISerializerType> Unique = new();
        static readonly List<ISerializerType> SerTypes = new();
        static readonly Dictionary<String, ISerializerType> FromExts = new(StringComparer.Ordinal);
        static readonly Dictionary<String, ITextSerializerType> FromTextExts = new(StringComparer.Ordinal);


    }
}
