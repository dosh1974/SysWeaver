using ProtoBuf.Meta;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace SysWeaver.Serialization.ProtobufNet
{

    /// <summary>
    /// Adds types to a protobuf-net <see cref="RuntimeTypeModel"/> without requiring protobuf attributes.
    /// </summary>
    /// <remarks>
    /// All instance fields (public and non-public, as returned by <see cref="Type.GetFields(BindingFlags)"/>) are added in reflection order (field numbers 1, 2, ..),
    /// base classes are added with the derived types as sub types (field numbers 500 + discovery index), and field and generic argument types are added recursively.
    /// The numbering depends on the order in which types are discovered, so it is only stable within a process.
    /// Built types are cached in a static dictionary (shared by all models).
    /// </remarks>
    static class SerializerBuilder
    {
        const BindingFlags Flags = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly Dictionary<Type, HashSet<Type>> SubTypes = new Dictionary<Type, HashSet<Type>>();
        static readonly ConcurrentDictionary<Type, int> BuiltTypes = new ConcurrentDictionary<Type, int>();
        static readonly Type ObjectType = typeof(object);
        static readonly Object BuildLock = new Object();

        /// <summary>
        /// Add <typeparamref name="T"/> (and all types it depends on) to the model, if not already done.
        /// </summary>
        /// <typeparam name="T">The type to build the serializer for.</typeparam>
        /// <param name="model">The model to update.</param>
        public static void Build<T>(RuntimeTypeModel model)
        {
            var type = typeof(T);
            Build(type, model);
        }

        /// <summary>
        /// Add <typeparamref name="T"/> and the runtime type of <paramref name="data"/> (if different) to the model.
        /// </summary>
        /// <typeparam name="T">The static type to build the serializer for.</typeparam>
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
        /// Add a type (and all types it depends on) to the model, if not already done.
        /// Types the model can already serialize (primitives, strings, collections etc) are not added, but their generic arguments are.
        /// </summary>
        /// <param name="type">The type to build the serializer for.</param>
        /// <param name="model">The model to update.</param>
        public static void Build(Type type, RuntimeTypeModel model)
        {
            if (BuiltTypes.ContainsKey(type))
            {
                return;
            }

            //  One lock for all types, building a type mutates the shared SubTypes (and the base types in the model)
            lock (BuildLock)
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
        /// <returns>All public and non-public instance fields (including inherited non-private fields).</returns>
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
