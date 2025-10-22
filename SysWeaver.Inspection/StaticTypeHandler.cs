using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SysWeaver.Inspection.Implementation
{

    public static class StaticTypeHandler
    {
        public static void AddContexts(IInspector inspector, IEnumerable<KeyValuePair<String, Object>> contexts)
        {
            if (contexts == null)
                return;
            var cc = inspector.Context;
            foreach (var c in contexts)
                cc.Add(c.Key, c.Value);
        }

        sealed class CachedTypename
        {
            public CachedTypename(Type type)
            {
                String n;
                if (type.IsNested)
                    n = String.Join("+", InternalGetTypename(type.DeclaringType).Names[0], type.Name);
                else
                {
                    if (String.IsNullOrEmpty(type.Namespace))
                        n = type.Name;
                    else
                        n = String.Concat(type.Namespace, (type.Namespace.Length > 0 ? "." : ""), type.Name);
                }
                var typeInfo = type.GetTypeInfo();
                if (typeInfo.IsGenericType)
                {
                    var x = typeInfo.GenericTypeArguments;
                    n += '[';
                    Names[0] = n;
                    Names[1] = n;
                    String[] gena = new string[x.Length];
                    for (int i = 0; i < x.Length; ++ i)
                    {
                        var q = InternalGetTypename(x[i]);
                        if (i != 0)
                        {
                            Names[0] += ',';
                            Names[1] += ',';
                        }
                        Names[0] += '[';
                        Names[1] += '[';
                        Names[0] += q.Names[0];
                        Names[1] += q.Names[1];
                        Names[0] += ']';
                        Names[1] += ']';
                    }
                    Names[0] += ']';
                    Names[1] += ']';
                    Names[1] += ", " + typeInfo.Assembly.FullName.Split(',')[0].Trim();
                    Names[2] = type.AssemblyQualifiedName;
                    return;
                }
                Names[0] = n;
                Names[1] = String.Join(", ", n , typeInfo.Assembly.FullName.Split(',')[0].Trim());
                Names[2] = type.AssemblyQualifiedName;
            }
            public readonly String[] Names = new String[3];
        }
        static readonly ConcurrentDictionary<Type, CachedTypename> Typenames = new ConcurrentDictionary<Type, CachedTypename>();

        public static String GetTypename(Type type, TypenameQualifications q)
        {
            CachedTypename t;
            if (Typenames.TryGetValue(type, out t))
                return t.Names[(int)q];
            t = new CachedTypename(type);
            Typenames[type] = t;
            return t.Names[(int)q];
        }
        
        static CachedTypename InternalGetTypename(Type type)
        {
            CachedTypename t;
            if (Typenames.TryGetValue(type, out t))
                return t;
            t = new CachedTypename(type);
            Typenames[type] = t;
            return t;
        }


        /*
        static StaticTypeHandler()
        {
            TypeHandlerCache<Boolean>.GetHandler();
            TypeHandlerCache<Byte>.GetHandler();
            TypeHandlerCache<Char>.GetHandler();
            TypeHandlerCache<Decimal>.GetHandler();
            TypeHandlerCache<Double>.GetHandler();
            TypeHandlerCache<Int16>.GetHandler();
            TypeHandlerCache<Int32>.GetHandler();
            TypeHandlerCache<Int64>.GetHandler();
            TypeHandlerCache<SByte>.GetHandler();
            TypeHandlerCache<Single>.GetHandler();
            TypeHandlerCache<String>.GetHandler();
            TypeHandlerCache<UInt16>.GetHandler();
            TypeHandlerCache<UInt32>.GetHandler();
            TypeHandlerCache<UInt64>.GetHandler();
        }
        */

        public static void ThrowInvalidVersion(int version, int currentVersion, Type type)
        {
            throw new Exception("Invalid version " + version + " for type " + type.FullName + ", this software only recognizes versions up to " + currentVersion + "\nPlease upgrade software!");
        }

        internal static readonly Type[] DescCurrent = new Type[]
        {
            typeof(IInspector)
        };
        internal static readonly Type[] DescLegacy = new Type[]
        {
            typeof(IInspector), typeof(int)
        };

        internal static readonly Type[] ConstructDescriptor = new Type[]
        {
            typeof(IInspector), typeof(int)
        };

        internal static readonly Type[] ConstructDescriptor2 = new Type[]
        {
            typeof(IInspector), typeof(int), typeof(bool)
        };

        internal static readonly Type[] Empty = new Type[0];

        public static int CurrentTypeCount
        {
            get
            {
                return Interlocked.Add(ref TypeCount, 0);
            }
        }

        internal static int TypeCount;

        public static MethodInfo SimpleRegField = typeof(StaticTypeHandler).GetTypeInfo().GetDeclaredMethods("GetSimpleRegField").First();

        internal static RegFieldDelegate<T> GetSimpleRegField<T>()
        {
            var mi = InspectorInfo<IInspector>.GetRegFieldMethod(typeof(T));
            var inspectorParameter = Expression.Parameter(typeof(IInspectorImplementation), "inspector");
            var valueParameter = Expression.Parameter(typeof(T).MakeByRefType(), "value");
            return Expression.Lambda<RegFieldDelegate<T>>(Expression.Call(inspectorParameter, mi, valueParameter), inspectorParameter, valueParameter).Compile();
        }

        public static Expression GetRegFieldExpression(Type t, Expression inspector, Expression valueByRef)
        {
            return Expression.Call(inspector, InspectorInfo<IInspector>.GetRegFieldMethod(t), valueByRef);
        }

        public static void HandleUnmanagedMemory(IInspector i, ref IntPtr data, ref int length, ref Action disposeAction)
        {
            Byte[] d = null;
            if (data != IntPtr.Zero)
            {
                d = new Byte[length];
                if (length > 0)
                    Marshal.Copy(data, d, 0, length);
            }
            var old = d;
            i.Field(ref d);
            if (old != d)
            {
                disposeAction?.Invoke();
                if (d == null)
                {
                    data = IntPtr.Zero;
                    length = 0;
                    disposeAction = null;
                }
                else
                {
                    length = d.Length;
                    var h = GCHandle.Alloc(d, GCHandleType.Pinned);
                    data = h.AddrOfPinnedObject();
                    disposeAction = () => h.Free();
                }
            }
        }

    }


    delegate void RegFieldDelegate<T>(IInspectorImplementation i, ref T value);


}

