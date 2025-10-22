using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Put this attribute on a type or method to specify the request response compression
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebApiCompressionAttribute : Attribute
    {

        /// <summary>
        /// Put this attribute on a type or method to specify the request response compression methods and priority
        /// </summary>
        /// <param name="compression">Compresson method and level in the desired priority, ex: "br:Fast, deflate:Balanced".
        /// Supported compressors:
        ///     br = Best overall.
        ///     deflate = Wide support.
        ///     gzip = Wider suppor, same as deflate but extra headers and performance overhead.
        /// Compression levels:
        ///     Fast = Best performance (typically use for small data).
        ///     Balanced = Better compression (typically use for larger data).
        ///     Best = Best compression, often to slow for on the fly.
        /// </param>
        public WebApiCompressionAttribute(String compression)
        {
            Compression = compression;
        }

        /// <summary>
        /// Compresson method and level in the desired priority, ex: "br:Fast, deflate:Balanced".
        /// Supported compressors:
        ///     br = Best overall.
        ///     deflate = Wide support.
        ///     gzip = Wider suppor, same as deflate but extra headers and performance overhead.
        /// Compression levels:
        ///     Fast = Best performance (typically use for small data).
        ///     Balanced = Better compression (typically use for larger data).
        ///     Best = Best compression, often to slow for on the fly.
        /// </summary>
        public readonly String Compression;


        
    }

    public static class WebApiCompress
    {
        public const String Best = "br:Best,deflate:Best,gzip:Best";
        public const String Balanced = "br:Balanced,deflate:Balanced,gzip:Balanced";
        public const String Fast = "br:Fast,deflate:Fast,gzip:Fast";
    }

}
