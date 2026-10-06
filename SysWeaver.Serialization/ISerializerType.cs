using System;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// A complete serializer implementation (both serialization and deserialization) that can be registered in <see cref="SerManager"/>.
    /// </summary>
    /// <remarks>
    /// Implementations are stateless singletons exposed through a static <c>Instance</c> field and must be thread safe.
    /// The service manager recognises types implementing this interface in a manifest and calls their public static parameterless <c>Register</c> method instead of creating an instance.
    /// </remarks>
    public interface ISerializerType : ISerializer, IDeserializer
    {
        /// <summary>
        /// Register the implementing serializer in <see cref="SerManager"/> (implementations call <see cref="SerManager.AddType(ISerializerType)"/> with their singleton instance).
        /// </summary>
        /// <exception cref="NotImplementedException">The default implementation always throws, implementing types must provide their own static <c>Register</c> method.</exception>
        static virtual void Register() => throw new NotImplementedException();
    }

    /// <summary>
    /// A text based serializer type, that in addition to binary (UTF-8) data can serialize to and deserialize from strings.
    /// </summary>
    public interface ITextSerializerType : ISerializerType, ITextSerializer, ITextDeserializer
    {
    }


}
