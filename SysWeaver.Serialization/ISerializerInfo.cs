using System;
using System.Text;

namespace SysWeaver.Serialization
{
    public interface ISerializerInfo
    {
        /// <summary>
        /// The name of the compression implementation
        /// </summary>
        String Name { get; }

        /// <summary>
        /// The serialized formats file extension
        /// </summary>
        String Extension { get; }

        /// <summary>
        /// The mime code for the data created by this serializer
        /// </summary>
        String Mime { get; }

        /// <summary>
        /// The Content-Type http header value to use (including char set)
        /// </summary>
        String MimeHeader { get; }

        /// <summary>
        /// If the serializer is text-based, this is the econding to use
        /// </summary>
        Encoding Encoding { get; }

        /// <summary>
        /// The priority (quality) of the compressor, if multiple compressors are available the one with the highest priority is returned by the compression manager
        /// </summary>
        int Prio { get; }
    }
}
