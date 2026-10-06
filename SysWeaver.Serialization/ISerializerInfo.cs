using System;
using System.Text;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// Describes a serialization format implementation (name, file extension, MIME type, encoding and priority).
    /// </summary>
    public interface ISerializerInfo
    {
        /// <summary>
        /// The name of the serializer implementation (for display and diagnostics).
        /// </summary>
        String Name { get; }

        /// <summary>
        /// The file extension of the serialized format, all lower case without a leading dot (ex: "json", "xml", "bson").
        /// This is the key used to look up serializers in <see cref="SerManager"/>.
        /// </summary>
        String Extension { get; }

        /// <summary>
        /// The MIME type of the data created by this serializer (ex: "application/json").
        /// </summary>
        String Mime { get; }

        /// <summary>
        /// The Content-Type HTTP header value to use (the MIME type with a "; charset=" suffix for text based serializers).
        /// </summary>
        String MimeHeader { get; }

        /// <summary>
        /// The text encoding used by text based serializers, null for binary serializers.
        /// </summary>
        Encoding Encoding { get; }

        /// <summary>
        /// The priority of the serializer, if multiple serializers are registered for the same extension, the one with the highest priority is returned by <see cref="SerManager"/>
        /// (on a tie, the one registered last wins).
        /// </summary>
        int Prio { get; }
    }
}
