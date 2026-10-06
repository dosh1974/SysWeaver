using System;
using System.Collections.Generic;

using System.Reflection;
using System.Globalization;
using System.Linq.Expressions;
using System.Collections.Concurrent;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Collections.Frozen;

namespace SysWeaver.Serialization.SwJson.Reader
{
    /// <summary>
    /// The compiled readers for a type, created on first use and cached forever (thread safe).
    /// Also sets <see cref="ReadTyped{T}.Create"/> for the type.
    /// </summary>
    /// <remarks>
    /// The reader expression depends on the type: numbers, dates, <see cref="Guid"/> and <see cref="Boolean"/> use <see cref="SpanParsers"/> (date/time types and <see cref="Guid"/> must be quoted),
    /// strings, <see cref="Char"/>, <see cref="Byte"/> arrays and <see cref="Object"/> are special cased, then enums, arrays, generic collections with one type argument, nullable value types,
    /// reference types (including dictionaries) and finally other value types.
    /// </remarks>
    sealed class ReadTypeCache
    {
        static readonly ConcurrentDictionary<Type, ReadTypeCache> Cache = new ConcurrentDictionary<Type, ReadTypeCache>();

        /// <summary>
        /// Get (or create) the readers for a type.
        /// </summary>
        /// <remarks>Creation is serialized by a global lock, element types of arrays and collections are created too.</remarks>
        /// <param name="t">The type to read</param>
        /// <returns>The cached readers</returns>
        public static ReadTypeCache Get(Type t)
        {
            var cache = Cache;
            if (cache.TryGetValue(t, out var cv))
                return cv;
            lock (cache)
            {
                if (cache.TryGetValue(t, out cv))
                    return cv;
                if (t.IsEnum)
                {
                    cv = new ReadTypeCache(t, Expression.Call(MethodReadEnum.MakeGenericMethod(t), TempCall));
                    //                    cv = new TypeCache(t, false, v => Expression.Call(EnumParse, Expression.Constant(t), v));
                    cache.TryAdd(t, cv);
                    return cv;
                }
                if (t.IsArray)
                {
                    var et = t.GetElementType();
                    var f = Expression.Call(MethodCreateArray.MakeGenericMethod(et), TempCall);
                    cv = new ReadTypeCache(t, f);
                    cache.TryAdd(t, cv);
                    Get(et);
                    return cv;
                }
                if (t.IsGenericType)
                {
                    var at = t.GetGenericArguments();
                    if (at.Length < 2)
                    {
                        var et = GetCollectionElementType(t);
                        if (et != null)
                        {
                            if (typeof(ICollection<>).MakeGenericType(et).IsAssignableFrom(t))
                            {
                                var f = Expression.Call(MethodCreateCollection.MakeGenericMethod(et, t), TempCall);
                                cv = new ReadTypeCache(t, f);
                                cache.TryAdd(t, cv);
                                Get(et);
                                return cv;
                            }
                            if (et.IsValueType && typeof(Nullable<>).MakeGenericType(et) == t)
                            {
                                var ecv = Get(et);
                                var f = Expression.Condition(
                                    Expression.Call(MethodIsNull, ParState),
                                    Expression.Constant(null, t),
                                    Expression.Convert(ecv.CreateExp, t));
                                cv = new ReadTypeCache(t, f);
                                cache.TryAdd(t, cv);
                                return cv;
                            }
                        }
                    }
                }
                if (!(t.IsPrimitive || t.IsValueType))
                {
                    var f = Expression.Call((t.IsSealed ? MethodCreateSealedNullableObject : MethodCreateNullableObject).MakeGenericMethod(t), TempCall);
                    cv = new ReadTypeCache(t, f);
                    cache.TryAdd(t, cv);
                    return cv;
                }
                {
                    var f = Expression.Call(MethodCreateStruct.MakeGenericMethod(t), TempCall);
                    cv = new ReadTypeCache(t, f);
                    cache.TryAdd(t, cv);
                    return cv;
                }
            }
        }


        /// <summary>
        /// Create a new (empty) instance of the type, boxed, see <see cref="ReadTyped{T}.CreateNewBoxed"/>.
        /// </summary>
        public readonly Func<Object> CreateNewBoxed;

        static readonly Type JsonParserType = typeof(Utf8JsonParser);


        static readonly MethodInfo MethodReadAsciiMaybeQuoted = Helper.SafeGetMethod(JsonParserType, nameof(Utf8JsonParser.ReadAsciiMaybeQuoted), BindingFlags.Static | BindingFlags.Public);
        static readonly MethodInfo MethodReadAsciiQuotedString = Helper.SafeGetMethod(JsonParserType, nameof(Utf8JsonParser.ReadAsciiQuotedString), BindingFlags.Static | BindingFlags.Public);
        static readonly MethodInfo MethodReadQuotedString = Helper.SafeGetMethod(JsonParserType, nameof(Utf8JsonParser.ReadQuotedString), BindingFlags.Static | BindingFlags.Public);

