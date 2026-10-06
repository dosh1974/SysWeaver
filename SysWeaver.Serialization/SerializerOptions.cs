namespace SysWeaver.Serialization
{
    /// <summary>
    /// Output style requested from a serializer.
    /// </summary>
    /// <remarks>
    /// The value is a hint: each implementation interprets it differently and many ignore it completely
    /// (MessagePack, Protobuf-Net, SysWeaver.Binary, SysWeaver.Json, SpanJson and Utf8Json always produce the same output).
    /// Several implementations use the underlying integer value as an index into per-option settings arrays, so only the defined values are valid.
    /// </remarks>
    public enum SerializerOptions
    {
        /// <summary>
        /// Compact output intended for transport and storage (no indentation).
        /// Newtonsoft based serializers emit type information only where the runtime type differs from the declared type.
        /// </summary>
        Compact = 0,
        /// <summary>
        /// Human readable output (indented where supported).
        /// Newtonsoft based serializers emit type information for every object.
        /// </summary>
        Verbose,
        /// <summary>
        /// Compact output without any type information (Newtonsoft based serializers), other implementations treat this like <see cref="Compact"/>.
        /// </summary>
        Typeless,
    };
}
