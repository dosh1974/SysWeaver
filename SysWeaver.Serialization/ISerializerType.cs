using System;

namespace SysWeaver.Serialization
{
    public interface ISerializerType : ISerializer, IDeserializer
    {
        static virtual void Register() => throw new NotImplementedException();
    }

    public interface ITextSerializerType : ISerializerType, ITextSerializer, ITextDeserializer
    {
    }


}
