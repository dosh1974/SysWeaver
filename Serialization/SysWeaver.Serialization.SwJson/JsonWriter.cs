using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using SysWeaver.Serialization.SwJson.Writer;

namespace SysWeaver.Serialization.SwJson
{

    /// <summary>
    /// Methods for serializing an object to UTF8 json (a byte buffer or a string).
    /// </summary>
    /// <remarks>
    /// A writer is generated (expression trees, compiled) for every type the first time it's used and cached for the lifetime of the process, so the first call for a type is slow.
    /// Supported: primitives, decimal, string, char, enums (as their numeric value), DateTime, DateOnly, TimeOnly, TimeSpan, DateTimeOffset, Guid, byte[] (base64), Nullable&lt;T&gt;,
    /// one dimensional arrays and other <see cref="ICollection{T}"/> types (json arrays), classes implementing <see cref="IEnumerable{T}"/> without any serialized members (like <see cref="Queue{T}"/>, <see cref="Stack{T}"/>,
    /// <see cref="ConcurrentQueue{T}"/> and <see cref="ConcurrentBag{T}"/>, json arrays in enumeration order),
    /// <see cref="IDictionary{TKey, TValue}"/> with string, char, integer, floating point, decimal, bool, enum, TimeSpan, DateTime, DateOnly, TimeOnly, DateTimeOffset or Guid keys (json objects, other key types are written with the key's ToString() as the property name and can't be read back),
    /// and classes / structs (json objects with the public instance fields and the public read / write properties, bounded types first, then sorted by type and name).
    /// Interfaces and abstract types are written using the runtime type of the value.
    /// When the runtime type of a value differs from the declared type, the value is written as {"$type":"name",...} (see <see cref="ToTypename"/>), except for primitive like types if typeIsOptional is true.
    /// Collections and dictionaries with type information are written as {"$type":"name","$values":[...]} and primitives as {"$type":"name","$value":value}.
    /// Numbers and dates are always formatted using the invariant culture. Float / double NaN and infinities are written as null (json has no such numbers), the reader reads null into a float / double as NaN
    /// (so infinities are read back as NaN, and a NaN or infinity in a nullable float / double is read back as null), dictionary keys are json strings and keep "NaN", "Infinity" and "-Infinity".
    /// Negative zero is written as -0.0 (keeps the sign).
    /// The methods are thread safe, but the static configuration (<see cref="AssemblyMap"/>, <see cref="NamespaceMap"/>, <see cref="ToTypename"/>) must be set up before the first use.
    /// </remarks>
    [SkipLocalsInit]
    unsafe public static partial class JsonWriter
    {

        /// <summary>
        /// Remapping of assembly names for type names (key is the simple assembly name, like "MyAssembly"), used by <see cref="DefaultTypename"/>.
        /// An empty value removes the assembly name from the type name.
        /// </summary>
        /// <remarks>
        /// Not thread safe, configure before the first use (type names are embedded in the cached writers when a type is first used).
        /// </remarks>
        public static readonly Dictionary<String, String> AssemblyMap = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Remapping of namespaces for type names (key is the full namespace of the type), used by <see cref="DefaultTypename"/>.
        /// An empty value removes the namespace from the type name.
        /// </summary>
        /// <remarks>
        /// Not thread safe, configure before the first use (type names are embedded in the cached writers when a type is first used).
        /// For generic types only the namespace of the generic type itself is remapped, not the namespaces of the (assembly qualified) generic type arguments.
        /// </remarks>
        public static readonly Dictionary<String, String> NamespaceMap = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Function that maps from a Type to the type name written as "$type" (by default <see cref="DefaultTypename"/>).
        /// </summary>
        /// <remarks>
        /// Set before the first use, the type names are embedded in the cached writers when a type is first used.
        /// </remarks>
        public static Func<Type, String> ToTypename = DefaultTypename;


        /// <summary>
        /// The default mapping of a type to a type-name: "FullName,AssemblyName" (like Newtonsoft.Json), uses the <see cref="AssemblyMap"/> and <see cref="NamespaceMap"/> to adjust the name.
        /// All spaces are removed.
        /// </summary>
        /// <param name="type">The type to get the name for</param>
        /// <returns>The type name, like "System.Int32,System.Private.CoreLib"</returns>
        public static String DefaultTypename(Type type)
        {
            var tn = type.FullName;
            var asm = type.Assembly.FullName.SplitFirst(',');
            if (AssemblyMap.TryGetValue(asm, out var na))
                asm = na;
            var ns = NamespaceMap;
            if (ns.Count > 0)
            {
                //  The full name of a generic type includes the assembly qualified type arguments (with '.' in them), use the namespace length
                var li = type.IsGenericType ? (type.Namespace?.Length ?? -1) : tn.LastIndexOf('.');
                if (li > 0)
                {
                    var n = tn.Substring(0, li);
                    if (ns.TryGetValue(n, out var nn))
                    {
                        if (String.IsNullOrEmpty(nn))
                        {
                            tn = tn.Substring(li + 1);
                        } else
                        {
                            tn = nn + tn.Substring(li);
                        }
                    }
                }
            }
            return (String.IsNullOrEmpty(asm) ? tn : String.Join(',', tn, asm)).Replace(" ", "");
        }

        /// <summary>
        /// Convert an object to bytes (UTF8 encoded string), using an existing buffer
        /// </summary>
        /// <remarks>
        /// If <paramref name="dest"/> is big enough it's written to in place, else (or if null) the writer grows using pooled buffers and <paramref name="dest"/> is replaced
        /// with a new array (somewhat larger than needed, so that it can be reused for similar data without growing).
        /// The json starts at <paramref name="destOffset"/>, the bytes before it are copied from the original <paramref name="dest"/> (undefined if <paramref name="dest"/> was null).
        /// </remarks>
        /// <typeparam name="T">Type of object, implicit</typeparam>
        /// <param name="dest">Destination buffer, can be null, will be replaced if more space is needed</param>
        /// <param name="value">The object to convert to json</param>
        /// <param name="destOffset">An optional write offset, must be within <paramref name="dest"/> (0 if <paramref name="dest"/> is null)</param>
        /// <param name="typeIsOptional">If true, boxed values of primitive like types (numbers, string, bool, char, date / time types, Guid and enums) are written without type information (compatible with old Newtonsoft.Json versions),
        /// if false all boxed data is written with type information</param>
        /// <returns>The end position of the json in <paramref name="dest"/>, i.e. <paramref name="destOffset"/> + the number of bytes written</returns>
        public static int ToJsonBytes<T>(ref Byte[] dest, T value, int destOffset = 0, bool typeIsOptional = true)
        {
            //  Without a destination (or if it's too small), the writer uses (and grows with) rented buffers,
            //  only the final buffer is allocated (as big as needed, so that it can be reused for the same data without growing)
            var rented = dest == null;
            var b = rented ? ArrayPoolStream.Rent(InitialRentSize) : dest;
            //  Pinning with fixed is cheaper than a GCHandle
            fixed (Byte* p = b)
            {
                var w = new BufferWriter(b, p, destOffset, rented, rented ? InitialCapacity : b.Length)
                {
                    TypeIsOptional = typeIsOptional,
                };
                try
                {
                    //  Primitives are written without an Ensure (the caller ensures the bounded size)
                    w.Ensure(64);
                    InternalRoot(ref w, value);
                    dest = w.DetachBuffer();
                    return w.Position;
                }
                finally
                {
                    w.Dispose();
                }
            }
        }

        /// <summary>
        /// Convert an object to bytes (UTF8 encoded string)
        /// </summary>
        /// <remarks>
        /// If <paramref name="dest"/> is big enough the returned memory points into it (the caller must not reuse it while the result is in use),
        /// else the json is written to pooled buffers and copied to a new array of the exact size.
        /// </remarks>
        /// <typeparam name="T">Type of object, implicit</typeparam>
        /// <param name="value">The object to convert to json</param>
        /// <param name="dest">An optional buffer to use (if big enough one buffer allocation is avoided)</param>
        /// <param name="typeIsOptional">If true, boxed values of primitive like types (numbers, string, bool, char, date / time types, Guid and enums) are written without type information (compatible with old Newtonsoft.Json versions),
        /// if false all boxed data is written with type information</param>
        /// <returns>The object as UTF8 encoded json</returns>
        public static Memory<Byte> ToJsonBytes<T>(T value, Byte[] dest = null, bool typeIsOptional = true)
        {
            //  Without a destination (or if it's too small), the writer uses (and grows with) rented buffers, the result is then copied to an array of the exact size
            var rented = dest == null;
            var size = rented ? SizeHint<T>.RentSize : 0;
            var b = rented ? ArrayPoolStream.Rent(size) : dest;
            //  Pinning with fixed is cheaper than a GCHandle
            fixed (Byte* p = b)
            {
                var w = new BufferWriter(b, p, 0, rented, rented ? size : b.Length)
                {
                    TypeIsOptional = typeIsOptional,
                };
                try
                {
                    //  Primitives are written without an Ensure (the caller ensures the bounded size)
                    w.Ensure(64);
                    InternalRoot(ref w, value);
                    var l = w.Offset;
                    if (rented)
                        SizeHint<T>.Set(l);
                    if (!w.Rented)
                        return new Memory<byte>(w.Data, 0, l);
                    var d = GC.AllocateUninitializedArray<Byte>(l);
                    new ReadOnlySpan<Byte>(w.DataPtr, l).CopyTo(d);
                    return d;
                }
                finally
                {
                    w.Dispose();
                }
            }
        }

        /// <summary>
        /// Convert an object to a json string
        /// </summary>
        /// <remarks>
        /// The json is written to pooled buffers, only the resulting string is allocated.
        /// </remarks>
        /// <typeparam name="T">Type of object, implicit</typeparam>
        /// <param name="value">The object to convert to json</param>
        /// <param name="typeIsOptional">If true, boxed values of primitive like types (numbers, string, bool, char, date / time types, Guid and enums) are written without type information (compatible with old Newtonsoft.Json versions),
        /// if false all boxed data is written with type information</param>
        /// <returns>The object as a json string</returns>
        public static String ToJsonString<T>(T value, bool typeIsOptional = true)
        {
            //  The writer uses (and grows with) rented buffers, only the string is allocated
            var size = SizeHint<T>.RentSize;
            var temp = ArrayPoolStream.Rent(size);
            //  Pinning with fixed is cheaper than a GCHandle
            fixed (Byte* p = temp)
            {
                var w = new BufferWriter(temp, p, 0, true, size)
                {
                    TypeIsOptional = typeIsOptional
                };
                try
                {
                    //  Primitives are written without an Ensure (the caller ensures the bounded size)
                    w.Ensure(64);
                    InternalRoot(ref w, value);
                    var l = w.Offset;
                    SizeHint<T>.Set(l);
                    return Encoding.UTF8.GetString(w.DataPtr, l);
                }
                finally
                {
                    //  Returns the rented buffer
                    w.Dispose();
                }
            }
        }

        /// <summary>
        /// The size of the buffer that is rented when the caller doesn't supply one
        /// </summary>
        const int InitialRentSize = 4096;

        /// <summary>
        /// The max size of the buffer that is rented up front (see <see cref="SizeHint{T}"/>)
        /// </summary>
        const int MaxRentSizeHint = 1 << 20;

        /// <summary>
        /// The size of the json written for the last value of a type (by the entry points that rent their buffer), so that a buffer that is big enough can be rented up front (no growing and copying)
        /// </summary>
        /// <remarks>
        /// Updated without synchronization (an int is written atomically, any value is just a hint).
        /// </remarks>
        static class SizeHint<T>
        {
            static int Last;

            /// <summary>
            /// The size of the buffer to rent: the size of the last json of the type plus a margin (at least <see cref="InitialRentSize"/>, at most <see cref="MaxRentSizeHint"/>)
            /// </summary>
            public static int RentSize
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    var l = Last;
                    l += (l >> 4) + 256;
                    return l <= InitialRentSize ? InitialRentSize : (l >= MaxRentSizeHint ? MaxRentSizeHint : l);
                }
            }

