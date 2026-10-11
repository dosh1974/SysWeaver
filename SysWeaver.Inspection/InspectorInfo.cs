using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace SysWeaver.Inspection.Implementation
{
    /// <summary>
    /// Reflection info about the Field / Prop methods of an inspector type (used to compile the type handlers, normally with I = <see cref="IInspector"/>)
    /// </summary>
    /// <typeparam name="I">The inspector type</typeparam>
    public static class InspectorInfo<I> where I : IInspector
    {
        public static readonly TypeInfo InspectorTypeInfo = typeof(I).GetTypeInfo();

        #region Field

        public static readonly IEnumerable<MethodInfo> AllFieldMethods = InspectorTypeInfo.AllMethods().Where(mi => (mi.Name == "Field") && (mi.GetParameters().Length == 1) && (mi.GetParameters()[0].ParameterType.IsByRef));
        public static readonly MethodInfo GenericRegField = AllFieldMethods.FirstOrDefault(mi => mi.IsGenericMethodDefinition);

        static ConcurrentDictionary<Type, MethodInfo> GetFields()
        {
            var r = new ConcurrentDictionary<Type, MethodInfo>();
            foreach (var mi in AllFieldMethods)
            {
                var p = mi.GetParameters();
                if (p.Length != 1)
                    continue;
                var type = p[0].ParameterType;
                if (!type.IsByRef)
                    continue;
                type = type.GetElementType();
                r[type] = mi;
            }
            return r;
        }

        static readonly ConcurrentDictionary<Type, MethodInfo> Fields = GetFields();

        /// <summary>
        /// Get the Field(ref T) method for a type: the typed overload if there is one, else the generic Field&lt;T&gt; made for the type
        /// </summary>
        /// <param name="t">The type (a by ref type is the same as its element type)</param>
        /// <returns>The method</returns>
        public static MethodInfo GetRegFieldMethod(Type t)
        {
            t = t.IsByRef ? t.GetElementType() : t;
            MethodInfo mi;
            if (!Fields.TryGetValue(t, out mi))
            {
                mi = GenericRegField.MakeGenericMethod(t);
                Fields.TryAdd(t, mi);
            }
            return mi;
        }
        /// <summary>
        /// Get the Field(ref T) method for a type
        /// </summary>
        /// <param name="t">The type (a by ref type is the same as its element type)</param>
        /// <param name="allowGeneric">False to return null instead of the generic Field&lt;T&gt; (when there is no typed overload)</param>
        /// <returns>The method or null</returns>
        public static MethodInfo GetRegFieldMethod(Type t, bool allowGeneric)
        {
            MethodInfo mi = GetRegFieldMethod(t);
            return ((!mi.IsGenericMethod) || allowGeneric) ? mi : null;
        }

        #endregion//Field

        #region Prop

        public static readonly IEnumerable<MethodInfo> AllPropMethods = InspectorTypeInfo.AllMethods().Where(mi => (mi.Name == "Prop") && (mi.GetParameters().Length == 2) && (!mi.GetParameters()[0].ParameterType.IsByRef) && (mi.GetParameters()[1].ParameterType == typeof(SetProp<>).MakeGenericType(mi.GetParameters()[0].ParameterType)));
        public static readonly MethodInfo GenericRegProp = AllPropMethods.FirstOrDefault(mi => mi.IsGenericMethodDefinition);

        private static ConcurrentDictionary<Type, MethodInfo> GetProps()
        {
            var r = new ConcurrentDictionary<Type, MethodInfo>();
            //  Prop(T value, SetProp<T> setValue), the value is passed by value, the generic method is the fallback (see GetRegPropMethod)
            foreach (var mi in AllPropMethods)
            {
                if (mi.IsGenericMethodDefinition)
                    continue;
                var p = mi.GetParameters();
                if (p.Length != 2)
                    continue;
                var type = p[0].ParameterType;
                if (type.IsByRef)
                    continue;
                r[type] = mi;
            }
            return r;
        }

        private static readonly ConcurrentDictionary<Type, MethodInfo> Props = GetProps();

        /// <summary>
        /// Get the Prop(T, SetProp&lt;T&gt;) method for a type: the typed overload if there is one, else the generic Prop&lt;T&gt; made for the type
        /// </summary>
        /// <param name="t">The type (a by ref type is the same as its element type)</param>
        /// <returns>The method</returns>
        public static MethodInfo GetRegPropMethod(Type t)
        {
            t = t.IsByRef ? t.GetElementType() : t;
            MethodInfo mi;
            if (!Props.TryGetValue(t, out mi))
            {
                mi = GenericRegProp.MakeGenericMethod(t);
                Props.TryAdd(t, mi);
            }
            return mi;
        }
        /// <summary>
        /// Get the Prop(T, SetProp&lt;T&gt;) method for a type
        /// </summary>
        /// <param name="t">The type (a by ref type is the same as its element type)</param>
        /// <param name="allowGeneric">False to return null instead of the generic Prop&lt;T&gt; (when there is no typed overload)</param>
        /// <returns>The method or null</returns>
        public static MethodInfo GetRegPropMethod(Type t, bool allowGeneric)
        {
            MethodInfo mi = GetRegPropMethod(t);
            return ((!mi.IsGenericMethod) || allowGeneric) ? mi : null;
        }

        #endregion//Prop


        public static readonly MethodInfo OnNew = InspectorTypeInfo.GetDeclaredMethod("OnNew");

 

    }

}

