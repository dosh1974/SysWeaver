using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Reflection;

namespace SysWeaver.Serialization.NewtonsoftBson
{
    sealed class MemberResolver : DefaultContractResolver
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
}