            /// <summary>
            /// Remember the size of the json written for a value of the type
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Set(int size)
            {
                if (Last != size)
                    Last = size;
            }
        }

        /// <summary>
        /// The initial capacity of a rented buffer that is copied to the caller (ToJsonBytes with a null dest), the size of the copy is the capacity (it grows in small steps)
        /// </summary>
        const int InitialCapacity = 256;


        #region Internal

        #region Build

        /// <summary>
        /// Write a value that is known to be non null and of exactly the type <typeparamref name="T"/> (used for value types, they are written without boxing)
        /// </summary>
        static void Internal<T>(ref BufferWriter w, T value)
        {
            if (typeof(T).IsValueType)
            {
                CacheT<T>.TypedWriter(ref w, value);
                return;
            }
            CacheT<T>.Writer(ref w, value);
        }

        /// <summary>
        /// Write the root value: a (non nullable) value type is always of exactly the type <typeparamref name="T"/> (written without boxing), other types using <see cref="InternalMaybeBoxed{T}"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void InternalRoot<T>(ref BufferWriter w, T value)
        {
            if (default(T) != null)
            {
                Internal(ref w, value);
                return;
            }
            InternalMaybeBoxed(ref w, value);
        }

        /// <summary>
        /// Write a value of exactly the type <typeparamref name="T"/> or null (used for sealed types)
        /// </summary>
        static void InternalMaybeNull<T>(ref BufferWriter w, T value)
        {
            if (value == null)
            {
                WriteNull(ref w);
                return;
            }
            CacheT<T>.Writer(ref w, value);
        }

        /// <summary>
        /// Write a value that may be null or of a derived type, if the runtime type differs from <typeparamref name="T"/> the value is written using <see cref="InternalBoxed{T}"/>,
        /// except for the type the reader creates for a collection / dictionary interface anyway (see <see cref="Helper.GetCreatedType(Type)"/>), that is written without type information
        /// </summary>
        static void InternalMaybeBoxed<T>(ref BufferWriter w, T value)
        {
            var expectedType = typeof(T);
            var actualType = value?.GetType();
            bool needType = (expectedType != actualType);
            if (needType) // Boxed or null, slow path
            {
                if ((actualType != null) && (actualType == CreatedType<T>.Type))
                {
                    GetWriter(actualType).Write(ref w, value);
                    return;
                }
                InternalBoxed<T>(ref w, value, actualType);
                return;
            }
            CacheT<T>.Writer(ref w, value);
        }

        /// <summary>
        /// The number of entries in <see cref="WriterCache"/> (a power of 2)
        /// </summary>
        const int WriterCacheSize = 256;

        /// <summary>
        /// A direct mapped cache of the writers of the runtime types of boxed values (by the identity hash code of the type), in front of the <see cref="Writers"/>.
        /// An entry is immutable and replaced as a whole (lock free, a collision just costs a look up).
        /// </summary>
        static readonly WriterCacheEntry[] WriterCache = new WriterCacheEntry[WriterCacheSize];

        sealed class WriterCacheEntry
        {
            public WriterCacheEntry(Type type, TypeInfo info)
            {
                Type = type;
                Info = info;
            }

            public readonly Type Type;
            public readonly TypeInfo Info;
        }

        /// <summary>
        /// Get the writer of a (runtime) type, build it if needed
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static TypeInfo GetWriter(Type type)
        {
            var e = WriterCache[RuntimeHelpers.GetHashCode(type) & (WriterCacheSize - 1)];
            if ((e != null) && ReferenceEquals(e.Type, type))
                return e.Info;
            return GetWriterSlow(type);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static TypeInfo GetWriterSlow(Type type)
        {
            if (!Writers.TryGetValue(type, out var writer))
            {
                writer = AddWriter(type);
                //  Only completed writers are cached
                if (writer == InProgressTypeInfo)
                    return writer;
            }
            WriterCache[RuntimeHelpers.GetHashCode(type) & (WriterCacheSize - 1)] = new WriterCacheEntry(type, writer);
            return writer;
        }

        /// <summary>
        /// The type the reader creates for <typeparamref name="T"/> when there is no "$type" (computed once per type), see <see cref="Helper.GetCreatedType(Type)"/>
        /// </summary>
        static class CreatedType<T>
        {
            public static readonly Type Type = Helper.GetCreatedType(typeof(T));
        }

        /// <summary>
        /// Write a value using the writer of its runtime type, with type information unless the type is optional (<see cref="BufferWriter.TypeIsOptional"/>)
        /// </summary>
        /// <param name="w">The writer</param>
        /// <param name="value">The value</param>
        /// <param name="actualType">The runtime type of the value, null to write null</param>
        static void InternalBoxed<T>(ref BufferWriter w, T value, Type actualType)
        {
            if (actualType == null)
            {
                WriteNull(ref w);
                return;
            }
            var writer = GetWriter(actualType);
            if (w.TypeIsOptional)
                writer.WriteOptionalTyped(ref w, value);
            else
                writer.WriteTyped(ref w, value);
        }


        /// <summary>
        /// A generated writer, writes the (boxed) value at the current position of the writer
        /// </summary>
        /// <param name="w">The writer</param>
        /// <param name="o">The value to write (of the type the writer was generated for)</param>
        public delegate void WriterDel(ref BufferWriter w, Object o);

        /// <summary>
        /// Per type cache of the writer (initialized on first use of a type)
        /// </summary>
        /// <remarks>
        /// The writer is fetched lazily (not by a static initializer), so a type initializer never waits for <see cref="BuildLock"/>
        /// and a failed or concurrent first use is never cached permanently.
        /// </remarks>
        static class CacheT<T>
        {
            /// <summary>
            /// A writer for a value of type <typeparamref name="T"/> (no boxing)
            /// </summary>
            public delegate void WriterDelT(ref BufferWriter w, T o);

            static WriterDel W;

            /// <summary>
            /// The writer for a value of exactly the type <typeparamref name="T"/>
            /// </summary>
            public static WriterDel Writer
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => W ?? Init();
            }

            static WriterDel Init()
            {
                //  CacheWriter returns completed writers (built under the BuildLock, a TypeInfo is immutable),
                //  the in progress place holder can only be returned to the thread building the type (never cache it)
                var ti = CacheWriter(typeof(T));
                var w = ti.Write;
                if (ti != InProgressTypeInfo)
                    Volatile.Write(ref W, w);
                return w;
            }

            static WriterDelT WT;

            /// <summary>
            /// The writer for a value of exactly the type <typeparamref name="T"/>, without boxing (for value types, a writer that boxes the value if the type has no typed writer)
            /// </summary>
            public static WriterDelT TypedWriter
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => WT ?? InitTyped();
            }

            static WriterDelT InitTyped()
            {
                var ti = CacheWriter(typeof(T));
                var wt = ti.WriteT as WriterDelT;
                if (wt == null)
                {
                    var w = ti.Write;
                    wt = (ref BufferWriter bw, T o) => w(ref bw, o);
                }
                if (ti != InProgressTypeInfo)
                    Volatile.Write(ref WT, wt);
                return wt;
            }
        }


        /// <summary>
        /// Escape a string for use inside a json string (no quotes are added), used for member and type names when building writers
        /// </summary>
        /// <param name="t">The string to escape</param>
        /// <returns>The escaped string (the same instance if nothing needs escaping)</returns>
        static String JsonEscape(String t)
        {
            var l = t.Length;
            Char[] rented = null;
            // An escaped char is at most 6 chars (\u00XX)
            var bl = checked(l * 6);
            var d = bl <= 4096 ? stackalloc Char[bl] : (rented = ArrayPool<Char>.Shared.Rent(bl)).AsSpan();
            try
            {
                int o = 0;
                var esc = EscapeChars;
                var me = MaxEscapedChar;
                var h = Hex;
                for (int i = 0; i < l; ++i)
                {
                    var x = t[i];
                    var xx = (uint)x;
                    if (xx > me)
                    {
                        d[o] = x;
                        ++o;
                        continue;
                    }
                    var e = esc[xx];
                    if (e == 0)
                    {
                        d[o] = x;
                        ++o;
                        continue;
                    }
                    if (e == 1)
                    {
                        d[o] = '\\';
                        ++o;
                        d[o] = 'u';
                        ++o;
                        d[o] = h[(xx >> 12) & 0xf];
                        ++o;
                        d[o] = h[(xx >> 8) & 0xf];
                        ++o;
                        d[o] = h[(xx >> 4) & 0xf];
                        ++o;
                        d[o] = h[(xx) & 0xf];
                        ++o;
                        continue;
                    }
                    d[o] = '\\';
                    ++o;
                    d[o] = (Char)e;
                    ++o;
                }
                return o == l ? t : new string(d.Slice(0, o));
            }
            finally
            {
#if DEBUG
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented, true);
#else//DEBUG
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
#endif//DEBUG
            }
        }

        /// <summary>
        /// Get the start of a typed value: {"$type":"name" (as UTF8, without a trailing comma)
        /// </summary>
        static Byte[] GetTypeJson(Type type)
        {
            return Encoding.UTF8.GetBytes(String.Concat("{\"$type\":\"", JsonEscape(ToTypename(type)), "\""));
        }

        static readonly Byte[] TextValues = Encoding.UTF8.GetBytes("\"$values\":");
        static readonly Byte[] TextValue = Encoding.UTF8.GetBytes("\"$value\":");
        static Byte[] GetTypeJson<T>() => Append(Append(GetTypeJson(typeof(T)), TextSepComma), TextValue);

        const Byte ObjectBegin = (Byte)'{';
        const Byte ObjectEnd = (Byte)'}';
        const Byte ArrayBegin = (Byte)'[';
        const Byte ArrayEnd = (Byte)']';
        const Byte SepComma = (Byte)',';
        const Byte SepColon = (Byte)':';
        const Byte SepQuote = (Byte)'"';


        static readonly Byte[] TextObjectBegin = [ObjectBegin];
        static readonly Byte[] TextObjectEnd = [ObjectEnd];
        static readonly Byte[] TextSepComma = [SepComma];

        static readonly Type BufferWriterType = typeof(BufferWriter);
        static readonly Type RefBufferWriterType = BufferWriterType.MakeByRefType();

        static readonly ParameterExpression WriterExp = Expression.Parameter(RefBufferWriterType, "w");
        static readonly ParameterExpression WriterObject = Expression.Parameter(typeof(Object), "value");

        static readonly Type JsonWriterType = typeof(JsonWriter);

        static readonly MethodInfo MethodBufferWriterEnsure = Helper.SafeGetMethod(BufferWriterType, nameof(BufferWriter.Ensure));
        static readonly MethodInfo MethodBufferWriterWriteByte = Helper.SafeGetMethod(BufferWriterType, nameof(BufferWriter.Write), [typeof(Byte)]);

        static readonly MethodInfo MethodInternalMaybeBoxed = Helper.SafeGetMethod(JsonWriterType, nameof(InternalMaybeBoxed), BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo MethodInternalMaybeNull = Helper.SafeGetMethod(JsonWriterType, nameof(InternalMaybeNull), BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo MethodInternal = Helper.SafeGetMethod(JsonWriterType, nameof(Internal), BindingFlags.NonPublic | BindingFlags.Static);

        static readonly MethodInfo MethodInternalList = Helper.SafeGetMethod(JsonWriterType, nameof(InternalList), BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo MethodInternalMaybeNullList = Helper.SafeGetMethod(JsonWriterType, nameof(InternalMaybeNullList), BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo MethodInternalMaybeBoxedList = Helper.SafeGetMethod(JsonWriterType, nameof(InternalMaybeBoxedList), BindingFlags.NonPublic | BindingFlags.Static);

        static readonly MethodInfo MethodInternalEnum = Helper.SafeGetMethod(JsonWriterType, nameof(InternalEnum), BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo MethodInternalMaybeNullEnum = Helper.SafeGetMethod(JsonWriterType, nameof(InternalMaybeNullEnum), BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo MethodInternalMaybeBoxedEnum = Helper.SafeGetMethod(JsonWriterType, nameof(InternalMaybeBoxedEnum), BindingFlags.NonPublic | BindingFlags.Static);

        static readonly MethodInfo MethodInternalKeyValueEnum = Helper.SafeGetMethod(JsonWriterType, nameof(InternalKeyValueEnum), BindingFlags.NonPublic| BindingFlags.Static);


        static readonly MethodInfo MethodInvoke = Helper.SafeGetMethod(typeof(WriterDel), nameof(WriterDel.Invoke), BindingFlags.Instance | BindingFlags.Public, null, [RefBufferWriterType, typeof(Object)], null);

        static readonly MethodInfo MethodMoveNext = Helper.SafeGetMethod(typeof(IEnumerator), nameof(IEnumerator.MoveNext), BindingFlags.Instance | BindingFlags.Public);


        static Expression MakeExpressionActionInternalT<T>() => Expression.Constant(new CacheT<T>.WriterDelT(Internal<T>));
        static Expression MakeExpressionActionInternalMaybeBoxedT<T>() => Expression.Constant(new CacheT<T>.WriterDelT(InternalMaybeBoxed<T>));
        static Expression MakeExpressionActionInternalMaybeNullT<T>() => Expression.Constant(new CacheT<T>.WriterDelT(InternalMaybeNull<T>));

        static readonly MethodInfo MethodMakeExpressionActionInternalT = Helper.SafeGetMethod(JsonWriterType, nameof(MakeExpressionActionInternalT), BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo MethodMakeExpressionActionInternalMaybeBoxedT = Helper.SafeGetMethod(JsonWriterType, nameof(MakeExpressionActionInternalMaybeBoxedT), BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo MethodMakeExpressionActionInternalMaybeNullT = Helper.SafeGetMethod(JsonWriterType, nameof(MakeExpressionActionInternalMaybeNullT), BindingFlags.NonPublic | BindingFlags.Static);

        static readonly MethodInfo MethodWriteDoubleKey = Helper.SafeGetMethod(JsonWriterType, nameof(WriteDoubleKey), BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo MethodWriteSingleKey = Helper.SafeGetMethod(JsonWriterType, nameof(WriteSingleKey), BindingFlags.NonPublic | BindingFlags.Static);

        static Expression MakeExpressionActionInternal(Type type) => Expression.Lambda<Func<Expression>>(Expression.Call(MethodMakeExpressionActionInternalT.MakeGenericMethod(type))).Compile()();
        static Expression MakeExpressionActionInternalMaybeBoxed(Type type) => Expression.Lambda<Func<Expression>>(Expression.Call(MethodMakeExpressionActionInternalMaybeBoxedT.MakeGenericMethod(type))).Compile()();
        static Expression MakeExpressionActionInternalMaybeNull(Type type) => Expression.Lambda<Func<Expression>>(Expression.Call(MethodMakeExpressionActionInternalMaybeNullT.MakeGenericMethod(type))).Compile()();

        const int MaxCachedInts = 1024;
        static readonly ConstantExpression[] CachedInts = new ConstantExpression[MaxCachedInts];

        static ConstantExpression GetInt32Exp(int value)
        {
            if ((value < 0) || (value >= MaxCachedInts))
                return Expression.Constant(value);
            var c = CachedInts;
            var v = c[value];
            if (v != null)
                return v;
            v = Expression.Constant(value);
            c[value] = v;
            return v;
        }

        static readonly Expression Ensure64Exp = Expression.Call(WriterExp, MethodBufferWriterEnsure, GetInt32Exp(64));

        static readonly ParameterExpression ParamData = Expression.Variable(typeof(Byte[]), "d");
        static readonly ParameterExpression ParamOffset = Expression.Variable(typeof(int), "o");


        static readonly Byte[] TypenameByteArray = GetTypeJson<Byte[]>();

        static readonly FieldInfo BufferWriterData = BufferWriterType.GetField(nameof(BufferWriter.Data), BindingFlags.Public | BindingFlags.Instance);
        static readonly FieldInfo BufferWriterOffset = BufferWriterType.GetField(nameof(BufferWriter.Offset), BindingFlags.Public | BindingFlags.Instance);


        static readonly MethodInfo MethodBufferBlockCopy = Helper.SafeGetMethod(typeof(Buffer), nameof(Buffer.BlockCopy), BindingFlags.Public | BindingFlags.Static);


        static readonly Expression ReadDataExp = Expression.Assign(ParamData, Expression.Field(WriterExp, BufferWriterData));
        static readonly Expression ReadOffsetExp = Expression.Assign(ParamOffset, Expression.Field(WriterExp, BufferWriterOffset));
        static readonly Expression WriteOffsetExp = Expression.Assign(Expression.Field(WriterExp, BufferWriterOffset), ParamOffset);

        static readonly Expression WriteDataAtOffsetExp = Expression.ArrayAccess(ParamData, ParamOffset);
        static readonly Expression IncOffsetExpression = Expression.PreIncrementAssign(ParamOffset);

        static readonly Expression[] CachedBytes = Enumerable.Range(0, 256).Select(x => Expression.Assign(WriteDataAtOffsetExp, Expression.Constant((Byte)x))).ToArray();

        /// <summary>
        /// A writer that writes a constant
        /// </summary>
        /// <param name="w">The writer</param>
        public delegate void WriterConstDel(ref BufferWriter w);


        static Expression GetWriteByteExpr(Byte c)
        {
            return Expression.Block([ParamData, ParamOffset],
                ReadDataExp,
                ReadOffsetExp,
                CachedBytes[c],
                IncOffsetExpression,
                WriteOffsetExp
            );
        }


        static readonly Expression WriteObjectBeginExp = GetWriteByteExpr(ObjectBegin);// Expression.Call(ExpressionWriter, MethodBufferWriterWriteByte, ConstObjectBegin);
        static readonly Expression WriteObjectEndExp = GetWriteByteExpr(ObjectEnd);// Expression.Call(ExpressionWriter, MethodBufferWriterWriteByte, ConstObjectEnd);
        static readonly Expression WriteArrayBeginExp = GetWriteByteExpr(ArrayBegin);// Expression.Call(ExpressionWriter, MethodBufferWriterWriteByte, ConstArrayBegin);
        static readonly Expression WriteArrayEndExp = GetWriteByteExpr(ArrayEnd);// Expression.Call(ExpressionWriter, MethodBufferWriterWriteByte, ConstArrayEnd);
        static readonly Expression WriteCommaEndExp = GetWriteByteExpr(SepComma);// Expression.Call(ExpressionWriter, MethodBufferWriterWriteByte, ConstObjectComma);

        /// <summary>
        /// The key types of dictionaries that are written as json objects (enums are allowed too, see <see cref="IsAllowedDictionaryKey"/>).
        /// Dictionaries with other key types are written with the key's ToString() as the property name (they can't be read back).
        /// </summary>
        static readonly IReadOnlySet<Type> AllowedDictionaryKeys = new HashSet<Type>()
        {
            typeof(Char),
            typeof(Byte),
            typeof(SByte),
            typeof(UInt16),
            typeof(Int16),
            typeof(UInt32),
            typeof(Int32),
            typeof(UInt64),
            typeof(Int64),
            typeof(Single),
            typeof(Double),
            typeof(Decimal),
            typeof(String),
            typeof(Boolean),
            typeof(TimeSpan),
            typeof(DateTime),
            typeof(DateOnly),
            typeof(TimeOnly),
            typeof(DateTimeOffset),
            typeof(Guid),
        }.ToFrozenSet();

        /// <summary>
        /// The dictionary key types that needs to be quoted (the writers of the other key types writes a json string), enums are quoted too (see <see cref="DictionaryKeyNeedQuote"/>)
        /// </summary>
        static readonly IReadOnlySet<Type> DictionaryKeysWithQuote = new HashSet<Type>()
        {
            typeof(Byte),
            typeof(SByte),
            typeof(UInt16),
            typeof(Int16),
            typeof(UInt32),
            typeof(Int32),
            typeof(UInt64),
            typeof(Int64),
            typeof(Single),
            typeof(Double),
            typeof(Decimal),
            typeof(Boolean),
        }.ToFrozenSet();

        /// <summary>
        /// True if a dictionary with this key type is written as a json object
        /// </summary>
        static bool IsAllowedDictionaryKey(Type keyType) => keyType.IsEnum || AllowedDictionaryKeys.Contains(keyType);

        /// <summary>
        /// True if the writer of the key type doesn't write a json string (the key must be quoted), enums are written as (quoted) numbers
        /// </summary>
        static bool DictionaryKeyNeedQuote(Type keyType) => keyType.IsEnum || DictionaryKeysWithQuote.Contains(keyType);

        /// <summary>
        /// The writers of all types (built on demand), initialized with the primitive writers by the static constructor.
        /// New writers are only added by <see cref="AddWriter(Type)"/> (under the <see cref="BuildLock"/>), reading is lock free.
        /// </summary>
        static readonly LowAllocConcurrentDictionary<Type, TypeInfo> Writers;


        static readonly Byte[] NullData = Encoding.UTF8.GetBytes("null");
        static readonly WriterDel NullWriter = GetWriteConstantBufferEnsuredActionObject(NullData);
        static readonly TypeInfo NullTypeInfo = new TypeInfo(NullWriter, NullWriter);

        static readonly WriterConstDel WriteNull = GetWriteConstantBufferEnsuredAction(NullData);
        static readonly WriterDel WriteEmptyObject = GetWriteConstantBufferEnsuredActionObject(Encoding.UTF8.GetBytes("{}"));

        /// <summary>
        /// Returned by <see cref="AddWriter(Type)"/> for a type that the current thread is already building (a recursive type), only used while building writers.
        /// It's unbounded, so values of the type are written using the (completed) writer of the type at runtime.
        /// </summary>
        static readonly TypeInfo InProgressTypeInfo = new TypeInfo(WriteEmptyObject, WriteEmptyObject);


        static readonly WriterConstDel WriteTypenameByteArray = GetWriteConstantBufferEnsuredAction(TypenameByteArray);


        //  TODO: Cache?
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Byte[] BuildParameterDecl(String name)
        {
            return Encoding.UTF8.GetBytes(String.Concat("\"", JsonEscape(name), "\":"));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Byte[] BuildParameterDeclComma(String name)
        {
            return Encoding.UTF8.GetBytes(String.Concat(",\"", JsonEscape(name), "\":"));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Byte[] Append(Byte[] a, Byte[] b)
        {
            return [.. a, .. b];
        }

        static TypeInfo CacheWriter(Type type)
        {
            if (!Writers.TryGetValue(type, out var writer))
                writer = AddWriter(type);
            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Type GetMemberType(MemberInfo i)
        {
            if (i.MemberType == MemberTypes.Property)
                return (i as PropertyInfo).PropertyType;
            return (i as FieldInfo).FieldType;
        }

        /// <summary>
        /// Types with a bounded json size (written without an Ensure of their own), the value is unused
        /// </summary>
        static readonly IReadOnlyDictionary<Type, int> PrimClasses = new Dictionary<Type, int>()
        {
            { typeof(Byte), 0 },
            { typeof(UInt16), 1 },
            { typeof(UInt32), 2 },
            { typeof(UInt64), 3 },
            { typeof(SByte), 4 },
            { typeof(Int16), 5 },
            { typeof(Int32), 6 },
            { typeof(Int64), 7 },
            { typeof(Single), 8 },
            { typeof(Double), 9 },
            { typeof(Decimal), 10 },
            { typeof(DateTime), 11 },
            { typeof(DateOnly), 12 },
            { typeof(TimeOnly), 13 },
            { typeof(TimeSpan), 14 },
            { typeof(DateTimeOffset), 15 },
            { typeof(Guid), 16 },
            { typeof(Boolean), 17 },
            { typeof(Char), 18 },
            //{ typeof(String), 19 },

        }.ToFrozenDictionary();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsBounded(Type type)
        {
            if (type.IsEnum)
                return true;
            return PrimClasses.TryGetValue(type, out var _);
        }

        /// <summary>
        /// The order members are written in: bounded types first (sorted by name), then the other types sorted by the full type name and name
        /// </summary>
        static int MemberCompare(MemberInfo x, MemberInfo y)
        {
            var a = GetMemberType(x);
            var b = GetMemberType(y);
            var ab = IsBounded(a);
            if (ab != IsBounded(b))
                return ab ? -1 : 1;
            int i;
            if (!ab)
            {
                i = a.FullName.CompareTo(b.FullName);
                if (i != 0)
                    return i;
            }
            return x.Name.CompareTo(y.Name);
        }


        /// <summary>
        /// True if a member is serialized: public properties with a getter and a setter (of any accessibility, indexers are excluded) and public fields that aren't readonly
        /// </summary>
        static bool IsValidMember(MemberInfo m)
        {
            {
                var p = m as PropertyInfo;
                if (p != null)
                    return p.CanRead && p.CanWrite && (p.GetIndexParameters().Length == 0);
            }
            {
                var f = m as FieldInfo;
                if (f != null)
                    return !f.IsInitOnly;
            }
            return false;
        }


        static void AddExpression(ref bool haveFirst, List<Expression> p, HashSet<ParameterExpression> extraParams, Expression e, Expression skipRepeats)
        {
            var b = e as BlockExpression;
            if (b != null)
            {
                foreach (var x in b.Variables)
                    extraParams.Add(x);
                foreach (var x in b.Expressions)
                    AddExpression(ref haveFirst, p, extraParams, x, skipRepeats);
                return;
            }
            if (e == skipRepeats)
            {
                if (haveFirst)
                    return;
                haveFirst = true;
            }
            p.Add(e);
        }

        

        /// <summary>
        /// Create a single block from a list of expressions, flattening nested blocks (their variables are moved to the new block)
        /// </summary>
        /// <param name="program">The expressions</param>
        /// <param name="parameters">Additional variables of the block</param>
        /// <param name="skipRepeats">An expression that is only kept the first time it occurs (in the flattened list)</param>
        /// <returns>The block, or the only expression if there is just one and no variables</returns>
        static Expression CreateProgramBlock(IList<Expression> program, IEnumerable<ParameterExpression> parameters = null, Expression skipRepeats = null)
        {
            HashSet<ParameterExpression> extraParams = new HashSet<ParameterExpression>();
            var pl = program.Count;
            var p = new List<Expression>(pl + pl);
            bool haveFirst = false;
            foreach (var e in program)
                AddExpression(ref haveFirst, p, extraParams, e, skipRepeats);
            if (parameters != null)
            {
                foreach (var t in parameters)
                    extraParams.Add(t);
            }
            if ((p.Count == 1) && (extraParams.Count <= 0))
                return p[0];
            var prog = Expression.Block(extraParams, p.ToArray());
            return prog;
        }

        /// <summary>
        /// Serializes the building of writers (all threads), the lock is reentrant so building a type can build the writers of its member types
        /// </summary>
        static readonly Object BuildLock = new Object();

        /// <summary>
        /// The types that writers are currently being built for (to break recursion for recursive types).
        /// Only accessed while holding the <see cref="BuildLock"/>, so it only contains types that the thread holding the lock is building.
        /// </summary>
        static readonly HashSet<Type> WritersInProgress = new HashSet<Type>();

        /// <summary>
        /// True if a type has members that are serialized (see <see cref="IsValidMember"/>)
        /// </summary>
        static bool HaveSerializedMembers(Type type) => type.GetMembers(BindingFlags.Public | BindingFlags.Instance).Any(IsValidMember);

        /// <summary>
        /// Get the generic interface of a type with the given generic type definition (the first one if there are many)
        /// </summary>
        static Type GetGenericInterface(Type type, Type genericTypeDefinition) => type.GetInterfaces().FirstOrDefault(t => t.IsGenericType && (t.GetGenericTypeDefinition() == genericTypeDefinition));

        /// <summary>
        /// Build (and cache) the writer for a type, or get the cached writer if another thread already built it
        /// </summary>
        /// <remarks>
        /// Writers are built while holding the <see cref="BuildLock"/>, so concurrent first use of a type waits for the writer to be completed.
        /// If the writer for the type is already being built by the current thread (a recursive type), <see cref="InProgressTypeInfo"/> is returned,
        /// it's only used while building (an unbounded type is written using its own cached writer at runtime).
        /// </remarks>
        /// <param name="type">The type</param>
        /// <returns>The writer info</returns>
        /// <exception cref="Exception">The type isn't supported (like multi dimensional arrays or dictionaries with an unsupported key type) or the writer couldn't be built</exception>
        static TypeInfo AddWriter(Type type)
        {
            lock (BuildLock)
            {
                if (Writers.TryGetValue(type, out var existing))
                    return existing;
                var inProgress = WritersInProgress;
                if (!inProgress.Add(type))
                    return InProgressTypeInfo;
                try
                {
                    return BuildWriter(type);
                }
                finally
                {
                    inProgress.Remove(type);
                }
            }
        }

        /// <summary>
        /// Build and cache the writer for a type, must be called by <see cref="AddWriter(Type)"/> (holding the <see cref="BuildLock"/>)
        /// </summary>
        static TypeInfo BuildWriter(Type type)
        {
            try
            {
                if (type.IsArray)
                {
                    if (type.GetArrayRank() != 1)
                        throw new Exception("Only one dimensional arrays supported!");
                }
                var writer = WriterExp;
                TypeInfo ti = null;
                //  IDictionary<T>
                var colType = GetGenericInterface(type, typeof(IDictionary<,>));
                if ((ti == null) && (colType != null) && !(type.IsInterface || type.IsAbstract))
                {
                    var kt = colType.GetGenericArguments()[0];
                    if (!IsAllowedDictionaryKey(kt))
                    {
                        //  Other key types used to be written as a collection of KeyValuePair, i.e. [{},{}] (all data was silently lost).
                        //  Write the keys as strings (ToString), the output is valid json (but it can't be read back into this dictionary type)
                        var pt = colType.GetGenericArguments()[1];
                        var stringKeyed = typeof(Dictionary<,>).MakeGenericType(typeof(String), pt);
                        var toStringKeyed = (Func<Object, Object>)Helper.SafeGetMethod(JsonWriterType, nameof(ToStringKeyed), BindingFlags.Static | BindingFlags.NonPublic)
                            .MakeGenericMethod(kt, pt).CreateDelegate(typeof(Func<Object, Object>));
                        TypeInfo stringKeyedInfo = null;
                        WriterDel write = (ref BufferWriter bw, Object value) => (stringKeyedInfo ??= CacheWriter(stringKeyed)).Write(ref bw, toStringKeyed(value));
                        ti = new TypeInfo(write, write);
                    }
                    else
                    {
                        var pt = colType.GetGenericArguments()[1];
                        CacheWriter(kt);
                        CacheWriter(pt);
                        var kvt = typeof(KeyValuePair<,>).MakeGenericType(kt, pt);
                        var ct = typeof(IEnumerable<>).MakeGenericType(kvt);
                        //  Float / double keys keep NaN, Infinity and -Infinity (the value writers write null)
                        var floatKeyWriter = kt == typeof(Double) ? MethodWriteDoubleKey : (kt == typeof(Single) ? MethodWriteSingleKey : null);
                        Expression kmi;
                        if (floatKeyWriter != null)
                        {
                            kmi = kt == typeof(Double) ? Expression.Constant(new CacheT<Double>.WriterDelT(WriteDoubleKey)) : Expression.Constant(new CacheT<Single>.WriterDelT(WriteSingleKey));
                        }
                        else if (kt.IsPrimitive || kt.IsValueType)
                        {
                            kmi = MakeExpressionActionInternal(kt);
                        }
                        else
                        {
                            kmi = kt.IsSealed ? MakeExpressionActionInternalMaybeNull(kt) : MakeExpressionActionInternalMaybeBoxed(kt);
                        }
                        Expression vmi;
                        if (pt.IsPrimitive || pt.IsValueType)
                        {
                            vmi = MakeExpressionActionInternal(pt);
                        }
                        else
                        {
                            vmi = pt.IsSealed ? MakeExpressionActionInternalMaybeNull(pt) : MakeExpressionActionInternalMaybeBoxed(pt);
                        }
                        var mi = MethodInternalKeyValueEnum.MakeGenericMethod(kt, pt);
                        var enumPattern = TryGetEnumeratorPattern(type, out _, out _, out var kvCurrent) && (kvCurrent.PropertyType == kvt);
                        //  Build the writer program, the typed variant starts with {"$type":"name" so every key (including the first) must be preceded by a comma
                        Expression BuildDictionaryProgram(Expression ensureBegin, Expression writeBegin, bool commaFirst)
                        {
                            List<Expression> program = new List<Expression>();
                            List<ParameterExpression> pes = new List<ParameterExpression>();
                            program.Add(ensureBegin);
                            program.Add(writeBegin);
                            if (enumPattern)
                            {
                                //  Like InternalKeyValueEnum but without interface and delegate calls (and boxing)
                                var dobj = Expression.Variable(type, "dict");
                                var kv = Expression.Variable(kvt, "kv");
                                pes.Add(dobj);
                                pes.Add(kv);
                                program.Add(Expression.Assign(dobj, Expression.Convert(WriterObject, type)));
                                var keyWriter = CacheWriter(kt);
                                var valueWriter = CacheWriter(pt);
                                var needQuote = DictionaryKeyNeedQuote(kt);
                                var keyQuote = Encoding.UTF8.GetBytes("\"");
                                var keyQuoteComma = Encoding.UTF8.GetBytes(",\"");
                                var keyEndQuote = Encoding.UTF8.GetBytes("\":");
                                var keyEnd = Encoding.UTF8.GetBytes(":");
                                var comma = Encoding.UTF8.GetBytes(",");
                                program.Add(GetLoopExp(type, kvt, dobj, (item, isFirst) =>
                                {
                                    var noComma = isFirst && !commaFirst;
                                    var key = Expression.Property(kv, nameof(KeyValuePair<int, int>.Key));
                                    var value = Expression.Property(kv, nameof(KeyValuePair<int, int>.Value));
                                    List<Expression> e = new List<Expression>(8);
                                    e.Add(Expression.Assign(kv, item));
                                    e.Add(Ensure64Exp);
                                    if (needQuote)
                                        e.Add(GetWriteConstExp(noComma ? keyQuote : keyQuoteComma));
                                    else if (!noComma)
                                        e.Add(GetWriteConstExp(comma));
                                    e.Add(floatKeyWriter != null ? Expression.Call(floatKeyWriter, writer, key) : GetWriteValueExp(kt, keyWriter, key));
                                    e.Add(Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(16)));
                                    e.Add(GetWriteConstExp(needQuote ? keyEndQuote : keyEnd));
                                    e.Add(GetWriteValueExp(pt, valueWriter, value));
                                    return Expression.Block(e);
                                }, pes));
                            }
                            else
                            {
                                program.Add(Expression.Call(mi, writer, Expression.Convert(WriterObject, ct), kmi, vmi, Expression.Constant(DictionaryKeyNeedQuote(kt)), Expression.Constant(commaFirst)));
                            }
                            program.Add(Ensure64Exp);
                            program.Add(WriteObjectEndExp);
                            return CreateProgramBlock(program, pes);
                        }
                        var finalUntyped = BuildDictionaryProgram(Ensure64Exp, WriteObjectBeginExp, false);
                        var cbUntyped = Expression.Lambda<WriterDel>(finalUntyped, writer, WriterObject).Compile();
                        //  {"$type":"name" (no trailing comma, every key is written with a leading comma, so an empty dictionary is valid json)
                        var temp = GetTypeJson(type);
                        var finalTyped = BuildDictionaryProgram(Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(temp.Length + 64)), GetWriteConstantBufferExp(temp), true);
                        var cbTyped = Expression.Lambda<WriterDel>(finalTyped, writer, WriterObject).Compile();
                        ti = new TypeInfo(cbUntyped, cbTyped);
                    }
                }
                //  ICollection<T>
                colType = GetGenericInterface(type, typeof(ICollection<>));
                //  IEnumerable<T> classes without serialized members (like Queue<T>, Stack<T>, ConcurrentQueue<T> and ConcurrentBag<T>) are written as a collection too (in enumeration order),
                //  they used to be written as an empty object. Classes with serialized members are still written as objects (the reader reads them as objects).
                if ((ti == null) && (colType == null) && !(type.IsValueType || type.IsInterface || type.IsAbstract) && !HaveSerializedMembers(type))
                    colType = GetGenericInterface(type, typeof(IEnumerable<>));
                if ((ti == null) && (colType != null))
                {
                    var pt = colType.GetGenericArguments()[0];
                    var propWriter = CacheWriter(pt);
                    List<ParameterExpression> pes = new List<ParameterExpression>();
                    List<Expression> program = new List<Expression>();
                    var cobj = Expression.Variable(type, "c");
                    pes.Add(cobj);
                    program.Add(Expression.Assign(cobj, Expression.Convert(WriterObject, type)));
                    program.Add(Ensure64Exp);
                    program.Add(WriteArrayBeginExp);
                    var itemSize = propWriter.BoundedSize > 0 ? propWriter.BoundedSize + 8 : 64;
                    var ensureItem = Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(itemSize));
                    program.Add(GetLoopExp(type, pt, cobj, (item, isFirst) =>
                    {
                        var itemWrite = propWriter.BoundedSize > 0 ? propWriter.WriteExp(item) : GetWriteUnboundedExp(pt, item);
                        if (isFirst)
                            return Expression.Block(ensureItem, itemWrite);
                        return Expression.Block(ensureItem, WriteCommaEndExp, itemWrite);
                    }, pes));
                    program.Add(Ensure64Exp);
                    program.Add(WriteArrayEndExp);
                    var finalUntyped = CreateProgramBlock(program, pes);
                    var temp = Append(Append(GetTypeJson(type), TextSepComma), TextValues);
                    program.Insert(0, Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(temp.Length + 64)));
                    program.Insert(1, GetWriteConstantBufferExp(temp));
                    program.Add(WriteObjectEndExp);

                    var finalTyped = CreateProgramBlock(program, pes);
                    var cbUntyped = Expression.Lambda<WriterDel>(finalUntyped, writer, WriterObject).Compile();
                    var cbTyped = Expression.Lambda<WriterDel>(finalTyped, writer, WriterObject).Compile();
                    ti = new TypeInfo(cbUntyped, cbTyped);
                }
                if (type.IsEnum)
                {
                    var temp = Append(Append(GetTypeJson(type), TextSepComma), TextValue);
                    var ut = type.GetEnumUnderlyingType();
                    if (!Writers.TryGetValue(ut, out var utw))
                        throw new Exception("Failed to get writer for \"" + ut + "\"");
                    ti = new TypeInfo(type, true, ut, utw.BoundedSize);
                }
                if (type.IsInterface || type.IsAbstract)
                    ti = NullTypeInfo;

                if ((ti == null) && type.IsGenericType)
                {
                    if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        var ttype = type.GetGenericArguments()[0];
                        var propWriter = CacheWriter(ttype);
                        var size = propWriter.BoundedSize;
                        if (size < 32)
                            size = 32;
                        var op = WriterObject;
                        var p = Expression.Variable(type, "val");
                        var sp = Expression.Assign(p, Expression.Convert(op, type));
                        var pv = Expression.Property(p, nameof(Nullable<int>.Value));
                        var exp =
                            Expression.IfThenElse(
                                Expression.Property(p, nameof(Nullable<int>.HasValue)),
                                    //  An unbounded struct is written by its (typed) writer at runtime (no boxing), also a recursive struct (being built by this thread)
                                    (propWriter == InProgressTypeInfo) || (propWriter.BoundedSize <= 0) ? GetWriteUnboundedExp(ttype, pv) : propWriter.WriteExp(pv),
                                    GetWriteConstantBufferExp(NullData)
                                    );
                        var pa = p.AsEnumerable();
                        var c = CreateProgramBlock(
                            [
                                Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(size)),
                                sp,
                                exp
                            ],
                            pa);
                            
                        var temp = Append(Append(GetTypeJson(type), TextSepComma), TextValue);

                        var c2 = CreateProgramBlock(
                            [
                                Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(size + temp.Length + 64)),
                                GetWriteConstantBufferExp(temp),
                                sp,
                                exp,
                                WriteObjectEndExp,
                            ],
                            pa);

                        var cbUnyped = Expression.Lambda<WriterDel>(c, writer, op).Compile();
                        var cbTyped = Expression.Lambda<WriterDel>(c2, writer, op).Compile();
                        ti = new TypeInfo(cbUnyped, cbTyped, false, type, c);
                    }
                }

                //  Object / struct
                if (ti == null)
                {
                    var obj = Expression.Variable(type, "v");
                    var props = type.GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(p => IsValidMember(p)).ToList();
                    if (props.Count > 1)
                        props.Sort(MemberCompare);
                    List<Expression> program = new List<Expression>(props.Count * 2 + 8);
                    List<Tuple<Byte[], MemberInfo, Func<Expression, Expression>>> simpleProps = new List<Tuple<byte[], MemberInfo, Func<Expression, Expression>>>();
                    program.Add(Expression.Assign(obj, Expression.Convert(WriterObject, type)));
                    program.Add(null);
                    Byte[] firstName = null;
                    bool needComma = false;
                    int size = 0;
                    foreach (var member in props)
                    {
                        var pi = member as PropertyInfo;
                        var fi = member as FieldInfo;
                        var isProp = pi != null;
                        var pt = isProp ? pi.PropertyType : fi.FieldType;
                        var propWriter = CacheWriter(pt);
                        if (propWriter.BoundedSize <= 0)
                            continue;
                        var declName = needComma ? BuildParameterDeclComma(member.Name) : BuildParameterDecl(member.Name);
                        firstName = firstName ?? declName;
                        size += declName.Length;
                        size += propWriter.BoundedSize;
                        var acc = isProp ? Expression.Property(obj, pi) : Expression.Field(obj, fi);
                        var writerExpression = propWriter.WriteExp(acc);
                        program.Add(GetWriteConstantBufferExp(declName));
                        program.Add(writerExpression);
                        simpleProps.Add(Tuple.Create(declName, member, propWriter.WriteExp));
                        needComma = true;
                    }
                    bool haveFixedSize = size > 0;
                    if (!haveFixedSize)
                        program.RemoveAt(1);
                    bool isSimple = true;
                    foreach (var member in props)
                    {
                        var pi = member as PropertyInfo;
                        var fi = member as FieldInfo;
                        var isProp = pi != null;
                        var pt = isProp ? pi.PropertyType : fi.FieldType;
                        var propWriter = CacheWriter(pt);
                        if (propWriter.BoundedSize > 0)
                            continue;
                        isSimple = false;
                        var declName = needComma ? BuildParameterDeclComma(member.Name) : BuildParameterDecl(member.Name);
                        firstName = firstName ?? declName;
                        program.Add(Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(declName.Length + 64)));
                        program.Add(GetWriteConstantBufferExp(declName));
                        var acc = isProp ? Expression.Property(obj, pi) : Expression.Field(obj, fi);
                        program.Add(GetWriteUnboundedExp(pt, acc));
                        needComma = true;
                    }
                    var canOpt = isSimple;
                    isSimple &= (type.IsValueType || type.IsPrimitive);
                    int simpleSize = isSimple ? (size + TextObjectBegin.Length + 64) : 0;
                    if (program.Count <= 1)
                    {
                        var temp = Append(GetTypeJson(type), TextObjectEnd);
                        var final = Expression.Block(Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(temp.Length + 64)), GetWriteConstantBufferExp(temp));
                        var t = Expression.Lambda<WriterDel>(final, writer, WriterObject).Compile();
                        ti = new TypeInfo(WriteEmptyObject, t);
                    }
                    else
                    {
                        program.Add(WriteObjectEndExp);
                        if (haveFixedSize)
                            program[1] = Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(size + TextObjectBegin.Length + 64));
                        if (ti == null)
                        {
                            var temp = Append(TextObjectBegin, firstName);
                            program[2] = GetWriteConstantBufferExp(temp);
                            var aobj = obj.AsEnumerable();
                            var finalUntyped = CreateProgramBlock(program, aobj, canOpt ? ReadDataExp : null);
                            var cbUntyped = Expression.Lambda<WriterDel>(finalUntyped, writer, WriterObject).Compile();
                            if (type.IsArray || (type.GetInterfaces().FirstOrDefault(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(ICollection<>))) != null))
                            {
                                firstName = type == typeof(Byte[]) ? TextValue : TextValues;
                            }
                            else
                            {
                                if (firstName == null)
                                    firstName = TextValue;
                            }
                            temp = Append(Append(GetTypeJson(type), TextSepComma), firstName);
                            program[1] = Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(size + temp.Length + 64));
                            program[2] = GetWriteConstantBufferExp(temp);


                            var finalTyped = CreateProgramBlock(program, aobj, canOpt ? ReadDataExp : null);
                            var cbTyped = Expression.Lambda<WriterDel>(finalTyped, writer, WriterObject).Compile();
                            //                        ti = new TypeInfo(simpleSize, cbUntyped, cbTyped);
                            ti = new TypeInfo(cbUntyped, cbTyped, false, type, finalUntyped);
                        }
                    }
                }
                if (ti == null)
                    throw new Exception("Don't know how to serializer type \"" + type.CleanTypename() + "\"");
                //  Only added here (under the BuildLock), so the type can't have been added by another thread
                if (!Writers.TryAdd(type, ti))
                    Writers.TryGetValue(type, out ti);
                return ti;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to create writer for \"" + type.CleanTypename() + "\": " + ex.Message, ex);
            }
        }


        /// <summary>
        /// An expression that writes a constant, optionally preceded by an Ensure (else the space, plus 7 bytes, must be ensured)
        /// </summary>
        static Expression GetWriteConstantBufferExp(Byte[] buffer, bool ensure = false)
        {
            var e = GetWriteConstExp(buffer);
            if (!ensure)
                return e;
            //  Ensure must be called before writing (Ensure may replace the buffer)
            return Expression.Block(Expression.Call(WriterExp, MethodBufferWriterEnsure, GetInt32Exp(buffer.Length + 64)), e);
        }

        /// <summary>
        /// Compile a writer that ensures space for and writes a constant
        /// </summary>
        static WriterConstDel GetWriteConstantBufferEnsuredAction(Byte[] buffer)
        {
            var w = WriterExp;
            var e = GetWriteConstantBufferExp(buffer, true);
            return Expression.Lambda<WriterConstDel>(e, w).Compile();
        }

        /// <summary>
        /// Compile a writer that ensures space for and writes a constant (the value is ignored)
        /// </summary>
        static WriterDel GetWriteConstantBufferEnsuredActionObject(Byte[] buffer)
        {
            var w = WriterExp;
            var e = Expression.Block(
                Expression.Call(w, MethodBufferWriterEnsure, GetInt32Exp(buffer.Length + 64)),
                GetWriteConstantBufferExp(buffer));
            return Expression.Lambda<WriterDel>(e, w, WriterObject).Compile();
        }

        #endregion//Build

        #region Runtime



        /// <summary>
        /// Create the escape table, same content as <see cref="FastFormat.Escapes"/>
        /// </summary>
        static Byte[] GetEscapeChars()
        {
            // Must be zero initialized (0 = no escape)
            var t = new Byte[128];
            // Json requires all control chars (0x00 - 0x1f) to be escaped
            for (int i = 0; i < 32; ++i)
                t[i] = 1;
            t[8] = (Byte)'b';
            t[9] = (Byte)'t';
            t[10] = (Byte)'n';
            t[12] = (Byte)'f';
            t[13] = (Byte)'r';
            t[(int)'"'] = (Byte)'"';
            t[(int)'\\'] = (Byte)'\\';
            /*              t[39] = 1;
                        t[60] = 1;
                        t[62] = 1;
            */
            return t;
        }

        static uint GetMaxEscapedChar(Byte[] escape)
        {
            int i = -1;
            int iMax = -1;
            foreach (var v in escape)
            {
                ++i;
                if (v == 0)
                    continue;
                iMax = i;
            }
            return (uint)iMax;
        }

        static readonly Byte[] EscapeChars = GetEscapeChars();
        static readonly uint MaxEscapedChar = GetMaxEscapedChar(EscapeChars);

        static readonly Char[] Hex = "0123456789abcdef".ToCharArray();
        static readonly Byte[] HexBytes = Hex.Select(x => (Byte)x).ToArray();


        /// <summary>
        /// Write chars without escaping: the low byte of every char is written, followed by the UTF8 encoding for non ASCII chars.
        /// </summary>
        /// <remarks>
        /// The output is NOT valid UTF8 for non ASCII chars (an extra byte precedes the UTF8 encoding of every char &gt;= 0x80), surrogate pairs are not combined.
        /// Not used by the writer.
        /// </remarks>
        /// <param name="d">The write position (at least count * 4 bytes must be available)</param>
        /// <param name="t">The chars</param>
        /// <param name="count">The number of chars to write</param>
        /// <returns>The position after the last written byte</returns>
        public static Byte* WriteUnescapedCharArray(Byte* d, Char[] t, int count)
        {
            for (int i = 0; i < count; ++ i)
            {
                uint c = t[i];
                *d = (Byte)c;
                ++d;
                if (c >= 0x80)
                    d = WriteUtf8(d, c);
            }
            return d;
        }

        /// <summary>
        /// Write a code point (&gt;= 0x80) as UTF8 (2 - 4 bytes), surrogate code points are encoded as is (not valid UTF8)
        /// </summary>
        /// <exception cref="Exception">The code point is above 0x10ffff</exception>
        static Byte* WriteUtf8(Byte* d, uint x)
        {
            if (x <= 0x7ff)
            {
                *d = (Byte)((x >> 6) | 0xc0);
                ++d;
                *d = (Byte)((x & 0x3f) | 0x80);
                ++d;
                return d;
            }
            if (x <= 0xffff)
            {
                *d = (Byte)((x >> 12) | 0xe0);
                ++d;
                *d = (Byte)(((x >> 6) & 0x3f) | 0x80);
                ++d;
                *d = (Byte)((x & 0x3f) | 0x80);
                ++d;
                return d;
            }
            if (x <= 0x10ffff)
            {
                *d = (Byte)((x >> 18) | 0xf0);
                ++d;
                *d = (Byte)(((x >> 12) & 0x3f) | 0x80);
                ++d;
                *d = (Byte)(((x >> 6) & 0x3f) | 0x80);
                ++d;
                *d = (Byte)((x & 0x3f) | 0x80);
                ++d;
                return d;
            }
            throw new Exception("Invalid unicode code point " + x + " (0x" + x.ToString("x8"));
        }

        /// <summary>
        /// Write an escape sequence: \uXXXX if <paramref name="e"/> is 1, else '\' followed by <paramref name="e"/>
        /// </summary>
        static Byte* WriteEscape(Byte* d, uint x, Byte e)
        {
            *d = (Byte)'\\';
            ++d;
            if (e == 1)
            {
                var h = HexBytes;
                *d = (Byte)'u';
                ++d;
                *d = h[(x >> 12) & 0xf];
                ++d;
                *d = h[(x >> 8) & 0xf];
                ++d;
                *d = h[(x >> 4) & 0xf];
                ++d;
                *d = h[(x) & 0xf];
                ++d;
            }
            else
            {
                *d = e;
                ++d;
            }
            return d;
        }


        /// <summary>
        /// Write the items of a list (without brackets). Not used by the generated writers (only 4 bytes per item are ensured, not enough for most item types).
        /// </summary>
        static void InternalList(ref BufferWriter w, Type actualType, IList values)
        {
            var l = values.Count;
            if (l <= 0)
                return;
            w.Ensure(64 + (l << 2));
            bool needComma = false;
            if (!Writers.TryGetValue(actualType, out var writer))
                writer = AddWriter(actualType);
            for (int i = 0; i < l; ++i)
            {
                var value = values[i];
                if (needComma)
                    w.Write(SepComma);
                needComma = true;
                writer.Write(ref w, value);
            }
        }

        /// <summary>
        /// Write the items of a sequence (without brackets). Not used by the generated writers.
        /// </summary>
        static void InternalEnum(ref BufferWriter w, Type actualType, IEnumerable values)
        {
            var valList = values as IList;
            if (valList != null)
            {
                InternalList(ref w, actualType, valList);
                return;
            }
            bool needComma = false;
            if (!Writers.TryGetValue(actualType, out var writer))
                writer = AddWriter(actualType);
            foreach (var value in values)
            {
                w.Ensure(64 + 16);
                if (needComma)
                    w.Write(SepComma);
                needComma = true;
                writer.Write(ref w, value);
            }
        }


        /// <summary>
        /// Write the items of a list that may contain nulls (without brackets). Not used by the generated writers.
        /// </summary>
        static void InternalMaybeNullList(ref BufferWriter w, Type actualType, IList values)
        {
            var l = values.Count;
            if (l <= 0)
                return;
            w.Ensure(64 + (l << 4));
            bool needComma = false;
            if (!Writers.TryGetValue(actualType, out var writer))
                writer = AddWriter(actualType);
            var wn = WriteNull;
            for (int i = 0; i < l; ++ i)
            {
                var value = values[i];
                if (needComma)
                    w.Write(SepComma);
                needComma = true;
                if (value == null)
                {
                    wn(ref w);
                    continue;
                }
                writer.Write(ref w, value);
            }
        }


        /// <summary>
        /// Write the items of a sequence that may contain nulls (without brackets). Not used by the generated writers.
        /// </summary>
        static void InternalMaybeNullEnum(ref BufferWriter w, Type actualType, IEnumerable values)
        {
            var valList = values as IList;
            if (valList != null)
            {
                InternalMaybeNullList(ref w, actualType, valList);
                return;
            }
            bool needComma = false;
            if (!Writers.TryGetValue(actualType, out var writer))
                writer = AddWriter(actualType);
            var wn = WriteNull;
            foreach (var value in values)
            {
                w.Ensure(64 + 16);
                if (needComma)
                    w.Write(SepComma);
                needComma = true;
                if (value == null)
                {
                    wn(ref w);
                    continue;
                }
                writer.Write(ref w, value);
            }
        }

        /// <summary>
        /// Write the items of a list that may contain nulls and derived types (without brackets). Not used by the generated writers.
        /// </summary>
        static void InternalMaybeBoxedList(ref BufferWriter w, Type expectedType, IList values)
        {
            var l = values.Count;
            if (l <= 0)
                return;
            w.Ensure(64 + (l << 4));
            var writers = Writers;
            if (!writers.TryGetValue(expectedType, out var defWriter))
                defWriter = AddWriter(expectedType);
            //  The type the reader creates for a collection / dictionary interface is written without type information
            var createdType = Helper.GetCreatedType(expectedType);
            bool needComma = false;
            var wn = WriteNull;
            for (int i = 0; i < l; ++ i)
            {
                w.Ensure(256);
                var value = values[i];
                if (needComma)
                    w.Write(SepComma);
                needComma = true;
                if (value == null)
                {
                    wn(ref w);
                    continue;
                }
                var actualType = value.GetType();
                if (expectedType == actualType)
                {
                    defWriter.Write(ref w, value);
                    continue;
                }
                if (!writers.TryGetValue(actualType, out var writer))
                    writer = AddWriter(actualType);
                if (actualType == createdType)
                {
                    writer.Write(ref w, value);
                    continue;
                }
                if (w.TypeIsOptional)
                {
                    writer.WriteOptionalTyped(ref w, value);
                    continue;
                }
                writer.WriteTyped(ref w, value);
            }

        }

        /// <summary>
        /// Write the items of a sequence that may contain nulls and derived types (without brackets). Not used by the generated writers.
        /// </summary>
        static void InternalMaybeBoxedEnum(ref BufferWriter w, Type expectedType, IEnumerable values)
        {
            var valList = values as IList;
            if (valList != null)
            {
                InternalMaybeBoxedList(ref w, expectedType, valList);
                return;
            }
            var writers = Writers;
            if (!writers.TryGetValue(expectedType, out var defWriter))
                defWriter = AddWriter(expectedType);
            //  The type the reader creates for a collection / dictionary interface is written without type information
            var createdType = Helper.GetCreatedType(expectedType);
            bool needComma = false;
            var wn = WriteNull;
            foreach (var value in values)
            {
                w.Ensure(64 + 16);
                if (needComma)
                    w.Write(SepComma);
                needComma = true;
                if (value == null)
                {
                    wn(ref w);
                    continue;
                }
                var actualType = value.GetType();
                if (expectedType == actualType)
                {
                    defWriter.Write(ref w, value);
                    continue;
                }
                if (!writers.TryGetValue(actualType, out var writer))
                    writer = AddWriter(actualType);
                if (actualType == createdType)
                {
                    writer.Write(ref w, value);
                    continue;
                }
                if (w.TypeIsOptional)
                {
                    writer.WriteOptionalTyped(ref w, value);
                    continue;
                }
                writer.WriteTyped(ref w, value);
            }
        }

        /// <summary>
        /// Convert a dictionary with an unsupported key type to a dictionary with string keys (the key's ToString(), null keys are skipped, the last value wins if keys have the same text)
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="value">The dictionary (an IEnumerable of KeyValuePair)</param>
        /// <returns>A new Dictionary with string keys</returns>
        static Object ToStringKeyed<K, V>(Object value)
        {
            var d = new Dictionary<String, V>(StringComparer.Ordinal);
            foreach (var kv in (IEnumerable<KeyValuePair<K, V>>)value)
            {
                var k = kv.Key?.ToString();
                if (k != null)
                    d[k] = kv.Value;
            }
            return d;
        }

        /// <summary>
        /// Write the entries of a dictionary as "key":value pairs (without braces), used for dictionaries without a pattern based enumerator
        /// </summary>
        /// <param name="w">The writer</param>
        /// <param name="values">The entries</param>
        /// <param name="writeKey">Writes a key</param>
        /// <param name="writeValue">Writes a value</param>
        /// <param name="needQuote">True if the key writer doesn't write a json string (the key is then quoted)</param>
        /// <param name="commaFirst">True to write a comma before the first key too (used when a "$type" member has already been written)</param>
        static void InternalKeyValueEnum<K, V>(ref BufferWriter w, IEnumerable<KeyValuePair<K, V>> values, CacheT<K>.WriterDelT writeKey, CacheT<V>.WriterDelT writeValue, bool needQuote, bool commaFirst)
        {
            bool needComma = commaFirst;
            if (needQuote)
            {
                foreach (var value in values)
                {
                    w.Ensure(64);
                    if (needComma)
                        w.Write(SepComma, SepQuote);
                    else
                        w.Write(SepQuote);
                    needComma = true;
                    writeKey(ref w, value.Key);
                    w.Write(SepQuote, SepColon);
                    w.Ensure(64);
                    writeValue(ref w, value.Value);
                }
            }
            else
            {
                foreach (var value in values)
                {
                    w.Ensure(64);
                    if (needComma)
                        w.Write(SepComma);
                    needComma = true;
                    writeKey(ref w, value.Key);
                    w.Write(SepColon);
                    w.Ensure(64);
                    writeValue(ref w, value.Value);
                }
            }
        }


        //  The primitive writers below are called with at least 64 bytes ensured (the BoundedSize)

        static void WriteUInt32(ref BufferWriter w, UInt32 value)
        {
            var org = w.DataPtr;
            w.Offset = (int)(FastFormat.WriteUInt32(org + w.Offset, value) - org);
        }

        static void WriteInt32(ref BufferWriter w, Int32 value)
        {
            var org = w.DataPtr;
            w.Offset = (int)(FastFormat.WriteInt32(org + w.Offset, value) - org);
        }

        static void WriteInt64(ref BufferWriter w, Int64 value)
        {
            var org = w.DataPtr;
            w.Offset = (int)(FastFormat.WriteInt64(org + w.Offset, value) - org);
        }

        static void WriteUInt64(ref BufferWriter w, UInt64 value)
        {
            var org = w.DataPtr;
            w.Offset = (int)(FastFormat.WriteUInt64(org + w.Offset, value) - org);
        }

        /// <summary>
        /// Write a float: integral values (that fit 32 bits) as integers, else the shortest round trippable representation ("r", invariant culture).
        /// </summary>
        /// <remarks>
        /// NaN and infinities are written as null (json has no such numbers, the reader reads null as NaN), -0 is written as -0.0.
        /// </remarks>
        static void WriteSingle(ref BufferWriter w, Single value)
        {
            if (Single.IsInteger(value))
            {
                //  The sign bit (set for -0 too, so the non negative path is unchanged)
                if (Single.IsNegative(value))
                {
                    if (value > Int32.MinValue)
                    {
                        var i = (Int32)value;
                        //  Only -0 is a negative integral value that converts to 0
                        if (i == 0)
                        {
                            WriteNegativeZero(ref w);
                            return;
                        }
                        WriteInt32(ref w, i);
                        return;
                    }
                }
                else
                {
                    if (value < UInt32.MaxValue)
                    {
                        WriteUInt32(ref w, (UInt32)value);
                        return;
                    }
                }
            }
            var org = w.DataPtr;
            var d = FastFormat.TryWriteShortSingle(org + w.Offset, value);
            if (d != null)
            {
                w.Offset = (int)(d - org);
                return;
            }
            if (!Single.IsFinite(value))
            {
                WriteNonFinite(ref w);
                return;
            }
            value.TryFormat(w.AsSpan(), out var size, "r", CultureInfo.InvariantCulture);
            w.Offset += size;
        }

        /// <summary>
        /// Write -0.0 (at least 64 bytes are ensured).
        /// With a fraction, so that readers that read an integral number as an integer before converting it (like Newtonsoft with a converter) keep the sign.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void WriteNegativeZero(ref BufferWriter w)
        {
            var o = w.Offset;
            //  "-0.0"
            Unsafe.WriteUnaligned(w.DataPtr + o, 0x302e302du);
            w.Offset = o + 4;
        }

        /// <summary>
        /// Write a NaN or an infinity as null, json has no such numbers (at least 64 bytes are ensured)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void WriteNonFinite(ref BufferWriter w)
        {
            var o = w.Offset;
            //  "null"
            Unsafe.WriteUnaligned(w.DataPtr + o, 0x6c6c756eu);
            w.Offset = o + 4;
        }

        /// <summary>
        /// Write a float dictionary key (the caller adds the quotes): like <see cref="WriteSingle"/>, but NaN and infinities are written as NaN, Infinity and -Infinity
        /// (valid json since keys are strings, and read back exactly).
        /// </summary>
        static void WriteSingleKey(ref BufferWriter w, Single value)
        {
            if (Single.IsFinite(value))
            {
                WriteSingle(ref w, value);
                return;
            }
            value.TryFormat(w.AsSpan(), out var size, "r", CultureInfo.InvariantCulture);
            w.Offset += size;
        }

        /// <summary>
        /// Write a double dictionary key (the caller adds the quotes): like <see cref="WriteDouble"/>, but NaN and infinities are written as NaN, Infinity and -Infinity
        /// (valid json since keys are strings, and read back exactly).
        /// </summary>
        static void WriteDoubleKey(ref BufferWriter w, Double value)
        {
            if (Double.IsFinite(value))
            {
                WriteDouble(ref w, value);
                return;
            }
            value.TryFormat(w.AsSpan(), out var size, "r", CultureInfo.InvariantCulture);
            w.Offset += size;
        }

        /// <summary>
        /// Write a double: integral values (that fit 64 bits) as integers, else the shortest round trippable representation ("r", invariant culture, like 1e-07 or 1.5e+300).
        /// </summary>
        /// <remarks>
        /// NaN and infinities are written as null (json has no such numbers, the reader reads null as NaN), -0 is written as -0.0.
        /// </remarks>
        static void WriteDouble(ref BufferWriter w, Double value)
        {
            if (Double.IsInteger(value))
            {
                //  The sign bit (set for -0 too, so the non negative path is unchanged)
                if (Double.IsNegative(value))
                {
                    if (value > Int64.MinValue)
                    {
                        var i = (Int64)value;
                        //  Only -0 is a negative integral value that converts to 0
                        if (i == 0)
                        {
                            WriteNegativeZero(ref w);
                            return;
                        }
                        WriteInt64(ref w, i);
                        return;
                    }
                }
                else
                {
                    if (value < UInt64.MaxValue)
                    {
                        WriteUInt64(ref w, (UInt64)value);
                        return;
                    }

                }
            }
            var org = w.DataPtr;
            var d = FastFormat.TryWriteShortDouble(org + w.Offset, value);
            if (d != null)
            {
                w.Offset = (int)(d - org);
                return;
            }
            if (!Double.IsFinite(value))
            {
                WriteNonFinite(ref w);
                return;
            }
            value.TryFormat(w.AsSpan(), out var size, "r", CultureInfo.InvariantCulture);
            w.Offset += size;
        }

        /// <summary>
        /// Write a decimal: integral values (that fit 64 bits) as integers (1.0m is written as 1), else with all decimals of its scale (1.50m is written as 1.50).
        /// </summary>
        static void WriteDecimal(ref BufferWriter w, Decimal value)
        {
            if (Decimal.IsInteger(value))
            {
                if (value < 0)
                {
                    if (value > Int64.MinValue)
                    {
                        WriteInt64(ref w, (Int64)value);
                        return;
                    }
                }
                else
                {
                    if (value < UInt64.MaxValue)
                    {
                        WriteUInt64(ref w, (UInt64)value);
                        return;
                    }

                }
            }
            var org = w.DataPtr;
            var d = FastFormat.TryWriteDecimal(org + w.Offset, value);
            if (d != null)
            {
                w.Offset = (int)(d - org);
                return;
            }
            value.TryFormat(w.AsSpan(), out var size, "r", CultureInfo.InvariantCulture);
            w.Offset += size;
        }

        /// <summary>
        /// Remove trailing zeros (and the '.' if all are zero) from the fraction of a DateTime formatted using "o" (the fraction ends at index 27)
        /// </summary>
        static void TrimDateTime(Span<Byte> dest, ref int size)
        {
            int s = 27;
            while (s > 0)
            {
                --s;
                var c = dest[s];
                if (c != '0')
                {
                    if (c != '.')
                        ++s;
                    break;
                }

            }
            var o = 27 - s;
            if (o > 0)
            {
                for (int i = 27; i < size; ++i)
                    dest[i - o] = dest[i];
                size -= o;
            }
        }

        /// <summary>
        /// Write a TimeSpan as a json string using the "c" format with trailing fraction zeros removed, like "-1.02:03:04.5"
        /// </summary>
        static void WriteTimeSpan(ref BufferWriter w, TimeSpan value)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            d = FastFormat.WriteTimeSpan(d + 1, value);
            *d = SepQuote;
            w.Offset = (int)(d + 1 - org);
        }

        /// <summary>
        /// Write a DateTime as a json string using the "o" format with trailing fraction zeros removed.
        /// Utc times ends with "Z", local times with the offset of the local time zone (at that time) and unspecified times without a suffix.
        /// </summary>
        static void WriteDateTime(ref BufferWriter w, DateTime value)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            var e = FastFormat.TryWriteDateTime(d + 1, value);
            if (e != null)
            {
                *e = SepQuote;
                w.Offset = (int)(e + 1 - org);
                return;
            }
            //  Local time (needs the time zone)
            w.Write(SepQuote);
            var dest = w.AsSpan();
            value.TryFormat(dest, out var size, "o", CultureInfo.InvariantCulture);
            TrimDateTime(dest, ref size);
            w.Offset += size;
            w.Write(SepQuote);
        }

        /// <summary>
        /// Write a DateOnly as a json string: "yyyy-MM-dd"
        /// </summary>
        static void WriteDateOnly(ref BufferWriter w, DateOnly value)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            d = FastFormat.WriteDateOnly(d + 1, value);
            *d = SepQuote;
            w.Offset = (int)(d + 1 - org);
        }

        /// <summary>
        /// Write a TimeOnly as a json string: "HH:mm:ss[.fffffff]" with trailing fraction zeros removed
        /// </summary>
        static void WriteTimeOnly(ref BufferWriter w, TimeOnly value)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            d = FastFormat.WriteTimeOnly(d + 1, value);
            *d = SepQuote;
            w.Offset = (int)(d + 1 - org);
        }

        /// <summary>
        /// Write a DateTimeOffset as a json string using the "o" format with trailing fraction zeros removed, like "2020-01-02T03:04:05.5+01:00"
        /// </summary>
        static void WriteDateTimeOffset(ref BufferWriter w, DateTimeOffset value)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            d = FastFormat.WriteDateTimeOffset(d + 1, value);
            *d = SepQuote;
            w.Offset = (int)(d + 1 - org);
        }

        /// <summary>
        /// Write a Guid as a json string using the "D" format (lower case hex with dashes)
        /// </summary>
        static void WriteGuid(ref BufferWriter w, Guid value)
        {
            w.Write(SepQuote);
            value.TryFormat(w.AsSpan(), out var size, "D");
            w.Offset += size;
            w.Write(SepQuote);
        }

        /// <summary>
        /// Base64 encoded with quotes, l + (l &gt;&gt; 1) + 64 bytes must be ensured
        /// </summary>
        static void WriteByteArray(ref BufferWriter w, Byte[] val)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            ++d;
            System.Buffers.Text.Base64.EncodeToUtf8(val, w.AsSpan().Slice(1), out _, out var written);
            d += written;
            *d = SepQuote;
            w.Offset = (int)(d + 1 - org);
        }

        /// <summary>
        /// Write a byte array as a base64 json string (or null), ensures the space it needs
        /// </summary>
        static void WriteByteArrayEnsure(ref BufferWriter w, Object o)
        {
            if (o == null)
            {
                WriteNull(ref w);
                return;
            }
            var b = (Byte[])o;
            var l = b.Length;
            w.Ensure(l + (l >> 1) + 64);
            WriteByteArray(ref w, b);
        }

        /// <summary>
        /// Write a byte array with type information: {"$type":"System.Byte[],...","$value":"base64"} (or null), ensures the space it needs
        /// </summary>
        static void WriteByteArrayTypename(ref BufferWriter w, Object obj)
        {
            if (obj == null)
            {
                WriteNull(ref w);
                return;
            }
            var b = (Byte[])obj;
            WriteTypenameByteArray(ref w);
            var l = b.Length;
            w.Ensure(l + (l >> 1) + 64);
            WriteByteArray(ref w, b);
            var o = w.Offset;
            w.DataPtr[o] = ObjectEnd;
            ++o;
            w.Offset = o;
        }

        /// <summary>
        /// Write a char as a json string (escaped if needed).
        /// A surrogate char is written as a \uXXXX escape (it can't be encoded as valid UTF8 on its own).
        /// </summary>
        static void WriteChar(ref BufferWriter w, Char value)
        {
            w.Ensure(64);
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            ++d;
            uint x = value;
            if (x < 128)
            {
                var e = EscapeChars[x];
                if (e == 0)
                {
                    *d = (Byte)x;
                    ++d;
                }
                else
                {
                    d = WriteEscape(d, x, e);
                }
            }else {
                d = Char.IsSurrogate(value) ? WriteEscape(d, x, 1) : WriteUtf8(d, x);
            }
            *d = SepQuote;
            ++d;
            w.Offset = (int)(d - org);
        }

        /// <summary>
        /// Write true or false (8 bytes are stored for false)
        /// </summary>
        static void WriteBoolean(ref BufferWriter w, Boolean value)
        {
            w.Ensure(16);
            var o = w.Offset;
            var d = w.DataPtr + o;
            if (value)
            {
                //  "true"
                Unsafe.WriteUnaligned(d, 0x65757274u);
                w.Offset = o + 4;
                return;
            }
            //  "false" (8 bytes are written, only 5 are used)
            Unsafe.WriteUnaligned(d, 0x00000065736c6166ul);
            w.Offset = o + 5;
        }

        /// <summary>
        /// Write a (non null) string as a json string, ensures the space it needs (see <see cref="FastFormat.WriteString"/>)
        /// </summary>
        /// <exception cref="OverflowException">The string is too long (more than int.MaxValue / 3 chars)</exception>
        static void WriteString(ref BufferWriter w, String value)
        {
            //  Max 3 bytes per char (escapes ensures more as needed)
            w.Ensure(checked(value.Length * 3 + 64));
            FastFormat.WriteString(ref w, value);
        }

        /// <summary>
        /// Write a surrogate pair (high surrogate <paramref name="x"/>, low surrogate <paramref name="y"/>) as a 4 byte UTF8 sequence. Unused.
        /// </summary>
        static Byte* WriteMultiUtf8(Byte* d, uint x, uint y)
        {
            x <<= 10;
            y -= 0xdc00;
            x -= ((0xd800 << 10) - 0x10000);
            y |= x;
            return WriteUtf8(d, y);

            /*            x -= 0xd800;
                        x <<= 10;
                        y -= 0xdc00;
                        x += 0x10000;
                        x |= y;
            return WriteUtf8(d, x);
            */
        }


        #endregion//Runtime


        /// <summary>
        /// Never set this to true, it only exists so that the static constructor references methods that are otherwise only used through reflection and expressions (keeps them from being trimmed); setting it makes the static constructor call them with dummy values.
        /// </summary>
        public static bool NeverSetToTrue;


        static JsonWriter()
        {
            if (NeverSetToTrue)
            {
                try
                {
                    String s = null;
                    BufferWriter w = new BufferWriter(Array.Empty<byte>());
                    Internal(ref w, s);
                    InternalMaybeNull(ref w, s);


                    WriteBoolean(ref w, false);
                    WriteChar(ref w, 'A');
                    WriteDateTime(ref w, DateTime.MinValue);


                    WriteDateOnly(ref w, DateOnly.MinValue);
                    WriteTimeOnly(ref w, TimeOnly.MinValue);
                    WriteDateTimeOffset(ref w, DateTimeOffset.MinValue);


                    WriteDecimal(ref w, 0);
                    WriteDouble(ref w, 0);
                    WriteGuid(ref w, Guid.Empty);
                    WriteInt32(ref w, 0);
                    WriteInt64(ref w, 0);
                    WriteSingle(ref w, 0);
                    WriteTimeSpan(ref w, TimeSpan.Zero);
                    WriteUInt32(ref w, 0);
                    WriteUInt64(ref w, 0);
                    //WriteNullString(ref w, s);
                    InternalEnum(ref w, null, null);
                    InternalMaybeNullEnum(ref w, null, null);
                    InternalMaybeBoxedEnum(ref w, null, null);
                    InternalKeyValueEnum<int, int>(ref w, null, null, null, false, false);
                }
                catch
                {
                }
            }
            {
                Type[] customWriters = 
                [

                    typeof(UInt32),
                    typeof(Int32),
                    typeof(UInt64),
                    typeof(Int64),

                    typeof(Single),
                    typeof(Double),
                    typeof(Decimal),

                    typeof(TimeSpan),
                    typeof(DateTime),
                    typeof(Guid),

                    typeof(DateOnly),
                    typeof(TimeOnly),
                    typeof(DateTimeOffset),

                    typeof(Boolean),
                    typeof(Char),
                ];

                var w = new LowAllocConcurrentDictionary<Type, TypeInfo>();

                w.TryAdd(typeof(Byte), new TypeInfo(typeof(Byte), true, typeof(UInt32)));
                w.TryAdd(typeof(SByte), new TypeInfo(typeof(SByte), true, typeof(Int32)));
                w.TryAdd(typeof(UInt16), new TypeInfo(typeof(UInt16), true, typeof(UInt32)));
                w.TryAdd(typeof(Int16), new TypeInfo(typeof(Int16), true, typeof(Int32)));

                foreach (var t in customWriters)
                    w.TryAdd(t, new TypeInfo(t, true));

                w.TryAdd(typeof(String), new TypeInfo((ref b, o) => { WriteString(ref b, (String)o); }, (ref b, o) => { WriteString(ref b, (String)o); }, true));
                w.TryAdd(typeof(Byte[]), new TypeInfo(WriteByteArrayEnsure, WriteByteArrayTypename));


                Writers = w;
            }
        }

        /// <summary>
        /// The writers for a type
        /// </summary>
        sealed class TypeInfo
        {
            /// <summary>
            /// Writers for an unbounded type
            /// </summary>
            /// <param name="write">Writes the value without type information</param>
            /// <param name="writeTyped">Writes the value with type information</param>
            /// <param name="typeIsOptional">If true, <see cref="WriteOptionalTyped"/> is <paramref name="write"/> (no type information), else <paramref name="writeTyped"/></param>
            /// <param name="valueType">The type of the values (used with <paramref name="untypedBody"/>)</param>
            /// <param name="untypedBody">The body of <paramref name="write"/>, for a value type it's used to build <see cref="WriteT"/> (the boxed value must only be used as Convert(value, <paramref name="valueType"/>))</param>
            public TypeInfo(WriterDel write, WriterDel writeTyped, bool typeIsOptional = false, Type valueType = null, Expression untypedBody = null)
            {
                Write = write;
                WriteTyped = writeTyped;
                WriteOptionalTyped = typeIsOptional ? write : writeTyped;
                WriteExp = exp => Expression.Invoke(Expression.Constant(write), WriterExp, exp.Type == typeof(Object) ? exp : Expression.Convert(exp, typeof(Object)));
                if ((untypedBody != null) && valueType.IsValueType)
                    WriteT = CompileTyped(valueType, untypedBody);
            }

            /// <summary>
            /// Compile a <see cref="CacheT{T}.WriterDelT"/> from the body of a <see cref="WriterDel"/>, the unboxing of the value is replaced by the (typed) value
            /// </summary>
            /// <returns>The writer, null if the body uses the boxed value in some other way</returns>
            static Delegate CompileTyped(Type valueType, Expression untypedBody)
            {
                var value = Expression.Parameter(valueType, "value");
                var r = new UnboxReplacer(valueType, value);
                var body = r.Visit(untypedBody);
                if (r.Failed)
                    return null;
                return Expression.Lambda(typeof(CacheT<>.WriterDelT).MakeGenericType(valueType), body, WriterExp, value).Compile();
            }

            /// <summary>
            /// Replaces Convert(<see cref="WriterObject"/>, type) with a typed value, fails if <see cref="WriterObject"/> is used in any other way
            /// </summary>
            sealed class UnboxReplacer : ExpressionVisitor
            {
                public UnboxReplacer(Type valueType, ParameterExpression value)
                {
                    ValueType = valueType;
                    Value = value;
                }

                readonly Type ValueType;
                readonly ParameterExpression Value;

                /// <summary>
                /// True if the boxed value is used in another way than unboxing it to the value type
                /// </summary>
                public bool Failed;

                protected override Expression VisitUnary(UnaryExpression node)
                {
                    if ((node.NodeType == ExpressionType.Convert) && (node.Operand == WriterObject) && (node.Type == ValueType))
                        return Value;
                    return base.VisitUnary(node);
                }

                protected override Expression VisitParameter(ParameterExpression node)
                {
                    if (node == WriterObject)
                        Failed = true;
                    return base.VisitParameter(node);
                }
            }

            static readonly ParameterExpression ParamObj = Expression.Parameter(typeof(Object), "o");

            /// <summary>
            /// Writers for a bounded type, using the static method named "Write" + type name of this class
            /// </summary>
            /// <remarks>
            /// The type name written with type information is always the name of <paramref name="t"/> (boxed bytes, shorts and enums used to be typed as their 32 bit / underlying type,
            /// so they were read back as the wrong type).
            /// </remarks>
            /// <param name="t">The type</param>
            /// <param name="typeIsOptional">If true, <see cref="WriteOptionalTyped"/> doesn't write type information</param>
            /// <param name="convertTo">An optional type to convert the value to before writing it (used for small integers and enums)</param>
            /// <param name="boundedSize">The max number of bytes written (the space that callers ensure)</param>
            public TypeInfo(Type t, bool typeIsOptional = false, Type convertTo = null, int boundedSize = 64)
            {
                var valueType = t;
                //  The "$type" is the name of the actual type (not the type it's converted to for writing)
                var buffer = Append(Append(GetTypeJson(t), TextSepComma), TextValue);
                Expression p;
                if (convertTo == null)
                {
                    p = Expression.Convert(ParamObj, t);
                }else
                {
                    p = Expression.Convert(Expression.Convert(ParamObj, t), convertTo);
                    t = convertTo;
                }
                //  There are no 8 and 16 bit writers, use the 32 bit ones
                var wt = (t == typeof(Byte)) || (t == typeof(UInt16)) ? typeof(UInt32) : ((t == typeof(SByte)) || (t == typeof(Int16)) ? typeof(Int32) : t);
                if (wt != t)
                    p = Expression.Convert(p, wt);
                var mi = Helper.SafeGetMethod(JsonWriterType, "Write" + wt.Name, BindingFlags.Static | BindingFlags.NonPublic);
                var w = WriterExp;
                var untyped = Expression.Call(mi, w, p);
                var typed = CreateProgramBlock(
                    [
                        Expression.Call(w, MethodBufferWriterEnsure, GetInt32Exp(buffer.Length + boundedSize)),
                        GetWriteConstantBufferExp(buffer),
                        Expression.Call(mi, w, p),
                        WriteObjectEndExp
                    ]);
                if (convertTo != null)
                    WriteExp = wt != convertTo ? (ep => Expression.Call(mi, w, Expression.Convert(Expression.Convert(ep, convertTo), wt))) : (ep => Expression.Call(mi, w, Expression.Convert(ep, convertTo)));
                else
                    WriteExp = ep => Expression.Call(mi, w, ep);
                BoundedSize = boundedSize;
                ParameterExpression[] pr = [w, ParamObj];
                var wl = Expression.Lambda<WriterDel>(untyped, pr).Compile();
                var wtl = Expression.Lambda<WriterDel>(typed, pr).Compile();
                Write = wl;
                WriteTyped = wtl;
                WriteOptionalTyped = typeIsOptional ? wl : wtl;
                //  Writes the value without boxing (the bounded types are value types)
                var value = Expression.Parameter(valueType, "value");
                WriteT = Expression.Lambda(typeof(CacheT<>.WriterDelT).MakeGenericType(valueType), WriteExp(value), w, value).Compile();
            }
            /// <summary>
            /// Builds an expression that writes a value (given as an expression), without an Ensure for bounded types
            /// </summary>
            public readonly Func<Expression, Expression> WriteExp;
            //  The maximum number of bytes required by this type, 0 = Not bounded
            /// <summary>
            /// The maximum number of bytes written by <see cref="Write"/> (that the caller must ensure), 0 if not bounded (the writer ensures the space it needs)
            /// </summary>
            public readonly int BoundedSize;
            /// <summary>
            /// Writes the value with type information
            /// </summary>
            public readonly WriterDel WriteTyped;
            /// <summary>
            /// Writes the value without type information
            /// </summary>
            public readonly WriterDel Write;
            /// <summary>
            /// The writer used for a boxed value when <see cref="BufferWriter.TypeIsOptional"/> is true (<see cref="Write"/> for primitive like types, else <see cref="WriteTyped"/>)
            /// </summary>
            public readonly WriterDel WriteOptionalTyped;
            /// <summary>
            /// Writes the value without type information, without boxing: a <see cref="CacheT{T}.WriterDelT"/> (null for reference types and value types without a typed writer)
            /// </summary>
            public readonly Delegate WriteT;
        }

        #endregion//Internal

    }

}
