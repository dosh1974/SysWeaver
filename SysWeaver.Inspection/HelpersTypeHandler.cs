using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SysWeaver.Inspection.Implementation
{
    static class HelpersTypeHandler
    {
        public static readonly Type inspectorHandlerType = typeof(IInspectorImplementation);
        public static readonly ParameterExpression inspectorParameter = Expression.Parameter(inspectorHandlerType, "inspector");
        public static readonly ConstantExpression falseConstant = Expression.Constant(false);
        public static readonly ParameterExpression versionParameter = Expression.Parameter(typeof(int), "version");
        public static readonly ParameterExpression isLatestVersionParameter = Expression.Parameter(typeof(bool), "isLatestVersion");


        public static bool IsNullable(Type type)
        {
            var ti = type.GetTypeInfo();
            if (!ti.IsValueType)
                return true;
            return ti.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static readonly Type DescribableType = typeof(IDescribable);
        public static readonly TypeInfo DescribableTypeInfo = DescribableType.GetTypeInfo();

        static readonly Type[] DescribeParams = [typeof(IInspector)];
        static readonly Type[] LegacyDescribeParams = [typeof(IInspector), typeof(int)];

        /// <summary>
        /// Get the IDescribable.Describe(IInspector) implementation of a type (see <see cref="FindDescribe"/>)
        /// </summary>
        public static MethodInfo GetDescribe(TypeInfo typeInfo) => FindDescribe(typeInfo, DescribeParams);

        /// <summary>
        /// Get the IDescribable.Describe(IInspector, int) implementation of a type (see <see cref="FindDescribe"/>)
        /// </summary>
        public static MethodInfo GetLegacyDescribe(TypeInfo typeInfo) => FindDescribe(typeInfo, LegacyDescribeParams);

        /// <summary>
        /// Find the Describe method to call for a type that implements <see cref="IDescribable"/>.
        /// The type and its base types are searched, most-derived first: the first type that declares an instance "Describe" method with the parameters,
        /// or that holds the interface implementation (also an explicit or inherited implementation, found with the interface map) wins.
        /// So a Describe declared by a derived class (also one that hides a base method) is used, and inherited / explicit implementations are found (inspection-cov-13).
        /// For an interface type the interface method itself is returned (called virtually).
        /// </summary>
        /// <param name="typeInfo">The type</param>
        /// <param name="parameters">The parameter types of the method</param>
        /// <returns>The method</returns>
        /// <exception cref="MissingMethodException">No implementation is found (the type doesn't implement IDescribable)</exception>
        static MethodInfo FindDescribe(TypeInfo typeInfo, Type[] parameters)
        {
            var type = typeInfo.AsType();
            var interfaceMethod = DescribableType.GetMethod("Describe", parameters);
            if (type.IsInterface)
                return interfaceMethod;
            MethodInfo target = null;
            if (DescribableType.IsAssignableFrom(type))
            {
                var map = type.GetInterfaceMap(DescribableType);
                var im = map.InterfaceMethods;
                for (int i = 0; i < im.Length; ++i)
                {
                    if (im[i] == interfaceMethod)
                    {
                        target = map.TargetMethods[i];
                        break;
                    }
                }
            }
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            for (var t = type; t != null; t = t.BaseType)
            {
                var m = t.GetMethod("Describe", flags, null, parameters, null);
                if (m != null)
                    return m;
                if ((target != null) && (target.DeclaringType == t))
                    return target;
            }
            return target ?? throw new MissingMethodException(type.FullName, "Describe");
        }


    }

}

