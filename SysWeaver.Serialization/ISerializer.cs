using System;

namespace SysWeaver.Serialization
{
    public interface ISerializer : ISerializerInfo
    {
        ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact);
    }


    public interface ITextSerializer : ISerializer
    {
        String ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact);
    }

}
