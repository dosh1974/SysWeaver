using System;

namespace SysWeaver.Serialization
{
    public interface IDeserializer : ISerializerInfo
    {
        T Create<T>(ReadOnlyMemory<Byte> data);

        T Create<T>(ReadOnlySpan<Byte> data);
    }

    public interface ITextDeserializer : IDeserializer
    {
        T FromString<T>(ReadOnlySpan<Char> text);
        T FromString<T>(String text);

    }

}