        /// <summary>
        /// The parser state parameter of all compiled readers.
        /// </summary>
        public static readonly ParameterExpression ParState = Expression.Parameter(typeof(JsonParserState), "state");
        /// <summary>
        /// The end condition parameter of all compiled readers.
        /// </summary>
        public static readonly ParameterExpression ParEndOn = Expression.Parameter(typeof(Func<char, bool>), "end");
        static readonly ParameterExpression ParHeader = Expression.Parameter(typeof(ReadOnlySpan<byte>), "h");

        static readonly Expression ExpReadAsciiMaybeQuoted = Expression.Call(MethodReadAsciiMaybeQuoted, ParState, ParEndOn);
        static readonly Expression ExpReadAsciiQuotedString = Expression.Call(MethodReadAsciiQuotedString, ParState);
        static readonly Expression ExpReadQuotedString = Expression.Call(MethodReadQuotedString, ParState);

        static readonly ParameterExpression ParString = Expression.Parameter(typeof(string), "value");

        static readonly ConstantExpression ConstInvariant = Expression.Constant(CultureInfo.InvariantCulture);

        /// <summary>
        /// The parameters of a compiled reader: (<see cref="ParState"/>, <see cref="ParEndOn"/>).
        /// </summary>
        public static readonly ParameterExpression[] TempCall =
        [
            ParState, ParEndOn
        ];

        /// <summary>
        /// <see cref="RuntimeHelpers.GetUninitializedObject(Type)"/>, used to create types without a public parameterless constructor.
        /// </summary>
        public static MethodInfo MethodGetUninitializedObject = Helper.SafeGetMethod(typeof(RuntimeHelpers), nameof(RuntimeHelpers.GetUninitializedObject), BindingFlags.Static | BindingFlags.Public);


        /// <summary>
        /// Read a (boxed) value at the current position.
        /// </summary>
        public delegate object Creator(JsonParserState state, Func<char, bool> endOn);
        /// <summary>
        /// Create and populate an object, given the already read first member name (<paramref name="header"/>).
        /// </summary>
        public delegate object CreateAndPopulateDel(ReadOnlySpan<byte> header, JsonParserState state, Func<char, bool> endOn);

        /// <summary>
        /// Set <see cref="ReadTyped{T}.Create"/> for the type (using reflection).
        /// </summary>
        static void SetStatic(Type t, Expression v)
        {
            Helper.SafeGetMethod(typeof(ReadTyped<>).MakeGenericType(t), nameof(ReadTyped<int>.Set), BindingFlags.Static | BindingFlags.Public).Invoke(null, [v.Type == t ? v : Expression.Convert(v, t)]);
        }

        /// <summary>
        /// Create the readers for a type that is read from a (quoted, escaped) string.
        /// </summary>
        /// <param name="t">The type</param>
        /// <param name="onStringExp">Converts the string read expression to an expression of the type</param>
        public ReadTypeCache(Type t, Func<Expression, Expression> onStringExp)
        {
#if VERBOSE
            try
            {
#endif//VERBOSE
                CreateNewBoxed = Expression.Lambda<Func<Object>>(Expression.Call(Helper.SafeGetMethod(typeof(ReadTyped<>).MakeGenericType(t), nameof(ReadTyped<int>.CreateNewBoxed), BindingFlags.Static | BindingFlags.Public))).Compile();
                var v = onStringExp(ExpReadQuotedString);
            CreateExp = v.Type == t ? v : Expression.Convert(v, t);
            SetStatic(t, v);
            if (v.Type != typeof(object))
                v = Expression.Convert(v, typeof(object));
            Create = Expression.Lambda<Creator>(v, ParState, ParEndOn).Compile();
            if (t.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null) != null)
                Cp = Expression.Lambda<CreateAndPopulateDel>(Expression.Call(MethodNewAndPopulate.MakeGenericMethod(t), ParHeader, ParState, ParEndOn), ParHeader, ParState, ParEndOn).Compile();
#if VERBOSE
                }
                catch (Exception ex)
                {
                    throw new Exception("Type \"" + t.CleanTypename() + "\" can't be processed", ex);
                }
#endif//VERBOSE
        }

