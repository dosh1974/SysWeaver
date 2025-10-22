using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Reflection;

namespace SysWeaver.Serialization.NewtonsoftJson
{
    public sealed class MemberResolver : DefaultContractResolver
    {
        public static readonly MemberResolver Instance = new MemberResolver();

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    if (!((member as FieldInfo).IsInitOnly))
                        return property;
                    break;
                case MemberTypes.Property:
                    if ((member as PropertyInfo).CanWrite)
                        return property;
                    break;
                default:
                    return property;
            }
            property.ShouldSerialize = x => false;
            return property;
        }


        protected override JsonContract CreateContract(Type objectType)
        {
            var contract = base.CreateContract(objectType);
            var containerContract = contract as JsonContainerContract;
            if (containerContract != null)
            {
                if (containerContract.ItemTypeNameHandling == null)
                    containerContract.ItemTypeNameHandling = TypeNameHandling.Auto;
            }
            return contract;
        }

    }

    sealed class SerializationBinder : ISerializationBinder
    {

        public static readonly ISerializationBinder Instance = new SerializationBinder();

        SerializationBinder()
        {
            Def = new DefaultSerializationBinder();
        }

        readonly ISerializationBinder Def;

        public void BindToName(Type serializedType, out string assemblyName, out string typeName) 
            => Def.BindToName(serializedType, out assemblyName, out typeName);
        
        public Type BindToType(string assemblyName, string typeName)
        {
            return TypeNameResolver.Get(String.Join(", ", typeName, assemblyName));
/*            var d = Def.BindToType(assemblyName, typeName);
            if (d != null)
                return d;
            return d;*/
        }
    }

}
