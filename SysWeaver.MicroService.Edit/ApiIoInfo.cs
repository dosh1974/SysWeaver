using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// How the APIs of a service can be called: the enabled serializers and compression methods and the request size limits
    /// (see <see cref="TypeService.GetApiIoInfo"/>, shown by explore/apihelp.html).
    /// </summary>
    public sealed class ApiIoInfo
    {
        /// <summary>
        /// The serializers accepted for the argument (POST body, selected with the Content-Type header).
        /// </summary>
        public ApiIoSerializer[] Input;

        /// <summary>
        /// The serializers that can be used for the result (selected with the Accept header).
        /// </summary>
        public ApiIoSerializer[] Output;

        /// <summary>
        /// The name of the serializer used for a body without a Content-Type header.
        /// </summary>
        public String DefaultInput;

        /// <summary>
        /// The name of the serializer used when the Accept header names no enabled serializer.
        /// </summary>
        public String DefaultOutput;

        /// <summary>
        /// The HTTP codes of the compression methods a request body (Content-Encoding) can use, ex: "br", "deflate", "gzip".
        /// </summary>
        public String[] Compression;

        /// <summary>
        /// The type characters of a compressed GET argument ("?_" + char + base64url data), ex: "g" = gzip, "u" = uncompressed.
        /// </summary>
        public ApiIoGetCompression[] GetCompression;

        /// <summary>
        /// The maximum size in bytes of a request body as sent (zero or negative: no limit).
        /// </summary>
        public long MaxRequestSize;

        /// <summary>
        /// The maximum size in bytes of a request body after decompression (zero or negative: no limit).
        /// </summary>
        public long MaxDecompressedSize;
    }

    /// <summary>
    /// A serializer of <see cref="ApiIoInfo"/>.
    /// </summary>
    public sealed class ApiIoSerializer
    {
        /// <summary>
        /// The name, ex: "json".
        /// </summary>
        public String Name;

        /// <summary>
        /// The mime type, ex: "application/json".
        /// </summary>
        public String Mime;

        /// <summary>
        /// The file extension (also the name used in a binary GET argument), ex: "json".
        /// </summary>
        public String Extension;

        /// <summary>
        /// True for a text format (can be sent as text in a GET argument).
        /// </summary>
        public bool IsText;
    }

    /// <summary>
    /// A compression method of a binary GET argument of <see cref="ApiIoInfo"/>.
    /// </summary>
    public sealed class ApiIoGetCompression
    {
        /// <summary>
        /// The type character after "_" (lower case: the data is json; upper case: followed by a serializer extension and ",").
        /// </summary>
        public String Char;

        /// <summary>
        /// The HTTP code of the compression method, null for uncompressed data.
        /// </summary>
        public String HttpCode;
    }
}
