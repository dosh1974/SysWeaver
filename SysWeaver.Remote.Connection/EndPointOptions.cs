using SysWeaver.Serialization;
using System;

namespace SysWeaver.Remote
{
    /// <summary>
    /// Per end point overrides (serializers and timeout) built from method attributes (<see cref="RemoteSerializerAttribute"/>, <see cref="RemoteTimeoutAttribute"/>).
    /// Instances are created by the static constructor of the generated remote API class and shared between end points with identical settings.
    /// </summary>
    public sealed class EndPointOptions
    {
        /// <summary>
        /// The serializer used to decode responses, null to use the connection default.
        /// </summary>
        public readonly ISerializerType Ser;
        /// <summary>
        /// The serializer used to encode request payloads, null to use the connection default (defaults to <see cref="Ser"/> if only that is specified).
        /// </summary>
        public readonly ISerializerType PostSer;
        /// <summary>
        /// The request timeout in milliseconds, 0 or less to use the connection timeout. Can't exceed the connection timeout.
        /// </summary>
        public readonly int TimeOutInMilliSeconds;

        /// <summary>
        /// Creates the options. Changes to the signature must be reflected in the IL emitted by the remote API type generator (InterfaceTypeCache).
        /// </summary>
        /// <param name="ser">The response serializer name (ex: "json"), null or empty to use the connection default.</param>
        /// <param name="postSer">The request serializer name, null or empty to use <paramref name="ser"/> (or the connection default).</param>
        /// <param name="timeOutInMilliSeconds">The request timeout in milliseconds, 0 or less to use the connection timeout.</param>
        public EndPointOptions(String ser, String postSer, int timeOutInMilliSeconds)
        {
            var s = String.IsNullOrEmpty(ser) ? null : SerManager.Get(ser);
            Ser = s;
            PostSer = String.IsNullOrEmpty(postSer) ? s : SerManager.Get(postSer);
            TimeOutInMilliSeconds = timeOutInMilliSeconds;
        }
    }

}
