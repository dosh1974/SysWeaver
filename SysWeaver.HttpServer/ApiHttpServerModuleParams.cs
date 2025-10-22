using System;

namespace SysWeaver.Net
{
    public class ApiHttpServerModuleParams
    {
        public override string ToString() => String.Concat(
            nameof(DefaultSerializer), ": ", DefaultSerializer.ToQuoted(), ", ",
            nameof(Root), ": ", Root.ToQuoted(), ", ",
            nameof(Auth), ": ", Auth.ToQuoted()
            );

        /// <summary>
        /// The default serializer to use (when no accept header is supplied)
        /// </summary>
        public String DefaultSerializer;

        /// <summary>
        /// The root url prefix to use for all request
        /// </summary>
        public String Root = "Api";

        /// <summary>
        /// Auth required for any request, can be overridden by the Type and Method
        /// </summary>
        public String Auth;

        /// <summary>
        /// Default API compression specifying order of preference and quality.
        /// Supported compressors:
        ///     br = Best overall.
        ///     deflate = Wide support.
        ///     gzip = Wider support, same as deflate but extra headers and performance overhead.
        /// Compression levels:
        ///     Fast = Best performance (typically use for small data).
        ///     Balanced = Better compression (typically use for larger data).
        ///     Best = Best compression, often to slow for on the fly.
        /// </summary>
        public String Compression = "br:Balanced, deflate:Balanced, gzip:Balanced";
        
        /// <summary>
        /// Default API compression for methods that are cached server side, typically can have better compression, specifying order of preference and quality.
        /// Supported compressors:
        ///     br = Best overall.
        ///     deflate = Wide support.
        ///     gzip = Wider support, same as deflate but extra headers and performance overhead.
        /// Compression levels:
        ///     Fast = Best performance (typically use for small data).
        ///     Balanced = Better compression (typically use for larger data).
        ///     Best = Best compression, often to slow for on the fly.
        /// </summary>
        public String CachedCompression = "br:Best, deflate:Best, gzip:Best";

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool PerMon = true;

        /// <summary>
        /// The supported input serializers
        /// </summary>
        public String InputSerializers = "json, xml, proto, bson";

        /// <summary>
        /// The supporteded output serializers
        /// </summary>
        public String OutputSerializers = "json, xml, proto, bson";
    }





}
