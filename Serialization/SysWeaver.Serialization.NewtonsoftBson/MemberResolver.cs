using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Reflection;

namespace SysWeaver.Serialization.NewtonsoftBson
{
    /// <summary>
    /// Newtonsoft contract resolver used for BSON serialization: only writable members (non read-only fields and properties with a setter) are serialized,
    /// and collection items use <see cref="TypeNameHandling.Auto"/> by default.
    /// </summary>
    sealed class MemberResolver : DefaultContractResolver
    {
        /// <summary>
        /// The shared instance (contract resolvers cache contracts, so share a single instance).
        /// </summary>
        public static readonly MemberResolver Instance = new MemberResolver();

        /// <summary>
        /// Creates the property as the default resolver does, but read-only fields and properties without a setter are never serialized.
        /// </summary>
        /// <param name="member">The field or property.</param>
        /// <param name="memberSerialization">The member serialization mode of the declaring type.</param>
        /// <returns>The property, with <see cref="JsonProperty.ShouldSerialize"/> returning false for read-only members.</returns>
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


        /// <summary>
        /// Creates the contract as the default resolver does, but container contracts (collections and dictionaries) default to
        /// <see cref="TypeNameHandling.Auto"/> for their items (when not set by attributes).
        /// </summary>
        /// <param name="objectType">The type to create a contract for.</param>
        /// <returns>The contract.</returns>
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
