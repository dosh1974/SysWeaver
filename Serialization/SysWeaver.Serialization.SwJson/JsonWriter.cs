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
using SysWeaver.Serialization.SwJson.Writer;

namespace SysWeaver.Serialization.SwJson
{

    /// <summary>
    /// Methods for serializing an object to a buffer
    /// </summary>
    [SkipLocalsInit]
    unsafe public static partial class JsonWriter
    {

        /// <summary>
        /// Remapping of assembly names for type names 
        /// </summary>
        public static readonly Dictionary<String, String> AssemblyMap = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Remapping of namespaces for type names
        /// </summary>
        public static readonly Dictionary<String, String> NamespaceMap = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Function that maps from a Type to an assembly qualified typename
        /// </summary>
        public static Func<Type, String> ToTypename = DefaultTypename;


        /// <summary>
        /// The default mapping of a type to a type-name, uses the AssemblyMap nad NamespaceMap to adjust name
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static String DefaultTypename(Type type)
        {
            var tn = type.FullName;
            var asm = type.Assembly.FullName.SplitFirst(',');
            if (AssemblyMap.TryGetValue(asm, out var na))
                asm = na;
            var ns = NamespaceMap;
            if (ns.Count > 0)
            {
                var li = tn.LastIndexOf('.');
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
        /// Convert an object to bytes (UTF8 encoded string), using an exisitng buffer
        /// </summary>
        /// <typeparam name="T">Type of object, implicit</typeparam>
        /// <param name="dest">Destination buffer, can be null, will be reallocated if more space is needed</param>
        /// <param name="value">The object to convert to json</param>
        /// <param name="destOffset">An optional write offset</param>
        /// <param name="typeIsOptional">If true, some primitive boxed values are written without type information to be compatible with old Newtonsoft.Json versions, if false boxed data is always round-trippable</param>
        /// <returns>The number of bytes written to the buffer</returns>
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
                    InternalMaybeBoxed(ref w, value);
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
        /// <typeparam name="T">Type of object, implicit</typeparam>
        /// <param name="value">The object to convert to json</param>
        /// <param name="dest">An optional temporary buffer to use (if big enough one buffer allocation is avoided)</param>
        /// <param name="typeIsOptional">If true, some primitive boxed values are written without type information to be compatible with old Newtonsoft.Json versions, if false boxed data is always round-trippable</param>
        /// <returns>The object as UTF8 encoded json</returns>
        public static Memory<Byte> ToJsonBytes<T>(T value, Byte[] dest = null, bool typeIsOptional = true)
        {
            //  Without a destination (or if it's too small), the writer uses (and grows with) rented buffers, the result is then copied to an array of the exact size
            var rented = dest == null;
            var b = rented ? ArrayPoolStream.Rent(InitialRentSize) : dest;
            //  Pinning with fixed is cheaper than a GCHandle
            fixed (Byte* p = b)
            {
                var w = new BufferWriter(b, p, 0, rented, rented ? InitialRentSize : b.Length)
                {
                    TypeIsOptional = typeIsOptional,
                };
                try
                {
                    //  Primitives are written without an Ensure (the caller ensures the bounded size)
                    w.Ensure(64);
                    InternalMaybeBoxed(ref w, value);
                    var l = w.Offset;
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
        /// <typeparam name="T">Type of object, implicit</typeparam>
        /// <param name="value">The object to convert to json</param>
        /// <param name="typeIsOptional">If true, some primitive boxed values are written without type information to be compatible with old Newtonsoft.Json versions, if false boxed data is always round-trippable</param>
        /// <returns>The object as a json string</returns>
        public static String ToJsonString<T>(T value, bool typeIsOptional = true)
        {
            //  The writer uses (and grows with) rented buffers, only the string is allocated
            var temp = ArrayPoolStream.Rent(InitialRentSize);
            //  Pinning with fixed is cheaper than a GCHandle
            fixed (Byte* p = temp)
            {
                var w = new BufferWriter(temp, p, 0, true, InitialRentSize)
                {
                    TypeIsOptional = typeIsOptional
                };
                try
                {
                    //  Primitives are written without an Ensure (the caller ensures the bounded size)
                    w.Ensure(64);
                    InternalMaybeBoxed(ref w, value);
                    return Encoding.UTF8.GetString(w.DataPtr, w.Offset);
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
        /// The initial capacity of a rented buffer that is copied to the caller (ToJsonBytes with a null dest), the size of the copy is the capacity (it grows in small steps)
        /// </summary>
        const int InitialCapacity = 256;


        #region Internal

        #region Build

        static void Internal<T>(ref BufferWriter w, T value) => CacheT<T>.Writer(ref w, value);

        static void InternalMaybeNull<T>(ref BufferWriter w, T value)
        {
            if (value == null)
            {
                WriteNull(ref w);
                return;
            }
            CacheT<T>.Writer(ref w, value);
        }

        static void InternalMaybeBoxed<T>(ref BufferWriter w, T value)
        {
            var expectedType = typeof(T);
            var actualType = value?.GetType();
            bool needType = (expectedType != actualType);
            if (needType) // Boxed or null, slow path
            {
                InternalBoxed<T>(ref w, value, actualType);
                return;
            }
            CacheT<T>.Writer(ref w, value);
        }

        static void InternalBoxed<T>(ref BufferWriter w, T value, Type actualType)
        {
            if (actualType == null)
            {
                WriteNull(ref w);
                return;
            }
            if (!Writers.TryGetValue(actualType, out var writer))
                writer = AddWriter(actualType);
            if (w.TypeIsOptional)
                writer.WriteOptionalTyped(ref w, value);
            else
                writer.WriteTyped(ref w, value);
        }


        public delegate void WriterDel(ref BufferWriter w, Object o);

        static class CacheT<T>
        {
            public delegate void WriterDelT(ref BufferWriter w, T o);

            static readonly Type Tp = typeof(T);
            public static readonly TypeInfo Info = AddWriter(Tp);
            public static readonly WriterDel Writer = Info.Write;
        }


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
            typeof(String),
            typeof(Boolean),
            typeof(TimeSpan),
            typeof(DateTime),
            typeof(Guid),
        }.ToFrozenSet();

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
            typeof(Boolean),
        }.ToFrozenSet();

        static readonly ConcurrentDictionary<Type, TypeInfo> Writers;


        static readonly Byte[] NullData = Encoding.UTF8.GetBytes("null");
        static readonly WriterDel NullWriter = GetWriteConstantBufferEnsuredActionObject(NullData);
        static readonly TypeInfo NullTypeInfo = new TypeInfo(NullWriter, NullWriter);

        static readonly WriterConstDel WriteNull = GetWriteConstantBufferEnsuredAction(NullData);
        static readonly WriterDel WriteEmptyObject = GetWriteConstantBufferEnsuredActionObject(Encoding.UTF8.GetBytes("{}"));


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


        static bool IsValidMember(MemberInfo m)
        {
            {
                var p = m as PropertyInfo;
                if (p != null)
                    return p.CanRead && p.CanWrite;
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

        static readonly ConcurrentDictionary<Type, Object> WritersInProgress = new ConcurrentDictionary<Type, object>();

        static TypeInfo AddWriter(Type type)
        {
            if (!WritersInProgress.TryAdd(type, new object()))
                return CacheT<Object>.Info;
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
                var colType = type.GetInterfaces().FirstOrDefault(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(IDictionary<,>)));
                if ((ti == null) && (colType != null))
                {
                    var kt = colType.GetGenericArguments()[0];
                    if (AllowedDictionaryKeys.Contains(kt))
                    {
                        var pt = colType.GetGenericArguments()[1];
                        CacheWriter(kt);
                        CacheWriter(pt);
                        var kvt = typeof(KeyValuePair<,>).MakeGenericType(kt, pt);
                        var ct = typeof(IEnumerable<>).MakeGenericType(kvt);
                        Expression kmi;
                        if (kt.IsPrimitive || kt.IsValueType)
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
                        List<Expression> program = new List<Expression>();
                        List<ParameterExpression> pes = new List<ParameterExpression>();
                        program.Add(Ensure64Exp);
                        program.Add(WriteObjectBeginExp);
                        if (TryGetEnumeratorPattern(type, out _, out _, out var kvCurrent) && (kvCurrent.PropertyType == kvt))
                        {
                            //  Like InternalKeyValueEnum but without interface and delegate calls (and boxing)
                            var dobj = Expression.Variable(type, "dict");
                            var kv = Expression.Variable(kvt, "kv");
                            pes.Add(dobj);
                            pes.Add(kv);
                            program.Add(Expression.Assign(dobj, Expression.Convert(WriterObject, type)));
                            var keyWriter = CacheWriter(kt);
                            var valueWriter = CacheWriter(pt);
                            var needQuote = DictionaryKeysWithQuote.Contains(kt);
                            var keyQuote = Encoding.UTF8.GetBytes("\"");
                            var keyQuoteComma = Encoding.UTF8.GetBytes(",\"");
                            var keyEndQuote = Encoding.UTF8.GetBytes("\":");
                            var keyEnd = Encoding.UTF8.GetBytes(":");
                            var comma = Encoding.UTF8.GetBytes(",");
                            program.Add(GetLoopExp(type, kvt, dobj, (item, isFirst) =>
                            {
                                var key = Expression.Property(kv, nameof(KeyValuePair<int, int>.Key));
                                var value = Expression.Property(kv, nameof(KeyValuePair<int, int>.Value));
                                List<Expression> e = new List<Expression>(8);
                                e.Add(Expression.Assign(kv, item));
                                e.Add(Ensure64Exp);
                                if (needQuote)
                                    e.Add(GetWriteConstExp(isFirst ? keyQuote : keyQuoteComma));
                                else if (!isFirst)
                                    e.Add(GetWriteConstExp(comma));
                                e.Add(GetWriteValueExp(kt, keyWriter, key));
                                e.Add(Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(16)));
                                e.Add(GetWriteConstExp(needQuote ? keyEndQuote : keyEnd));
                                e.Add(GetWriteValueExp(pt, valueWriter, value));
                                return Expression.Block(e);
                            }, pes));
                        }
                        else
                        {
                            program.Add(Expression.Call(mi, writer, Expression.Convert(WriterObject, ct), kmi, vmi, Expression.Constant(DictionaryKeysWithQuote.Contains(kt))));
                        }
                        program.Add(Ensure64Exp);
                        program.Add(WriteObjectEndExp);
                        var finalUntyped = CreateProgramBlock(program, pes);
                        var cbUntyped = Expression.Lambda<WriterDel>(finalUntyped, writer, WriterObject).Compile();
                        var temp = Append(TextObjectBegin, Append(GetTypeJson(type), TextSepComma));
                        program[0] = Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(temp.Length + 64));
                        program[1] = GetWriteConstantBufferExp(temp);
                        var finalTyped = CreateProgramBlock(program, pes);
                        var cbTyped = Expression.Lambda<WriterDel>(finalTyped, writer, WriterObject).Compile();
                        ti = new TypeInfo(cbUntyped, cbTyped);
                    }
                }
                //  ICollection<T>
                colType = type.GetInterfaces().FirstOrDefault(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(ICollection<>)));
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
                        var exp =
                            Expression.IfThenElse(
                                Expression.Property(p, nameof(Nullable<int>.HasValue)),
                                    propWriter.WriteExp(Expression.Property(p, nameof(Nullable<int>.Value))),
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
                        ti = new TypeInfo(cbUnyped, cbTyped);
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
                            ti = new TypeInfo(cbUntyped, cbTyped);
                        }
                    }
                }
                if (ti == null)
                    throw new Exception("Don't know how to serializer type \"" + type.CleanTypename() + "\"");
                if (!Writers.TryAdd(type, ti))
                    Writers.TryGetValue(type, out ti);
                return ti;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to create writer for \"" + type.CleanTypename() + "\": " + ex.Message, ex);
            }
            finally
            {
                WritersInProgress.TryRemove(type, out var _);
            }
        }