        /// <summary>
        /// Create the readers for a type.
        /// </summary>
        /// <param name="t">The type</param>
        /// <param name="v">The read expression (using <see cref="TempCall"/> parameters), converted to <paramref name="t"/> if needed</param>
        public ReadTypeCache(Type t, Expression v)
        {
#if VERBOSE
                try
                {
#endif//VERBOSE
                CreateNewBoxed = Expression.Lambda<Func<Object>>(Expression.Call(Helper.SafeGetMethod(typeof(ReadTyped<>).MakeGenericType(t), nameof(ReadTyped<int>.CreateNewBoxed), BindingFlags.Static | BindingFlags.Public))).Compile();
                CreateExp = v.Type == t ? v : Expression.Convert(v, t);
            SetStatic(t, v);
            if (v.Type != typeof(object))
                v = Expression.Convert(v, typeof(object));
            Create = Expression.Lambda<Creator>(v, ParState, ParEndOn).Compile();
            if (t.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null) != null)
                Cp = Expression.Lambda<CreateAndPopulateDel>(Expression.Call(MethodNewAndPopulate.MakeGenericMethod(t), ParHeader, ParState, ParEndOn), ParHeader, ParState, ParEndOn).Compile();
#if VERBOSE
                }
                catch (Exception ex)
                {
                    throw new Exception("Type \"" + t.CleanTypename() + "\" can't be processed", ex);
                }
#endif//VERBOSE
        }

        /// <summary>
        /// The read expression, of the type (for embedding in other readers).
        /// </summary>
        public readonly Expression CreateExp;
        /// <summary>
        /// The compiled reader, boxed.
        /// </summary>
        public readonly Creator Create;
        /// <summary>
        /// Create and populate an instance given the first member name, null if the type has no public parameterless constructor.
        /// Used when <c>"$type"</c> names a different type than the declared one.
        /// </summary>
        public readonly CreateAndPopulateDel Cp;

        static readonly Type JsonReaderType = typeof(JsonReader);
        static readonly MethodInfo EnumParse = Helper.SafeGetMethod(typeof(Enum), nameof(Enum.Parse), [typeof(Type), typeof(string)]);

        static readonly MethodInfo EnumParseT = Helper.SafeGetMethod(typeof(Enum), nameof(Enum.Parse), [typeof(string), typeof(bool)]);


        static readonly MethodInfo MethodCreateArray = Helper.SafeGetMethod(JsonReaderType, nameof(JsonReader.CreateArray), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo MethodCreateCollection = Helper.SafeGetMethod(JsonReaderType, nameof(JsonReader.CreateCollection), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo MethodCreateNullableObject = Helper.SafeGetMethod(JsonReaderType, nameof(JsonReader.CreateNullableObject), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo MethodCreateSealedNullableObject = Helper.SafeGetMethod(JsonReaderType, nameof(JsonReader.CreateSealedNullableObject), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo MethodCreateStruct = Helper.SafeGetMethod(JsonReaderType, nameof(JsonReader.CreateStruct), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo MethodNewAndPopulate = Helper.SafeGetMethod(JsonReaderType, nameof(JsonReader.NewAndPopulate), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo MethodIsNull = Helper.SafeGetMethod(JsonParserType, nameof(Utf8JsonParser.IsNullState), BindingFlags.Static | BindingFlags.Public);

        static readonly MethodInfo MethodReadUtf8MaybeQuoted = Helper.SafeGetMethod(JsonParserType, nameof(Utf8JsonParser.ReadUtf8MaybeQuoted), BindingFlags.Static | BindingFlags.Public);
        static readonly MethodInfo MethodReadEnum = Helper.SafeGetMethod(JsonParserType, nameof(Utf8JsonParser.ReadEnum), BindingFlags.Static | BindingFlags.Public);

        static readonly MethodInfo MethodToUtf8StringEscaped = Helper.SafeGetMethod(JsonParserType, nameof(Utf8JsonParser.ToUtf8String), BindingFlags.Static | BindingFlags.Public);


        /// <summary>
        /// Extra dictionary key parsers (from an unescaped UTF8 span expression), for key types not handled by <see cref="SpanParsers"/>.
        /// </summary>
        public static readonly IReadOnlyDictionary<Type, Func<Expression, Expression>> JsonSpanReaders = new Dictionary<Type, Func<Expression, Expression>>()
        {
            { typeof(String), e => Expression.Call(MethodToUtf8StringEscaped, ParState, e) },
        }.ToFrozenDictionary();


        /// <summary>
        /// Compares memory by content, the hash code is the length and the first byte (throws for empty memory).
        /// </summary>
        sealed class Cmp : IEqualityComparer<ReadOnlyMemory<Byte>>
        {
            public bool Equals(ReadOnlyMemory<Byte> x, ReadOnlyMemory<Byte> y) => x.Span.SequenceEqual(y.Span);

            public int GetHashCode([DisallowNull] ReadOnlyMemory<Byte> obj)
            {
                HashCode h = new();
                h.AddBytes(obj.Span);
                return h.ToHashCode();
            }
        }

        /// <summary>
        /// Resolved (and allowed) "$type" names, keyed by the raw UTF8 bytes, at most <see cref="TypeFinder.MaxCachedNames"/> entries (the same type can be named in many ways)
        /// </summary>
        static readonly LowAllocConcurrentDictionary<ReadOnlyMemory<Byte>, Type> TypenameCache = new (new Cmp());
        static readonly Expression ExpFalse = Expression.Constant(false);

        //static Assembly LastAsm;

        /// <summary>
        /// Resolve a <c>"$type"</c> type name using <see cref="TypeNameResolver.GetForData"/>, cached by the raw (still escaped) UTF8 bytes.
        /// </summary>
        /// <param name="state">The parser state</param>
        /// <param name="ptr">The start of the name (after the quote)</param>
        /// <param name="len">The length of the name in bytes (excluding the quotes)</param>
        /// <param name="isEscaped">True if the name contains escapes</param>
        /// <returns>The type</returns>
        /// <exception cref="Exception">No type with the name was found</exception>
        /// <exception cref="DataTypeNotAllowedException">The type isn't allowed by the <see cref="DataTypePolicy"/></exception>
        /// <remarks>Only types allowed by the <see cref="DataTypePolicy"/> can be resolved, the callers also checks that the type is assignable to the declared type.</remarks>
        public unsafe static Type ResolveType(JsonParserState state, Byte* ptr, int len, bool isEscaped)
        {
            var c = TypenameCache;
            var mem = state.Mem;
            mem.Set(ptr, len);
            if (c.TryGetValue(mem.Memory, out var t))
                return t;
            ref var buf = ref state.Temp;
            var newKey = new ReadOnlySpan<Byte>(ptr, len).ToArray();
            String typename = isEscaped ? Utf8Parser.ReadEscapedUtf8String(ref buf, ref ptr, ptr + len, (Char)0) : Utf8Parser.ReadUtf8String(ref buf, ref ptr, ptr + len , (Char)0);
            t = TypeNameResolver.GetForData(typename);
            if (t != null)
            {
                if (c.Count < TypeFinder.MaxCachedNames)
                    c.TryAdd(newKey, t);
                return t;
            }
            throw new Exception("Can't find a type named \"" + typename + "\"");
        }

        static ReadTypeCache()
        {
            var c = Cache;
            //  Add types above
            Type[] requiresQuote =
            [
                typeof(TimeSpan), typeof(Guid), typeof(DateTime), typeof(DateOnly), typeof(TimeOnly), typeof(DateTimeOffset),
            ];
            var maybeQuoted = Expression.Call(Helper.SafeGetMethod(JsonParserType, nameof(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted), BindingFlags.Static | BindingFlags.Public), TempCall);
            var quoted = Expression.Call(Helper.SafeGetMethod(JsonParserType, nameof(Utf8JsonParser.ReadAsciiReadOnlyMemoryQuoted), BindingFlags.Static | BindingFlags.Public), TempCall);
            foreach (var t in SpanParsers.SupportedTypes)
            {
                var prog = SpanParsers.GetExpression(t, requiresQuote.Contains(t) ? quoted : maybeQuoted);
                c.TryAdd(t, new ReadTypeCache(t, prog));
            }
            //  Add special types
            c.TryAdd(typeof(string), new ReadTypeCache(typeof(string), v => v));
            var defaultPropertyAttribute = typeof(string).GetCustomAttributes(typeof(DefaultMemberAttribute), false).First() as DefaultMemberAttribute;
            var propInfo = typeof(string).GetProperty(defaultPropertyAttribute.MemberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            c.TryAdd(typeof(char), new ReadTypeCache(typeof(char), v => Expression.Property(v, propInfo, Expression.Constant(0))));
            c.TryAdd(typeof(byte[]), new ReadTypeCache(typeof(byte[]), Expression.Call(Helper.SafeGetMethod(JsonReaderType, nameof(JsonReader.CreateByteArray), BindingFlags.Static | BindingFlags.NonPublic), TempCall)));
            c.TryAdd(typeof(object), new ReadTypeCache(typeof(object), Expression.Call(Helper.SafeGetMethod(JsonReaderType, nameof(JsonReader.CreateBoxedObject), BindingFlags.Static | BindingFlags.NonPublic), TempCall)));
        }

        /// <summary>
        /// The element type of a generic collection: the type argument, or a <see cref="KeyValuePair{TKey, TValue}"/> of two type arguments.
        /// </summary>
        /// <exception cref="Exception">The type has more than two type arguments</exception>
        static Type GetCollectionElementType(Type t)
        {
            var at = t.GetGenericArguments();
            if (at.Length == 1)
                return at[0];
            if (at.Length == 2)
                return typeof(KeyValuePair<,>).MakeGenericType(at);
            throw new Exception("Unknown collection type");
        }


    }

}
