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

        public static MethodInfo GetDescribe(TypeInfo typeInfo)
        {
            return typeInfo.GetDeclaredMethods("Describe").First(mi => mi.GetParameters().Length == 1);
        }
        public static MethodInfo GetLegacyDescribe(TypeInfo typeInfo)
        {
            return typeInfo.GetDeclaredMethods("Describe").First(mi => mi.GetParameters().Length == 2);
        }


    }

}

