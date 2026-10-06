using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Configuration for an <see cref="ApiHttpServerModule"/> (typically supplied through the API server micro service parameters).
    /// </summary>
    public class ApiHttpServerModuleParams
    {
        /// <inheritdoc/>
        public override string ToString() => String.Concat(
            nameof(DefaultSerializer), ": ", DefaultSerializer.ToQuoted(), ", ",
            nameof(Root), ": ", Root.ToQuoted(), ", ",
            nameof(Auth), ": ", Auth.ToQuoted()
            );

        /// <summary>
        /// The name of the default serializer, used for output when the Accept header doesn't match any enabled serializer
        /// (or is missing), and for input when no format can be determined.
        /// Null or empty means "json"; if that isn't registered either, the first registered serializer is used.
        /// The default serializer is always enabled for both input and output.
        /// </summary>
        public String DefaultSerializer;

        /// <summary>
        /// The root url path that all API urls are placed under (e.g. "Api" gives "Api/[type url]/[method]").
        /// </summary>
        public String Root = "Api";

        /// <summary>
        /// Default auth required for API's that don't specify any auth themselves; a <see cref="SysWeaver.MicroService.WebApiAuthAttribute"/> on the method
        /// (or else on the type) takes precedence, and a run-time override supplied by the service instance takes precedence over all.
        /// Null = no auth required, "" = any logged in user, else a comma separated list of tokens where at least one is required.
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
        /// Can be overridden per method using <see cref="SysWeaver.MicroService.WebApiCompressionAttribute"/>.
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
        /// Enable performance monitoring of API calls (see <see cref="ApiHttpServerModule.PerfMon"/>).
        /// </summary>
        public bool PerMon = true;

        /// <summary>
        /// Comma separated names of the serializers accepted for API input (unknown or unregistered names are ignored).
        /// The default serializer is always added.
        /// </summary>
        public String InputSerializers = "json, xml, proto, bson";

        /// <summary>
        /// Comma separated names of the serializers that can be used for API output, selected using the request Accept header
        /// (unknown or unregistered names are ignored). The default serializer is always added.
        /// </summary>
        public String OutputSerializers = "json, xml, proto, bson";
    }





}
