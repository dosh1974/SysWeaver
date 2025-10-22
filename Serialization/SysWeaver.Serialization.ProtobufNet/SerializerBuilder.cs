using ProtoBuf.Meta;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace SysWeaver.Serialization.ProtobufNet
{

    static class SerializerBuilder
    {
        const BindingFlags Flags = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly Dictionary<Type, HashSet<Type>> SubTypes = new Dictionary<Type, HashSet<Type>>();
        static readonly ConcurrentDictionary<Type, int> BuiltTypes = new ConcurrentDictionary<Type, int>();
        static readonly Type ObjectType = typeof(object);

        /// <summary>
        /// Build the ProtoBuf serializer from the generic <see cref="Type">type</see>.
        /// </summary>
        /// <typeparam name="T">The type of build the serializer for.</typeparam>
        public static void Build<T>(RuntimeTypeModel model)
        {
            var type = typeof(T);
            Build(type, model);
        }

        /// <summary>
        /// Build the ProtoBuf serializer from the data's <see cref="Type">type</see>.
        /// </summary>
        /// <typeparam name="T">The type of build the serializer for.</typeparam>
        /// <param name="data">The data who's type a serializer will be made.</param>
        /// <param name="model">The model to update.</param>
        // ReSharper disable once UnusedParameter.Global
        public static void Build<T>(T data, RuntimeTypeModel model)
        {
            Build<T>(model);
            if (data != null)
            {
                var dt = data.GetType();
                if (dt != typeof(T))
                    Build(dt, model);
            }
        }

        /// <summary>
        /// Build the ProtoBuf serializer for the <see cref="Type">type</see>.
        /// </summary>
        /// <param name="type">The type of build the serializer for.</param>
        /// <param name="model">The model to update.</param>
        public static void Build(Type type, RuntimeTypeModel model)
        {
            if (BuiltTypes.ContainsKey(type))
            {
                return;
            }

            lock (type)
            {
                if (model.CanSerialize(type))
                {
                    if (type.IsGenericType)
                    {
                        BuildGenerics(type, model);
                    }

                    return;
                }

                var meta = model.Add(type, false);
                var fields = GetFields(type);

                meta.Add(fields.Select(m => m.Name).ToArray());
                meta.UseConstructor = false;

                BuildBaseClasses(type, model);
                BuildGenerics(type, model);

                foreach (var memberType in fields.Select(f => f.FieldType).Where(t => !t.IsPrimitive))
                {
                    Build(memberType, model);
                }

                BuiltTypes.TryAdd(type, 0);
            }
        }

        /// <summary>
        /// Gets the fields for a type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        static FieldInfo[] GetFields(Type type)
        {
            return type.GetFields(Flags);
        }

        /// <summary>
        /// Builds the base class serializers for a type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="model">The model to update.</param>
        static void BuildBaseClasses(Type type, RuntimeTypeModel model)
        {
            var baseType = type.BaseType;
            var inheritingType = type;


            while (baseType != null && baseType != ObjectType)
            {
                HashSet<Type> baseTypeEntry;

                if (!SubTypes.TryGetValue(baseType, out baseTypeEntry))
                {
                    baseTypeEntry = new HashSet<Type>();
                    SubTypes.Add(baseType, baseTypeEntry);
                }

                if (!baseTypeEntry.Contains(inheritingType))
                {
                    Build(baseType, model);
                    model[baseType].AddSubType(baseTypeEntry.Count + 500, inheritingType);
                    baseTypeEntry.Add(inheritingType);
                }

                inheritingType = baseType;
                baseType = baseType.BaseType;
            }
        }

        /// <summary>
        /// Builds the serializers for the generic parameters for a given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="model">The model to update.</param>
        static void BuildGenerics(Type type, RuntimeTypeModel model)
        {
            if (type.IsGenericType || (type.BaseType != null && type.BaseType.IsGenericType))
            {
                var generics = type.IsGenericType ? type.GetGenericArguments() : type.BaseType.GetGenericArguments();

                foreach (var generic in generics)
                {
                    Build(generic, model);
                }
            }
        }
    }


}