        static Expression GetWriteConstantBufferExp(Byte[] buffer, bool ensure = false)
        {
            var e = GetWriteConstExp(buffer);
            if (!ensure)
                return e;
            //  Ensure must be called before writing (Ensure may replace the buffer)
            return Expression.Block(Expression.Call(WriterExp, MethodBufferWriterEnsure, GetInt32Exp(buffer.Length + 64)), e);
        }

        static WriterConstDel GetWriteConstantBufferEnsuredAction(Byte[] buffer)
        {
            var w = WriterExp;
            var e = GetWriteConstantBufferExp(buffer, true);
            return Expression.Lambda<WriterConstDel>(e, w).Compile();
        }

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

        static void InternalMaybeBoxedList(ref BufferWriter w, Type expectedType, IList values)
        {
            var l = values.Count;
            if (l <= 0)
                return;
            w.Ensure(64 + (l << 4));
            var writers = Writers;
            if (!writers.TryGetValue(expectedType, out var defWriter))
                defWriter = AddWriter(expectedType);
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
                if (w.TypeIsOptional)
                {
                    writer.WriteOptionalTyped(ref w, value);
                    continue;
                }
                writer.WriteTyped(ref w, value);
            }

        }

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
                if (w.TypeIsOptional)
                {
                    writer.WriteOptionalTyped(ref w, value);
                    continue;
                }
                writer.WriteTyped(ref w, value);
            }
        }

        static void InternalKeyValueEnum<K, V>(ref BufferWriter w, IEnumerable<KeyValuePair<K, V>> values, CacheT<K>.WriterDelT writeKey, CacheT<V>.WriterDelT writeValue, bool needQuote)
        {
            bool needComma = false;
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

        static void WriteSingle(ref BufferWriter w, Single value)
        {
            if (Single.IsInteger(value))
            {
                if (value < 0)
                {
                    if (value > Int32.MinValue)
                    {
                        WriteInt32(ref w, (Int32)value);
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
            value.TryFormat(w.AsSpan(), out var size, "r", CultureInfo.InvariantCulture);
            w.Offset += size;
        }

        static void WriteDouble(ref BufferWriter w, Double value)
        {
            if (Double.IsInteger(value))
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
            var d = FastFormat.TryWriteShortDouble(org + w.Offset, value);
            if (d != null)
            {
                w.Offset = (int)(d - org);
                return;
            }
            value.TryFormat(w.AsSpan(), out var size, "r", CultureInfo.InvariantCulture);
            w.Offset += size;
        }

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

        static void WriteTimeSpan(ref BufferWriter w, TimeSpan value)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            d = FastFormat.WriteTimeSpan(d + 1, value);
            *d = SepQuote;
            w.Offset = (int)(d + 1 - org);
        }

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

        static void WriteDateOnly(ref BufferWriter w, DateOnly value)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            d = FastFormat.WriteDateOnly(d + 1, value);
            *d = SepQuote;
            w.Offset = (int)(d + 1 - org);
        }

        static void WriteTimeOnly(ref BufferWriter w, TimeOnly value)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            d = FastFormat.WriteTimeOnly(d + 1, value);
            *d = SepQuote;
            w.Offset = (int)(d + 1 - org);
        }

        static void WriteDateTimeOffset(ref BufferWriter w, DateTimeOffset value)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            d = FastFormat.WriteDateTimeOffset(d + 1, value);
            *d = SepQuote;
            w.Offset = (int)(d + 1 - org);
        }

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
                d = WriteUtf8(d, x);
            }
            *d = SepQuote;
            ++d;
            w.Offset = (int)(d - org);
        }

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

        static void WriteString(ref BufferWriter w, String value)
        {
            //  Max 3 bytes per char (escapes ensures more as needed)
            w.Ensure(checked(value.Length * 3 + 64));
            FastFormat.WriteString(ref w, value);
        }

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
                    InternalKeyValueEnum<int, int>(ref w, null, null, null, false);
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

                var w = new ConcurrentDictionary<Type, TypeInfo>();

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

        sealed class TypeInfo
        {
            public TypeInfo(WriterDel write, WriterDel writeTyped, bool typeIsOptional = false)
            {
                Write = write;
                WriteTyped = writeTyped;
                WriteOptionalTyped = typeIsOptional ? write : writeTyped;
                WriteExp = exp => Expression.Invoke(Expression.Constant(write), WriterExp, exp.Type == typeof(Object) ? exp : Expression.Convert(exp, typeof(Object)));
            }

            static readonly ParameterExpression ParamObj = Expression.Parameter(typeof(Object), "o");

            public TypeInfo(Type t, bool typeIsOptional = false, Type convertTo = null, int boundedSize = 64)
            {
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
                var buffer = Append(Append(GetTypeJson(t), TextSepComma), TextValue);
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
            }
            public readonly Func<Expression, Expression> WriteExp;
            //  The maximum number of bytes required by this type, 0 = Not bounded
            public readonly int BoundedSize;
            public readonly WriterDel WriteTyped;
            public readonly WriterDel Write;
            public readonly WriterDel WriteOptionalTyped;
        }

        #endregion//Internal

    }

}
