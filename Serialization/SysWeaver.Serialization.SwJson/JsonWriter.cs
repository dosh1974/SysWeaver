using System;
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
    unsafe public static class JsonWriter
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
            var asm = type.Assembly.FullName.Split(',')[0];
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
            using var w = new BufferWriter(dest, destOffset)
            {
                TypeIsOptional = typeIsOptional,
            };
            InternalMaybeBoxed(w, value);
            dest = w.GetBuffer();
            return w.Position;
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
            Byte[] temp = null;
            if (dest == null)
                temp = dest = ByteBufferCache.GetTempBuffer();
            using var w = new BufferWriter(dest)
            {
                TypeIsOptional = typeIsOptional,
            };
            InternalMaybeBoxed(w, value);
            var d = w.Data;
            if (d != temp)
                ByteBufferCache.ReturnTempBuffer(temp);
            return new Memory<byte>(d, 0, w.Offset);
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
            using var w = new BufferWriter(ByteBufferCache.GetTempBuffer());
            w.TypeIsOptional = typeIsOptional;
            InternalMaybeBoxed(w, value);
            return Encoding.UTF8.GetString(w.GetBuffer(), 0, w.Offset);
        }


        #region Internal

        #region Build

        static void Internal<T>(BufferWriter w, T value) => CacheT<T>.Writer(w, value);

        static void InternalMaybeNull<T>(BufferWriter w, T value)
        {
            if (value == null)
            {
                WriteNull(w);
                return;
            }
            CacheT<T>.Writer(w, value);
        }

        static void InternalMaybeBoxed<T>(BufferWriter w, T value)
        {
            var expectedType = typeof(T);
            var actualType = value?.GetType();
            bool needType = (expectedType != actualType);
            if (needType) // Boxed or null, slow path
            {
                InternalBoxed<T>(w, value, actualType);
                return;
            }
            CacheT<T>.Writer(w, value);
        }

        static void InternalBoxed<T>(BufferWriter w, T value, Type actualType)
        {
            if (actualType == null)
            {
                WriteNull(w);
                return;
            }
            if (!Writers.TryGetValue(actualType, out var writer))
                writer = AddWriter(actualType);
            if (w.TypeIsOptional)
                writer.WriteOptionalTyped(w, value);
            else
                writer.WriteTyped(w, value);
        }

        static class CacheT<T>
        {
            static readonly Type Tp = typeof(T);
            public static readonly TypeInfo Info = AddWriter(Tp);
            public static readonly Action<BufferWriter, Object> Writer = Info.Write;
        }


        static String JsonEscape(String t)
        {
            var l = t.Length;
            var d = GC.AllocateUninitializedArray<Char>((l << 2) + (l << 1));
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
            return o == l ? t : new string(d, 0, o);
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

        static readonly ParameterExpression WriterExp = Expression.Parameter(typeof(BufferWriter), "w");
        static readonly ParameterExpression WriterObject = Expression.Parameter(typeof(Object), "value");

        static readonly Type BufferWriterType = typeof(BufferWriter);
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


        static readonly MethodInfo MethodInvoke = Helper.SafeGetMethod(typeof(Action<BufferWriter, Object>), nameof(Action<BufferWriter, Object>.Invoke), BindingFlags.Instance | BindingFlags.Public, null, [BufferWriterType, typeof(Object)], null);

        static readonly MethodInfo MethodMoveNext = Helper.SafeGetMethod(typeof(IEnumerator), nameof(IEnumerator.MoveNext), BindingFlags.Instance | BindingFlags.Public);


        static Expression MakeExpressionActionInternalT<T>() => Expression.Constant(new Action<BufferWriter, T>(Internal<T>));
        static Expression MakeExpressionActionInternalMaybeBoxedT<T>() => Expression.Constant(new Action<BufferWriter, T>(InternalMaybeBoxed<T>));
        static Expression MakeExpressionActionInternalMaybeNullT<T>() => Expression.Constant(new Action<BufferWriter, T>(InternalMaybeNull<T>));

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

        static readonly Action<BufferWriter> BooleanTrue = GetWriteConstant(Encoding.UTF8.GetBytes("true"));
        static readonly Action<BufferWriter> BooleanFalse = GetWriteConstant(Encoding.UTF8.GetBytes("false"));

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
        static readonly Action<BufferWriter, Object> NullWriter = GetWriteConstantBufferEnsuredActionObject(NullData);
        static readonly TypeInfo NullTypeInfo = new TypeInfo(NullWriter, NullWriter);

        static readonly Action<BufferWriter> WriteNull = GetWriteConstantBufferEnsuredAction(NullData);
        static readonly Action<BufferWriter> WriteEmptyObject = GetWriteConstantBufferEnsuredAction(Encoding.UTF8.GetBytes("{}"));


        static readonly Action<BufferWriter> WriteTypenameByteArray = GetWriteConstantBufferEnsuredAction(TypenameByteArray);


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
                        program.Add(Ensure64Exp);
                        program.Add(WriteObjectBeginExp);
                        program.Add(Expression.Call(mi, writer, Expression.Convert(WriterObject, ct), kmi, vmi, Expression.Constant(DictionaryKeysWithQuote.Contains(kt))));
                        program.Add(WriteObjectEndExp);
                        var finalUntyped = CreateProgramBlock(program);
                        var cbUntyped = Expression.Lambda<Action<BufferWriter, Object>>(finalUntyped, writer, WriterObject).Compile();
                        var temp = Append(TextObjectBegin, Append(GetTypeJson(type), TextSepComma));
                        program[0] = Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(temp.Length + 64));
                        program[1] = GetWriteConstantBufferExp(temp);
                        var finalTyped = CreateProgramBlock(program);
                        var cbTyped = Expression.Lambda<Action<BufferWriter, Object>>(finalTyped, writer, WriterObject).Compile();
                        ti = new TypeInfo(cbUntyped, cbTyped);
                    }
                }
                //  ICollection<T>
                colType = type.GetInterfaces().FirstOrDefault(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(ICollection<>)));
                if ((ti == null) && (colType != null))
                {
                    var pt = colType.GetGenericArguments()[0];
                    var propWriter = CacheWriter(pt);
                    var b = propWriter.WriteExp;
                    bool isPrimitive = propWriter.BoundedSize > 0;// FixedSizeWriters.TryGetValue(pt, out var b);
                    List<ParameterExpression> pes = new List<ParameterExpression>();
                    List<Expression> program = new List<Expression>();
                    if (isPrimitive)
                    {
                        var lt = typeof(ICollection<>).MakeGenericType(pt);
                        var obj = Expression.Variable(lt, "o");
                        pes.Add(obj);
                        program.Add(Expression.Assign(obj, Expression.Convert(WriterObject, lt)));
                        program.Add(Expression.Call(writer, MethodBufferWriterEnsure, Expression.Multiply(Expression.Property(obj, nameof(ICollection<int>.Count)), GetInt32Exp(propWriter.BoundedSize))));
                        program.Add(WriteArrayBeginExp);
                        var label = Expression.Label("exit");
                        var et = typeof(IEnumerable<>).MakeGenericType(pt);
                        var getEnumMi = Helper.SafeGetMethod(et, nameof(IEnumerable<int>.GetEnumerator), BindingFlags.Instance | BindingFlags.Public);
                        var ett = typeof(IEnumerator<>).MakeGenericType(pt);
                        var enumerator = Expression.Variable(ett, "e");
                        var propValue = ett.GetProperty(nameof(IEnumerator<int>.Current), BindingFlags.Instance | BindingFlags.Public);
                        pes.Add(enumerator);
                        program.Add(Expression.Assign(enumerator, Expression.Call(Expression.Convert(obj, et), getEnumMi)));
                        program.Add(Expression.IfThen(Expression.Call(enumerator, MethodMoveNext), Expression.Block(
                                b(Expression.Property(enumerator, propValue)),
                                Expression.Loop(
                                        Expression.Block(
                                            Expression.IfThen(Expression.Not(Expression.Call(enumerator, MethodMoveNext)),
                                                Expression.Break(label)),
                                            WriteCommaEndExp,
                                            b(Expression.Property(enumerator, propValue))
                                        ), label))));
                        program.Add(WriteArrayEndExp);
                    }
                    else
                    {
                        var conv = typeof(IEnumerable);
                        MethodInfo mi;
                        if (typeof(IList).IsAssignableFrom(type))
                        {
                            conv = typeof(IList);
                            if (pt.IsPrimitive || pt.IsValueType)
                            {
                                mi = MethodInternalList;
                            }
                            else
                            {
                                mi = pt.IsSealed ? MethodInternalMaybeNullList : MethodInternalMaybeBoxedList;
                            }
                        }else
                        {
                            if (pt.IsPrimitive || pt.IsValueType)
                            {
                                mi = MethodInternalEnum;
                            }
                            else
                            {
                                mi = pt.IsSealed ? MethodInternalMaybeNullEnum : MethodInternalMaybeBoxedEnum;
                            }
                        }
                        program.Add(Ensure64Exp);
                        program.Add(WriteArrayBeginExp);
                        program.Add(Expression.Call(mi, writer, Expression.Constant(pt), Expression.Convert(WriterObject, conv)));
                        program.Add(WriteArrayEndExp);
                    }
                    var finalUntyped = CreateProgramBlock(program, pes);
                    var temp = Append(Append(GetTypeJson(type), TextSepComma), TextValues);
                    program.Insert(0, Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(temp.Length + 64)));
                    program.Insert(1, GetWriteConstantBufferExp(temp));
                    program.Add(WriteObjectEndExp);

                    var finalTyped = CreateProgramBlock(program, pes);
                    var cbUntyped = Expression.Lambda<Action<BufferWriter, Object>>(finalUntyped, writer, WriterObject).Compile();
                    var cbTyped = Expression.Lambda<Action<BufferWriter, Object>>(finalTyped, writer, WriterObject).Compile();
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

                        var cbUnyped = Expression.Lambda<Action<BufferWriter, Object>>(c, writer, op).Compile();
                        var cbTyped = Expression.Lambda<Action<BufferWriter, Object>>(c2, writer, op).Compile();
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
                        if (pt.IsPrimitive || pt.IsValueType)
                        {
                            if (propWriter.BoundedSize > 0)
                            {
                                program.Add(Expression.Call(Expression.Constant(propWriter.Write), MethodInvoke, writer, Expression.Convert(acc, typeof(Object))));
                            }
                            else
                                program.Add(Expression.Call(MethodInternal.MakeGenericMethod(pt), writer, acc));
                        }
                        else
                        {
                            if (pt.IsSealed)
                            {
                                program.Add(Expression.Call(MethodInternalMaybeNull.MakeGenericMethod(pt), writer, acc));
                            }
                            else
                            {
                                program.Add(Expression.Call(MethodInternalMaybeBoxed.MakeGenericMethod(pt), writer, acc));
                            }
                        }
                        needComma = true;
                    }
                    var canOpt = isSimple;
                    isSimple &= (type.IsValueType || type.IsPrimitive);
                    int simpleSize = isSimple ? (size + TextObjectBegin.Length + 64) : 0;
                    if (program.Count <= 1)
                    {
                        var temp = Append(GetTypeJson(type), TextObjectEnd);
                        var final = Expression.Block(Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(temp.Length + 64)), GetWriteConstantBufferExp(temp));
                        var t = Expression.Lambda<Action<BufferWriter, Object>>(final, writer, WriterObject).Compile();
                        ti = new TypeInfo(EmptyObject, t);
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
                            var cbUntyped = Expression.Lambda<Action<BufferWriter, Object>>(finalUntyped, writer, WriterObject).Compile();
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
                            var cbTyped = Expression.Lambda<Action<BufferWriter, Object>>(finalTyped, writer, WriterObject).Compile();
                            //                        ti = new TypeInfo(simpleSize, cbUntyped, cbTyped);
                            ti = new TypeInfo(cbUntyped, cbTyped);
                        }
                    }
                }
                if (ti == null)
                    throw new Exception("Don't know how to serializer type \"" + type.FullName + "\"");
                if (!Writers.TryAdd(type, ti))
                    Writers.TryGetValue(type, out ti);
                return ti;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to create writer for \"" + type.FullName + "\": " + ex.Message, ex);
            }
            finally
            {
                WritersInProgress.TryRemove(type, out var _);
            }
        }

        static readonly Action<BufferWriter, Object> EmptyObject = (w, o) => WriteEmptyObject(w);


        static Byte[] GetNumberBytes(int i) => Encoding.UTF8.GetBytes(i.ToString());


        const int NumberCacheSize = 100;


        static Action<BufferWriter> GetWriteConstant(Byte[] data)
        {
            var l = data.Length;
            Expression[] code = GC.AllocateUninitializedArray<Expression>(3 + l + l);
            code[0] = ReadDataExp;
            code[1] = ReadOffsetExp;
            int d = 2;
            for (int i = 0; i < l; ++ i)
            {
                code[d] = CachedBytes[data[i]];
                ++d;
                code[d] = IncOffsetExpression;
                ++d;
            }
            code[d] = WriteOffsetExp;
            var exp = Expression.Block([ParamData, ParamOffset], code);
            return Expression.Lambda<Action<BufferWriter>>(exp, WriterExp).Compile();
        }



        static Expression GetWriteConstantBufferExp(Byte[] buffer, bool ensure = false)
        {
            var writer = WriterExp;
            var d = ParamData;
            if (buffer.Length > 24)
            {
                var size = GetInt32Exp(buffer.Length);
                var f = Expression.Field(writer, BufferWriterOffset);
                if (ensure)
                {
                    return Expression.Block(
                            d.AsEnumerable(),
                            ReadDataExp,
                            Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(buffer.Length + 64)),
                            Expression.Call(MethodBufferBlockCopy, Expression.Constant(buffer), GetInt32Exp(0), d, f, size),
                            Expression.Assign(f, Expression.Add(f, size)));
                }
                return Expression.Block(
                        d.AsEnumerable(),
                        ReadDataExp,
                        Expression.Call(MethodBufferBlockCopy, Expression.Constant(buffer), GetInt32Exp(0), d, f, size),
                        Expression.Assign(f, Expression.Add(f, size)));
            }
            List<Expression> program = new List<Expression>();
            var o = ParamOffset;
            if (ensure)
                program.Add(Expression.Call(writer, MethodBufferWriterEnsure, GetInt32Exp(buffer.Length + 64)));
            program.Add(ReadDataExp);
            program.Add(ReadOffsetExp); 
            foreach (var b in buffer)
            {
                program.Add(CachedBytes[b]);
                program.Add(IncOffsetExpression);
            }
            program.Add(WriteOffsetExp);
            return Expression.Block([d, o], program);
        }

        static Action<BufferWriter> GetWriteConstantBufferEnsuredAction(Byte[] buffer)
        {
            var w = WriterExp;
            var e = GetWriteConstantBufferExp(buffer, true);
            return Expression.Lambda<Action<BufferWriter>>(e, w).Compile();
        }

        static Action<BufferWriter, Object> GetWriteConstantBufferEnsuredActionObject(Byte[] buffer)
        {
            var w = WriterExp;
            var e = Expression.Block(
                Expression.Call(w, MethodBufferWriterEnsure, GetInt32Exp(buffer.Length + 64)),
                GetWriteConstantBufferExp(buffer));
            return Expression.Lambda<Action<BufferWriter, Object>>(e, w, WriterObject).Compile();
        }

        #endregion//Build

        #region Runtime

        static readonly Action<BufferWriter>[] NumberWriterCache = Enumerable.Range(0, NumberCacheSize + 1).Select(x => GetWriteConstant(Encoding.UTF8.GetBytes(x.ToString()))).ToArray();
        //static readonly Byte[][] NumberCache = Enumerable.Range(0, NumberCacheSize + 1).Select(x => Encoding.UTF8.GetBytes(x.ToString())).ToArray();

        static readonly Byte[] Base64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/".Select(x => (Byte)x).ToArray();

        static Byte[] GetEscapeChars()
        {
            var t = new Byte[128];
            for (int i = 0; i < 31; ++i)
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

        static void Swap(Byte* start, Byte* end)
        {
            --end;
            while (end > start)
            {
                var t = *start;
                *start = *end;
                *end = t;
                --end;
                ++start;
            }
        }


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


        static void InternalList(BufferWriter w, Type actualType, IList values)
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
                writer.Write(w, value);
            }
        }

        static void InternalEnum(BufferWriter w, Type actualType, IEnumerable values)
        {
            var valList = values as IList;
            if (valList != null)
            {
                InternalList(w, actualType, valList);
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
                writer.Write(w, value);
            }
        }


        static void InternalMaybeNullList(BufferWriter w, Type actualType, IList values)
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
                    wn(w);
                    continue;
                }
                writer.Write(w, value);
            }
        }


        static void InternalMaybeNullEnum(BufferWriter w, Type actualType, IEnumerable values)
        {
            var valList = values as IList;
            if (valList != null)
            {
                InternalMaybeNullList(w, actualType, valList);
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
                    wn(w);
                    continue;
                }
                writer.Write(w, value);
            }
        }

        static void InternalMaybeBoxedList(BufferWriter w, Type expectedType, IList values)
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
                    wn(w);
                    continue;
                }
                var actualType = value.GetType();
                if (expectedType == actualType)
                {
                    defWriter.Write(w, value);
                    continue;
                }
                if (!writers.TryGetValue(actualType, out var writer))
                    writer = AddWriter(actualType);
                if (w.TypeIsOptional)
                {
                    writer.WriteOptionalTyped(w, value);
                    continue;
                }
                writer.WriteTyped(w, value);
            }

        }

        static void InternalMaybeBoxedEnum(BufferWriter w, Type expectedType, IEnumerable values)
        {
            var valList = values as IList;
            if (valList != null)
            {
                InternalMaybeBoxedList(w, expectedType, valList);
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
                    wn(w);
                    continue;
                }
                var actualType = value.GetType();
                if (expectedType == actualType)
                {
                    defWriter.Write(w, value);
                    continue;
                }
                if (!writers.TryGetValue(actualType, out var writer))
                    writer = AddWriter(actualType);
                if (w.TypeIsOptional)
                {
                    writer.WriteOptionalTyped(w, value);
                    continue;
                }
                writer.WriteTyped(w, value);
            }
        }

        static void InternalKeyValueEnum<K, V>(BufferWriter w, IEnumerable<KeyValuePair<K, V>> values, Action<BufferWriter, K> writeKey, Action<BufferWriter, V> writeValue, bool needQuote)
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
                    writeKey(w, value.Key);
                    w.Write(SepQuote, SepColon);
                    writeValue(w, value.Value);
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
                    writeKey(w, value.Key);
                    w.Write(SepColon);
                    writeValue(w, value.Value);
                }
            }
        }


        static void WriteUInt32(BufferWriter w, UInt32 value)
        {
            if (value <= NumberCacheSize)
            {
                NumberWriterCache[value](w);
                return;
            }
            var org = w.DataPtr;
            var d = org + w.Offset;
            var start = d;
            do
            {
                var v = value;
                value /= 10;
                v += 48;
                v -= value * 10;
                *d = (Byte)v;
                ++d;
            } while (value != 0);
            Swap(start, d);
            w.Offset = (int)(d - org);
        }

        static void WriteInt32(BufferWriter w, Int32 signedValue)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            UInt32 value = (UInt32)signedValue;
            if (signedValue < 0)
            {
                *d = (Byte)('-');
                ++d;
                value = (UInt32)(-signedValue);
                ++w.Offset;
            }
            if (value <= NumberCacheSize)
            {
                NumberWriterCache[value](w);
                return;
            }
            var start = d;
            do
            {
                var v = value;
                value /= 10;
                v += 48;
                v -= value * 10;
                *d = (Byte)v;
                ++d;
            } while (value != 0);
            Swap(start, d);
            w.Offset = (int)(d - org);
        }

        static void WriteInt64(BufferWriter w, Int64 signedValue)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            UInt64 value = (UInt64)signedValue;
            if (signedValue < 0)
            {
                *d = (Byte)('-');
                ++d;
                value = (UInt64)(-signedValue);
                ++w.Offset;
            }
            if (value <= NumberCacheSize)
            {
                NumberWriterCache[value](w);
                return;
            }
            var start = d;
            do
            {
                var v = value;
                value /= 10;
                v += 48;
                v -= value * 10;
                *d = (Byte)v;
                ++d;
            } while (value != 0);
            Swap(start, d);
            w.Offset = (int)(d - org);

        }

        static void WriteUInt64(BufferWriter w, UInt64 value)
        {
            if (value <= NumberCacheSize)
            {
                NumberWriterCache[value](w);
                return;
            }
            var org = w.DataPtr;
            var d = org + w.Offset;
            var start = d;
            do
            {
                var v = value;
                value /= 10;
                v += 48;
                v -= value * 10;
                *d = (Byte)v;
                ++d;
            } while (value != 0);
            Swap(start, d);
            w.Offset = (int)(d - org);
        }

        static void WriteSingle(BufferWriter w, Single value)
        {
            if (Single.IsInteger(value))
            {
                if (value < 0)
                {
                    if (value > Int32.MinValue)
                    {
                        WriteInt32(w, (Int32)value);
                        return;
                    }
                }
                else
                {
                    if (value < UInt32.MaxValue)
                    {
                        WriteUInt32(w, (UInt32)value);
                        return;
                    }
                }
            }
            /*            var t = value.ToString("r", CultureInfo.InvariantCulture);
                        w.WriteAsciiString(t);
                        */
/* .NET 8 better?
            var o = w.Offset;
            value.TryFormat(w.Data.AsSpan(o), out var size, "r", CultureInfo.InvariantCulture);
            o += size;
            w.Offset = o;
*/
            value.TryFormat(w.AsSpan(), out var size, "r", CultureInfo.InvariantCulture);
            w.Offset += size;
        }

        static void WriteDouble(BufferWriter w, Double value)
        {
            if (Double.IsInteger(value))
            {
                if (value < 0)
                {
                    if (value > Int64.MinValue)
                    {
                        WriteInt64(w, (Int64)value);
                        return;
                    }
                }
                else
                {
                    if (value < UInt64.MaxValue)
                    {
                        WriteUInt64(w, (UInt64)value);
                        return;
                    }

                }
            }
            /*
            var t = value.ToString("r", CultureInfo.InvariantCulture);
            w.WriteAsciiString(t);
            */
            value.TryFormat(w.AsSpan(), out var size, "r", CultureInfo.InvariantCulture);
            w.Offset += size;
            //w.WriteCharTempAsAscci(size);
        }

        static void WriteDecimal(BufferWriter w, Decimal value)
        {
            if (Decimal.IsInteger(value))
            {
                if (value < 0)
                {
                    if (value > Int64.MinValue)
                    {
                        WriteInt64(w, (Int64)value);
                        return;
                    }
                }
                else
                {
                    if (value < UInt64.MaxValue)
                    {
                        WriteUInt64(w, (UInt64)value);
                        return;
                    }

                }
            }
            //var t = value.ToString(CultureInfo.InvariantCulture);
            //w.WriteAsciiString(t);
            value.TryFormat(w.AsSpan(), out var size, "r", CultureInfo.InvariantCulture);
            w.Offset += size;
            //w.WriteCharTempAsAscci(size);

        }

        static void TrimTime(Span<Byte> dest, ref int size)
        {
            while (size > 8)
            {
                --size;
                var c = dest[size];
                if (c != '0')
                {
                    if (c != '.')
                        ++size;
                    break;
                }
            }
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

        static void WriteTimeSpan(BufferWriter w, TimeSpan value)
        {
            w.Write(SepQuote);
            var dest = w.AsSpan();
            value.TryFormat(dest, out var size, "c", CultureInfo.InvariantCulture);
            TrimTime(dest, ref size);
            w.Offset += size;
            w.Write(SepQuote);
        }

        static void WriteDateTime(BufferWriter w, DateTime value)
        {
            w.Write(SepQuote);
            var dest = w.AsSpan();
            value.TryFormat(dest, out var size, "o", CultureInfo.InvariantCulture);
            TrimDateTime(dest, ref size);
            w.Offset += size;
            w.Write(SepQuote);
        }

        static void WriteDateOnly(BufferWriter w, DateOnly value)
        {
            w.Write(SepQuote);
            value.TryFormat(w.AsSpan(), out var size, "o", CultureInfo.InvariantCulture);
            w.Offset += size;
            w.Write(SepQuote);
        }

        static void WriteTimeOnly(BufferWriter w, TimeOnly value)
        {
            w.Write(SepQuote);
            var dest = w.AsSpan();
            value.TryFormat(dest, out var size, "o", CultureInfo.InvariantCulture);
            TrimTime(dest, ref size);
            w.Offset += size;
            w.Write(SepQuote);
        }

        static void WriteDateTimeOffset(BufferWriter w, DateTimeOffset value)
        {
            w.Write(SepQuote);
            var dest = w.AsSpan();
            value.TryFormat(dest, out var size, "o", CultureInfo.InvariantCulture);
            TrimDateTime(dest, ref size);
            w.Offset += size;
            w.Write(SepQuote);
        }

        static readonly int GuidLen = Guid.Empty.ToString().Length;

        static void WriteGuid(BufferWriter w, Guid value)
        {
            w.Write(SepQuote);
            value.TryFormat(w.AsSpan(), out var size, "D");
            w.Offset += size;
            w.Write(SepQuote);
        }

        static void WriteByteArray(BufferWriter w, Byte[] val)
        {
            var org = w.DataPtr;
            var d = org + w.Offset;
            var l = val.Length;
            var tripleCount = l / 3;
            var tripleEnd = tripleCount * 3;
            l -= tripleEnd;
            var b = Base64;
            *d = SepQuote;
            ++d;
            fixed (Byte* valPtr = val)
            {
                var value = valPtr;
                var valueEnd = valPtr + tripleEnd;
                while (value != valueEnd)
                {
                    uint t = *value;
                    ++value;
                    t <<= 8;
                    t |= *value;
                    ++value;
                    t <<= 8;
                    t |= *value;
                    ++value;
                    *d = b[(t >> 18)];
                    ++d;
                    *d = b[(t >> 12) & 0x3f];
                    ++d;
                    *d = b[(t >> 6) & 0x3f];
                    ++d;
                    *d = b[(t) & 0x3f];
                    ++d;
                }
                switch (l)
                {
                    case 1:
                        {
                            uint t = *value;
                            t <<= 16;
                            *d = b[(t >> 18)];
                            ++d;
                            *d = b[(t >> 12) & 0x3f];
                            ++d;
                            *d = (Byte)'=';
                            ++d;
                            *d = (Byte)'=';
                            ++d;
                        }
                        break;
                    case 2:
                        {
                            uint t = *value;
                            ++value;
                            t <<= 8;
                            t |= *value;
                            t <<= 8;
                            *d = b[(t >> 18)];
                            ++d;
                            *d = b[(t >> 12) & 0x3f];
                            ++d;
                            *d = b[(t >> 6) & 0x3f];
                            ++d;
                            *d = (Byte)'=';
                            ++d;
                        }
                        break;
                }
            }
            *d = SepQuote;
            ++d;
            w.Offset = (int)(d - org);
        }

        static void WriteByteArrayEnsure(BufferWriter w, Object o)
        {
            if (o == null)
            {
                WriteNull(w);
                return;
            }
            var b = (Byte[])o;
            var l = b.Length;
            w.Ensure(l + (l >> 1) + 64);
            WriteByteArray(w, b);
        }

        static void WriteByteArrayTypename(BufferWriter w, Object obj)
        {
            if (obj == null)
            {
                WriteNull(w);
                return;
            }
            var b = (Byte[])obj;
            WriteTypenameByteArray(w);
            var l = b.Length;
            w.Ensure(l + (l >> 1) + 64);
            WriteByteArray(w, b);
            var o = w.Offset;
            w.DataPtr[o] = ObjectEnd;
            ++o;
            w.Offset = o;
        }

        static void WriteChar(BufferWriter w, Char value)
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

        static void WriteBoolean(BufferWriter w, Boolean value)
        {
            w.Ensure(16);
            (value ? BooleanTrue : BooleanFalse)(w);
        }

        static void WriteString(BufferWriter w, String value)
        {
            var l = value.Length;
            var count = (l << 2) + 64;
            w.Ensure(count);
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = SepQuote;
            ++d;
            fixed (byte* esc = EscapeChars)
            {
                fixed (char* srcVal = value)
                {
                    var src = srcVal;
                    var end = srcVal + l;
                    while (src != end)
                    {
                        uint x = *src;
                        if ((x >= 128) || (esc[x] != 0))
                            break;
                        ++src;
                        *d = (Byte)x;
                        ++d;
                    }
                    while (src < end)
                    {
                        uint x = *src;
                        ++src;
                        if (x < 128)
                        {
                            var e = esc[x];
                            if (e == 0)
                            {
                                *d = (Byte)x;
                                ++d;
                                continue;
                            }
                            d = WriteEscape(d, x, e);
                            continue;
                        }
                        if ((x < 0xd800) || (x > 0xdbff))
                        {
                            d = WriteUtf8(d, x);
                            continue;
                        }
                        d = WriteMultiUtf8(d, x, *src);
                        ++src;
                    }
                }
            }
            *d = SepQuote;
            ++d;
            w.Offset = (int)(d - org);
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
                    Internal(null, s);
                    InternalMaybeNull(null, s);


                    WriteBoolean(null, false);
                    WriteChar(null, 'A');
                    WriteDateTime(null, DateTime.MinValue);


                    WriteDateOnly(null, DateOnly.MinValue);
                    WriteTimeOnly(null, TimeOnly.MinValue);
                    WriteDateTimeOffset(null, DateTimeOffset.MinValue);


                    WriteDecimal(null, 0);
                    WriteDouble(null, 0);
                    WriteGuid(null, Guid.Empty);
                    WriteInt32(null, 0);
                    WriteInt64(null, 0);
                    WriteSingle(null, 0);
                    WriteTimeSpan(null, TimeSpan.Zero);
                    WriteUInt32(null, 0);
                    WriteUInt64(null, 0);
                    //WriteNullString(null, s);
                    InternalEnum(null, null, null);
                    InternalMaybeNullEnum(null, null, null);
                    InternalMaybeBoxedEnum(null, null, null);
                    InternalKeyValueEnum<int, int>(null, null, null, null, false);
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

                w.TryAdd(typeof(String), new TypeInfo((b, o) => { WriteString(b, (String)o); }, (b, o) => { WriteString(b, (String)o); }, true));
                w.TryAdd(typeof(Byte[]), new TypeInfo(WriteByteArrayEnsure, WriteByteArrayTypename));


                Writers = w;
            }
        }

        sealed class TypeInfo
        {
            public TypeInfo(Action<BufferWriter, Object> write, Action<BufferWriter, Object> writeTyped, bool typeIsOptional = false)
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
                var mi = Helper.SafeGetMethod(JsonWriterType, "Write" + t.Name, BindingFlags.Static | BindingFlags.NonPublic);
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
                    WriteExp = ep => Expression.Call(mi, w, Expression.Convert(ep, convertTo));
                else
                    WriteExp = ep => Expression.Call(mi, w, ep);
                BoundedSize = boundedSize;
                ParameterExpression[] pr = [w, ParamObj];
                var wl = Expression.Lambda<Action<BufferWriter, Object>>(untyped, pr).Compile();
                var wtl = Expression.Lambda<Action<BufferWriter, Object>>(typed, pr).Compile();
                Write = wl;
                WriteTyped = wtl;
                WriteOptionalTyped = typeIsOptional ? wl : wtl;
            }
            public readonly Func<Expression, Expression> WriteExp;
            //  The maximum number of bytes required by this type, 0 = Not bounded
            public readonly int BoundedSize;
            public readonly Action<BufferWriter, Object> WriteTyped;
            public readonly Action<BufferWriter, Object> Write;
            public readonly Action<BufferWriter, Object> WriteOptionalTyped;
        }



        #endregion//Internal

    }

}
