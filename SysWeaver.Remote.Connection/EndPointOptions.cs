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
        /// The request timeout in milliseconds, 0 or less to use the connection timeout.
        /// It overrides the connection timeout (it can both shorten and extend it).
        /// </summary>
        public readonly int TimeOutInMilliSeconds;

        /// <summary>
        /// Creates the options. Changes to the signature must be reflected in the IL emitted by the remote API type generator (InterfaceTypeCache).
        /// </summary>
        /// <param name="ser">The response serializer name (ex: "json"), null or empty to use the connection default.</param>
        /// <param name="postSer">The request serializer name, null or empty to use <paramref name="ser"/> (or the connection default).</param>
        /// <param name="timeOutInMilliSeconds">The request timeout in milliseconds, 0 or less to use the connection timeout.</param>
        /// <exception cref="NotSupportedException">A serializer name isn't known (see <see cref="GetSerializer"/>).</exception>
        public EndPointOptions(String ser, String postSer, int timeOutInMilliSeconds)
        {
            var s = String.IsNullOrEmpty(ser) ? null : GetSerializer(ser);
            Ser = s;
            PostSer = String.IsNullOrEmpty(postSer) ? s : GetSerializer(postSer);
            TimeOutInMilliSeconds = timeOutInMilliSeconds;
        }

        /// <summary>
        /// Get a serializer by name: <see cref="RemoteParam.FormUrlSerializer"/> and <see cref="RemoteParam.FormUrlIgnoreDefaultsSerializer"/> (case insensitive)
        /// return the form url serializers (no registration needed), anything else is looked up using <see cref="SerManager.Get(string)"/>.
        /// </summary>
        /// <param name="name">The serializer name (file extension), ex: "json".</param>
        /// <returns>The serializer, never null.</returns>
        /// <exception cref="NotSupportedException">No serializer with that name is registered.</exception>
        internal static ISerializerType GetSerializer(String name)
        {
            if (String.Equals(name, RemoteParam.FormUrlSerializer, StringComparison.OrdinalIgnoreCase))
                return FormUrlSerializer.Instance;
            if (String.Equals(name, RemoteParam.FormUrlIgnoreDefaultsSerializer, StringComparison.OrdinalIgnoreCase))
                return IgnoreDefaultsFormUrlSerializer.Instance;
            return SerManager.Get(name) ?? throw new NotSupportedException("Unknown serializer \"" + name + "\"!");
        }
    }

}
