using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SysWeaver.Inspection.Implementation
{

    /// <summary>
    /// Helpers used by the type handlers and inspector implementations
    /// </summary>
    public static class StaticTypeHandler
    {
        /// <summary>
        /// Add context values to the <see cref="IInspector.Context"/> of an inspector
        /// </summary>
        /// <param name="inspector">The inspector</param>
        /// <param name="contexts">The values to add, may be null (nothing is added)</param>
        /// <exception cref="ArgumentNullException"><paramref name="inspector"/> is null (DEBUG builds only)</exception>
        /// <exception cref="ArgumentException">A key is already present in the context</exception>
        public static void AddContexts(IInspector inspector, IEnumerable<KeyValuePair<String, Object>> contexts)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(inspector);
#endif//DEBUG
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

        /// <summary>
        /// Get the name of a type (cached), the names can be resolved using TypeFinder
        /// </summary>
        /// <param name="type">The type</param>
        /// <param name="q">How the name (and the names of generic arguments) are qualified</param>
        /// <returns>The type name</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is null (DEBUG builds only)</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="q"/> isn't a defined value (DEBUG builds only)</exception>
        public static String GetTypename(Type type, TypenameQualifications q)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(type);
            if (((uint)q) > (uint)TypenameQualifications.Full)
                throw new ArgumentOutOfRangeException(nameof(q), q, "Unknown qualification");
#endif//DEBUG
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

        /// <summary>
        /// Throw the exception for data that was written by a newer version of a type than the version known by this software
        /// </summary>
        /// <param name="version">The version of the data</param>
        /// <param name="currentVersion">The latest version known (<see cref="TypeHandler{T}.LatestVersion"/>)</param>
        /// <param name="type">The type</param>
        /// <exception cref="InvalidDataException">Always</exception>
        public static void ThrowInvalidVersion(int version, int currentVersion, Type type)
        {
            throw new InvalidDataException("Invalid version " + version + " for type " + type.FullName + ", this software only recognizes versions up to " + currentVersion + "\nPlease upgrade software!");
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

        /// <summary>
        /// The number of type handlers created so far (each <see cref="TypeHandler{T}"/> gets a unique <see cref="TypeHandler{T}.TypeIndex"/>)
        /// </summary>
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

        /// <summary>
        /// Get an expression that calls the IInspector.Field method for a type (the typed overload if there is one, else the generic Field&lt;T&gt;)
        /// </summary>
        /// <param name="t">The type of the value (or a by ref type)</param>
        /// <param name="inspector">The inspector expression</param>
        /// <param name="valueByRef">The value expression (passed by reference: a parameter, variable, field or array element)</param>
        /// <returns>The call expression</returns>
        public static Expression GetRegFieldExpression(Type t, Expression inspector, Expression valueByRef)
        {
            return Expression.Call(inspector, InspectorInfo<IInspector>.GetRegFieldMethod(t), valueByRef);
        }

        /// <summary>
        /// Implements IInspector.UnmanagedMemory by inspecting the memory as a byte array (null if <paramref name="data"/> is zero).
        /// If the inspector assigns a new array (when reading), <paramref name="disposeAction"/> is called, and the memory is replaced by the pinned array
        /// (<paramref name="disposeAction"/> is set to an action that unpins it), or by zero / 0 / null if null was read.
        /// </summary>
        /// <param name="i">The inspector</param>
        /// <param name="data">The memory, may be zero</param>
        /// <param name="length">The number of bytes</param>
        /// <param name="disposeAction">The action that frees the memory, may be null</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative and <paramref name="data"/> isn't zero (DEBUG builds only)</exception>
        public static void HandleUnmanagedMemory(IInspector i, ref IntPtr data, ref int length, ref Action disposeAction)
        {
            Byte[] d = null;
            if (data != IntPtr.Zero)
            {
#if DEBUG
                ArgumentOutOfRangeException.ThrowIfNegative(length);
#endif//DEBUG
                d = GC.AllocateUninitializedArray<Byte>(length);
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

