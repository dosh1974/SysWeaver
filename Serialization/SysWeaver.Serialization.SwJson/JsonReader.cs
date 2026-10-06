using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SysWeaver.Serialization.SwJson.Reader;

namespace SysWeaver.Serialization.SwJson
{


    /// <summary>
    /// Deserializes UTF8 (or string) json into typed objects, the read side of the SysWeaver json format (see <see cref="JsonWriter"/>).
    /// </summary>
    /// <remarks>
    /// Parsing is done directly on the UTF8 bytes using compiled expression trees (built and cached per type on first use, so the first call for a type is slow).
    /// All methods are thread safe.
    /// <para>Supported: public read/write properties and public non-readonly fields, arrays, <see cref="List{T}"/>, <see cref="HashSet{T}"/> and other <see cref="ICollection{T}"/> types with a public parameterless (or <see cref="List{T}"/>) constructor,
    /// generic dictionaries (keys of any number/date/<see cref="Guid"/>/<see cref="String"/> type), enums (names or numbers), nullable value types, <see cref="Byte"/> arrays (base64 or number arrays) and boxed <see cref="Object"/> values.</para>
    /// <para>Polymorphism: an object starting with a <c>"$type"</c> member is created as that type (resolved with <see cref="TypeNameResolver"/>), <c>"$value"</c> / <c>"$values"</c> hold the boxed value or array of such an object.</para>
    /// <para>The parser is lenient: unquoted keys, numbers / booleans in quotes, <c>//</c> and <c>/* */</c> comments and trailing commas are accepted.
    /// Unknown members are skipped, but only when the value is a string or a scalar (unknown object or array values throw).
    /// Numbers are parsed with the invariant culture. Integers are not range checked (out of range values wrap) and in release builds digits are not validated.</para>
    /// </remarks>
    [SkipLocalsInit]
    public unsafe static class JsonReader
    {
        /// <summary>
        /// Create an object from UTF8 encoded json.
        /// </summary>
        /// <typeparam name="T">The type of the object to create (the declared type, a <c>"$type"</c> member in the json can create a derived type)</typeparam>
        /// <param name="data">UTF8 encoded json (without a byte order mark)</param>
        /// <param name="filename">Not used (reserved for including a filename in exception messages)</param>
        /// <returns>The object represented by the json, the default of <typeparamref name="T"/> if the data is empty, white space only or <c>null</c></returns>
        /// <exception cref="Exception">The json is invalid or can't be converted to <typeparamref name="T"/>, the message contains the (row, column) and an excerpt of the json around the error position, the inner exception is the actual error</exception>
        public static T Create<T>(ReadOnlySpan<Byte> data, String filename = null)
        {
            fixed (Byte* ptr = data)
            {
                using var state = JsonParserState.Get(ptr, data.Length);
                try
                {
                    return InternalCreate<T>(state);
                }
                catch (Exception ex)
                {
                    throw new Exception(GetThrowDetails(state, filename), ex);
                }
            }
        }

        /// <summary>
        /// Create an object from a json string.
        /// </summary>
        /// <remarks>The string is transcoded to UTF8 first (on the stack for short strings, else in a pooled buffer).</remarks>
        /// <typeparam name="T">The type of the object to create (the declared type, a <c>"$type"</c> member in the json can create a derived type)</typeparam>
        /// <param name="s">The json text, must not be null</param>
        /// <param name="filename">Not used (reserved for including a filename in exception messages)</param>
        /// <returns>The object represented by the json, the default of <typeparamref name="T"/> if the string is empty, white space only or <c>null</c></returns>
        /// <exception cref="Exception">The json is invalid or can't be converted to <typeparamref name="T"/>, the message contains the (row, column) and an excerpt of the json around the error position, the inner exception is the actual error</exception>
        public static T Create<T>(String s, String filename = null)
        {
            var utf8 = Utf8Parser.UTF8;
            var size = utf8.GetMaxByteCount(s.Length);
            Byte[] d = null;
            var data = size <= 4096 ? stackalloc Byte[size] : (d = ArrayPoolStream.Rent(size)).AsSpan();
            var l = utf8.GetBytes(s, data);
            fixed (Byte* ptr = data)
            {
                using var state = JsonParserState.Get(ptr, l);
                try
                {
                    return InternalCreate<T>(state);
                }
                catch (Exception ex)
                {
                    throw new Exception(GetThrowDetails(state, filename), ex);
                }
                finally
                {
                    if (d != null)
                        ArrayPoolStream.Return(d);
                }
            }
        }

        /// <summary>
        /// Create an object of a runtime type from UTF8 encoded json.
        /// </summary>
        /// <param name="type">The type of object to create (the declared type, a <c>"$type"</c> member in the json can create a derived type)</param>
        /// <param name="data">UTF8 encoded json (without a byte order mark)</param>
        /// <param name="filename">Not used (reserved for including a filename in exception messages)</param>
        /// <returns>The (boxed) object represented by the json, null if the data is empty or white space only</returns>
        /// <exception cref="Exception">The json is invalid or can't be converted to <paramref name="type"/>, the message contains the (row, column) and an excerpt of the json around the error position, the inner exception is the actual error</exception>
        public static Object Create(Type type, ReadOnlySpan<Byte> data, String filename = null)
        {
            fixed (Byte* ptr = data)
            {
                using var state = JsonParserState.Get(ptr, data.Length);
                try
                {
                    return InternalCreate(type, state);
                }
                catch (Exception ex)
                {
                    throw new Exception(GetThrowDetails(state, filename), ex);
                }
            }
        }

        /// <summary>
        /// Create an object of a runtime type from a json string.
        /// </summary>
        /// <remarks>The string is transcoded to UTF8 first (on the stack for short strings, else in a pooled buffer).</remarks>
        /// <param name="type">The type of object to create (the declared type, a <c>"$type"</c> member in the json can create a derived type)</param>
        /// <param name="s">The json text, must not be null</param>
        /// <param name="filename">Not used (reserved for including a filename in exception messages)</param>
        /// <returns>The (boxed) object represented by the json, null if the string is empty or white space only</returns>
        /// <exception cref="Exception">The json is invalid or can't be converted to <paramref name="type"/>, the message contains the (row, column) and an excerpt of the json around the error position, the inner exception is the actual error</exception>
        public static Object Create(Type type, String s, String filename = null)
        {
            var utf8 = Utf8Parser.UTF8;
            var size = utf8.GetMaxByteCount(s.Length);
            Byte[] d = null;
            var data = size <= 4096 ? stackalloc Byte[size] : (d = ArrayPoolStream.Rent(size)).AsSpan();
            var l = utf8.GetBytes(s, data);
            fixed (Byte* ptr = data)
            {
                using var state = JsonParserState.Get(ptr, l);
                try
                {
                    return InternalCreate(type, state);
                }
                catch (Exception ex)
                {
                    throw new Exception(GetThrowDetails(state, filename), ex);
                }
                finally
                {
                    if (d != null)
                        ArrayPoolStream.Return(d);
                }
            }

        }

        #region Arrays

        /// <summary>
        /// Read a <see cref="Byte"/> array, either a base64 string or a json array of numbers (or null).
        /// </summary>
        /// <param name="state">The parser state, positioned at the value</param>
        /// <param name="endOn">The end condition of the enclosing container</param>
        /// <returns>The bytes read</returns>
        internal static Byte[] CreateByteArray(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            var c = (Char)(*d);
            if (c == '"')
            {
                ++d;
                return Utf8Parser.ReadBase64Bytes(ref d, e, c);
            }
            return CreateArray<Byte>(state, endOn);
        }

        /// <summary>
        /// Read an array/collection that was written as an object (after the '{'): <c>{"$type":"name","$values":[..]}</c> (or <c>"$value"</c>).
        /// </summary>
        /// <typeparam name="T">The declared array/collection type</typeparam>
        /// <param name="state">The parser state, positioned after the '{'</param>
        /// <param name="endOn">The end condition of the enclosing container</param>
        /// <returns>The value of the type named by <c>"$type"</c></returns>
        /// <exception cref="Exception">The object doesn't start with <c>"$type"</c>, or isn't followed by <c>"$value"</c> / <c>"$values"</c></exception>
        internal static T ReadArrayLikeObject<T>(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;

            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedEndOfObject();
            ReadOnlySpan<Byte> spanVal = default;
            if (!Utf8JsonParser.ReadKey(state, ref spanVal, ref d, e, Utf8JsonParser.EndOnColon))
                return ReturnEmpty<T>(ref d);
            if (!TypeKey.Equals(spanVal))
                ReadException.ThrowExpectedArrayFoundObject();
            if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != ':'))
                ReadException.ThrowExpectedKeyValueSeparator();
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedTypename();
            var newType = Utf8JsonParser.ReadAndResolveType(ref d, e, state);
            var t = typeof(T);
            //  Always checked before anything is created (the type must be compatible with the declared type)
            if (!t.IsAssignableFrom(newType))
                throw new DataTypeNotAllowedException(newType.FullName, newType, t);
            bool isNew = t != newType;
            t = newType;
            if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != ','))
                ReadException.ThrowExpectedValueSeparator();
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedValue();
            if (!Utf8JsonParser.ReadKey(state, ref spanVal, ref d, e, Utf8JsonParser.EndOnColon))
                ReadException.ThrowExpectedBoxedValue();
            if (!(ValueKey.Equals(spanVal) || ValuesKey.Equals(spanVal)))
                ReadException.ThrowExpectedBoxedValue();
            if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != ':'))
                ReadException.ThrowExpectedKeyValueSeparator();
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedValue();
            var v = (T)ReadTypeCache.Get(t).Create(state, Utf8JsonParser.EndOnObject);
            if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != '}'))
                ReadException.ThrowExpectedEndOfObject();
            return v;
        }

        /// <summary>
        /// Read an array (or null, or a <c>"$type"</c> wrapped array object).
        /// </summary>
        /// <remarks>Items are read into a buffer rented from <see cref="ArrayPool{T}.Shared"/>, only the final exact sized array is allocated.</remarks>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="state">The parser state, positioned at the value</param>
        /// <param name="endOn">The end condition of the enclosing container</param>
        /// <returns>The array read, null for a json null</returns>
        internal static T[] CreateArray<T>(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            if (Utf8JsonParser.IsNull(ref d, e))
                return null;
            var c = Utf8Parser.ReadAsciiChar(ref d, e);
            if (c != '[')
            {
                if (c != '{')
                    ReadException.ThrowArrayOpener();
                return ReadArrayLikeObject<T[]>(state, endOn);
            }
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedArray();
            //  The items are read into a pooled buffer, only the final array is allocated
            var pool = ArrayPool<T>.Shared;
            var buf = pool.Rent(16);
            try
            {
                var count = ReadItems(state, ref buf);
                if (count <= 0)
                    return [];
                var a = GC.AllocateUninitializedArray<T>(count);
                buf.AsSpan(0, count).CopyTo(a);
                return a;
            }
            finally
            {
                pool.Return(buf, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            }
        }

        /// <summary>
        /// Read the items of an array (after the '[' and any white space), into a pooled buffer that is replaced by a larger one as needed.
        /// The position is set to after the closing ']' (a trailing comma before the ']' is accepted).
        /// </summary>
        /// <param name="state">The parser state</param>
        /// <param name="buf">A buffer rented from <see cref="ArrayPool{T}.Shared"/>, can be replaced (the old one is returned to the pool)</param>
        /// <returns>The number of items read</returns>
        static int ReadItems<T>(JsonParserState state, ref T[] buf)
        {
            ref var d = ref state.D;
            var e = state.E;
            var createTyped = ReadTyped<T>.Create;
            int count = 0;
            for (; ; )
            {
                if ((Char)(*d) == ']')
                {
                    ++d;
                    break;
                }
                if (count == buf.Length)
                    buf = GrowRented(buf, count);
                buf[count] = ReadItem(state, createTyped);
                ++count;
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfArray();
                var c = Utf8Parser.ReadAsciiChar(ref d, e);
                if (c == ']')
                    break;
                if (c != ',')
                    ReadException.ThrowExpectedValueSeparator();
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfArray();
            }
            return count;
        }

        static readonly bool Is64BitProcess = Environment.Is64BitProcess;

        /// <summary>
        /// Read an array item, common primitives are parsed directly (the same code as the generated creator, without the delegate call)
        /// </summary>
        /// <param name="state">The parser state, positioned at the item</param>
        /// <param name="create">The compiled creator for <typeparamref name="T"/></param>
        /// <returns>The item read</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static T ReadItem<T>(JsonParserState state, ReadTyped<T>.TypedCreator create)
        {
            var endOn = Utf8JsonParser.EndOnArray;
            if (typeof(T) == typeof(Int32))
            {
                if (Is64BitProcess)
                    return (T)(Object)(Int32)SpanParsers.ToInt64(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn));
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (T)(Object)SpanParsers.ToInt64(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn));
            }
            else if (typeof(T) == typeof(Double))
            {
                return (T)(Object)SpanParsers.ToDouble(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn));
            }
            else if (typeof(T) == typeof(Boolean))
            {
                return (T)(Object)SpanParsers.ToBoolean(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn));
            }
            else if (typeof(T) == typeof(String))
            {
                return (T)(Object)Utf8JsonParser.ReadQuotedString(state);
            }
            return create(state, endOn);
        }

        /// <summary>
        /// Rent a buffer twice the size, copy the first <paramref name="count"/> items and return the old buffer to the pool.
        /// </summary>
        static T[] GrowRented<T>(T[] buf, int count)
        {
            var pool = ArrayPool<T>.Shared;
            var nb = pool.Rent(count << 1);
            buf.AsSpan(0, count).CopyTo(nb);
            pool.Return(buf, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            return nb;
        }

        /// <summary>
        /// Read a collection (or null, or a <c>"$type"</c> wrapped array object).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <typeparam name="C">The collection type to create (see <see cref="CollectionFactory{T, C}"/>)</typeparam>
        /// <param name="state">The parser state, positioned at the value</param>
        /// <param name="endOn">The end condition of the enclosing container</param>
        /// <returns>The collection read, null for a json null</returns>
        internal static ICollection<T> CreateCollection<T, C>(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            if (Utf8JsonParser.IsNull(ref d, e))
                return null;
            var c = Utf8Parser.ReadAsciiChar(ref d, e);
            if (c != '[')
            {
                if (c != '{')
                    ReadException.ThrowArrayOpener();
                return ReadArrayLikeObject<ICollection<T>>(state, endOn);
            }
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedArray();
            //  The items are read into a pooled buffer, then the collection is created with the exact size
            var pool = ArrayPool<T>.Shared;
            var buf = pool.Rent(16);
            try
            {
                var count = ReadItems(state, ref buf);
                return CollectionFactory<T, C>.Create(buf, count);
            }
            finally
            {
                pool.Return(buf, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            }
        }

        /// <summary>
        /// Creates a collection of type <typeparamref name="C"/> from the items read (computed once per type).
        /// <see cref="List{T}"/> and <see cref="HashSet{T}"/> are created directly, other types with a compiled call to a public constructor taking a <see cref="List{T}"/>
        /// (or something it's assignable to), else with <see cref="Activator.CreateInstance(Type, object[])"/>.
        /// </summary>
        static class CollectionFactory<T, C>
        {
            public static readonly Func<T[], int, ICollection<T>> Create = GetCreate();

            static ICollection<T> CreateList(T[] items, int count)
            {
                var l = new List<T>(count);
                CollectionsMarshal.SetCount(l, count);
                items.AsSpan(0, count).CopyTo(CollectionsMarshal.AsSpan(l));
                return l;
            }

            static ICollection<T> CreateHashSet(T[] items, int count)
            {
                //  Same as new HashSet<T>(list) (that sizes the set to the number of items and adds them in order)
                var h = new HashSet<T>(count);
                for (int i = 0; i < count; ++i)
                    h.Add(items[i]);
                return h;
            }

            static Func<T[], int, ICollection<T>> GetCreate()
            {
                var ct = typeof(C);
                if (ct == typeof(List<T>))
                    return CreateList;
                if (ct == typeof(HashSet<T>))
                    return CreateHashSet;
                //  The constructor that Activator.CreateInstance(ct, List<T>) would use (compiled instead of reflection)
                var lt = typeof(List<T>);
                var ctor = ct.GetConstructor(BindingFlags.Instance | BindingFlags.Public, Type.DefaultBinder, [lt], null);
                if (ctor == null)
                    return (items, count) => (ICollection<T>)Activator.CreateInstance(ct, CreateList(items, count));
                var p = Expression.Parameter(lt, "l");
                var ctorParam = ctor.GetParameters()[0].ParameterType;
                var create = Expression.Lambda<Func<List<T>, ICollection<T>>>(Expression.Convert(Expression.New(ctor, ctorParam == lt ? p : Expression.Convert(p, ctorParam)), typeof(ICollection<T>)), p).Compile();
                return (items, count) => create((List<T>)CreateList(items, count));
            }
        }

        #endregion//Arrays

        #region Object

        /// <summary>
        /// Read a (non sealed) reference type object, or null. A <c>"$type"</c> member can create a derived type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T CreateNullableObject<T>(JsonParserState state, Func<Char, bool> endOn)
        {
            return Utf8JsonParser.IsNullState(state) ? default(T) : CreateObject<T>(state, endOn);
        }

        /// <summary>
        /// Read a sealed reference type object, or null (no <c>"$type"</c> handling, it would be read as an unknown member).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T CreateSealedNullableObject<T>(JsonParserState state, Func<Char, bool> endOn)
        {
            return Utf8JsonParser.IsNullState(state) ? default(T) : CreateSealedObject<T>(state, endOn);
        }

        /// <summary>
        /// Read a value declared as <see cref="Object"/>.
        /// Objects must have a <c>"$type"</c> member (else a plain new <see cref="Object"/> is returned and all members are skipped).
        /// For Newtonsoft compatibility: strings that <see cref="DateTime.TryParse(string, IFormatProvider, DateTimeStyles, out DateTime)"/> accepts (using the current culture) become a <see cref="DateTime"/>,
        /// <c>true</c>/<c>false</c> a <see cref="Boolean"/>, integral numbers an <see cref="Int64"/> and other numbers a <see cref="Double"/> (numbers are parsed as <see cref="Decimal"/>, so exponents are not supported).
        /// </summary>
        /// <param name="state">The parser state, positioned at the value</param>
        /// <param name="endOn">The end condition of the enclosing container</param>
        /// <returns>The value read, null for a json null</returns>
        internal static Object CreateBoxedObject(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            if (Utf8JsonParser.IsNull(ref d, e))
                return null;
            var c = (Char)(*d);
            if (c != '{')
            {
                //  Boxed hacks for Newtonsoft JSON compatibility
                if (c == '"')
                {
                    var v = Utf8JsonParser.ReadQuotedString(state);
                    if (DateTime.TryParse(v, null, DateTimeStyles.RoundtripKind, out var dts))
                        return dts;
                    return v;
                }
                var vv = Utf8Parser.ReadAsciiStringNoLast(ref d, e, Utf8JsonParser.EndOnObject);
                if (Boolean.TryParse(vv, out var br))
                    return br;
                var val = Decimal.Parse(vv, CultureInfo.InvariantCulture);
                if (Math.Round(val) == val)
                    return (Int64)val;
                return (Double)val;
            }
            return CreateObject<Object>(state, endOn);
        }

        /// <summary>
        /// Read a value type object (no null or <c>"$type"</c> handling).
        /// </summary>
        internal static T CreateStruct<T>(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            var t = typeof(T);
            var c = Utf8Parser.ReadAsciiChar(ref d, e);
            if (c != '{')
                ReadException.ThrowObjectOpener();
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedObject();
            ReadOnlySpan<Byte> header = default;
            if (!Utf8JsonParser.ReadKey(state, ref header, ref d, e, Utf8JsonParser.EndOnColon))
                return ReturnEmpty<T>(ref d);
            return NewAndPopulate<T>(header, state, endOn);
        }

        /// <summary>
        /// Read a sealed reference type object (not null, no <c>"$type"</c> handling).
        /// </summary>
        internal static T CreateSealedObject<T>(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            var c = Utf8Parser.ReadAsciiChar(ref d, e);
            var t = typeof(T);
            if (c != '{')
                ReadException.ThrowObjectOpener();
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedObject();
            ReadOnlySpan<Byte> header = default;
            if (!Utf8JsonParser.ReadKey(state, ref header, ref d, e, Utf8JsonParser.EndOnColon))
                return ReturnEmpty<T>(ref d);
            return NewAndPopulate<T>(header, state, endOn);
        }

        /// <summary>
        /// Read a reference type object (not null). If the first member is <c>"$type"</c> the named type is created instead,
        /// and a following <c>"$value"</c> / <c>"$values"</c> member is read as the complete value of that type.
        /// </summary>
        /// <remarks>
        /// The <c>"$type"</c> type must be allowed by the <see cref="DataTypePolicy"/> and assignable to <typeparamref name="T"/> (checked before anything is created).
        /// </remarks>
        internal static T CreateObject<T>(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            var c = Utf8Parser.ReadAsciiChar(ref d, e);
            var t = typeof(T);
            if (c != '{')
                ReadException.ThrowObjectOpener();
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedObject();
            ReadOnlySpan<Byte> spanVal = default;
            if (!Utf8JsonParser.ReadKey(state, ref spanVal, ref d, e, Utf8JsonParser.EndOnColon))
                return ReturnEmpty<T>(ref d);
            if (!TypeKey.Equals(spanVal))
                return NewAndPopulate<T>(spanVal, state, endOn);
            if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != ':'))
                ReadException.ThrowExpectedKeyValueSeparator();
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedTypename();
            var newType = Utf8JsonParser.ReadAndResolveType(ref d, e, state);
            //  Always checked before anything is created (the type must be compatible with the declared type)
            if (!t.IsAssignableFrom(newType))
                throw new DataTypeNotAllowedException(newType.FullName, newType, t);
            bool isNew = t != newType;
            t = newType;
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedValue();
            var next = Utf8Parser.ReadAsciiChar(ref d, e);
            if (next == '}')
                return (T)ReadTypeCache.Get(newType).CreateNewBoxed();
            if (next != ',')
                ReadException.ThrowExpectedValueSeparator();
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedValue();
            if (!Utf8JsonParser.ReadKey(state, ref spanVal, ref d, e, Utf8JsonParser.EndOnColon))
                return ReturnEmpty<T>(ref d);
            if (ValueKey.Equals(spanVal) || ValuesKey.Equals(spanVal))
            {
                if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != ':'))
                    ReadException.ThrowExpectedKeyValueSeparator();
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedValue();
                var v = (T)ReadTypeCache.Get(t).Create(state, Utf8JsonParser.EndOnObject);
                if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != '}'))
                    ReadException.ThrowExpectedEndOfObject();
                return v;
            }
            return isNew ? (T)ReadTypeCache.Get(t).Cp(spanVal, state, endOn) : NewAndPopulate<T>(spanVal, state, endOn);
        }

        /// <summary>
        /// Create a dictionary and add all key-value pairs, starting with the already read first <paramref name="key"/>.
        /// The position is set to after the closing '}'.
        /// </summary>
        /// <remarks>Uses the dictionary's Add method, so a duplicated key throws.</remarks>
        internal static T NewAndPopulateDictionary<T>(ReadOnlySpan<Byte> key, JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            var add = ReadTyped<T>.GetDictionary(out var v);
            for (; ; )
            {
                if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != ':'))
                    ReadException.ThrowExpectedKeyValueSeparator();
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedValue();
                add(v, key, state, Utf8JsonParser.EndOnObject);
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfObject();
                var c = Utf8Parser.ReadAsciiChar(ref d, e);
                if (c == '}')
                    break;
                if (c != ',')
                    ReadException.ThrowExpectedValueSeparator();
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfObject();
                if (!Utf8JsonParser.ReadKey(state, ref key, ref d, e, Utf8JsonParser.EndOnColon))
                {
                    ++d;
                    break;
                }
            }
            return v;
        }

        /// <summary>
        /// Create an object (or dictionary) and populate it, starting with the already read first <paramref name="key"/> (the position is after the key).
        /// Unknown members are skipped. The position is set to after the closing '}'.
        /// </summary>
        /// <typeparam name="T">The type to create</typeparam>
        /// <param name="key">The UTF8 name of the first member (unescaped)</param>
        /// <param name="state">The parser state</param>
        /// <param name="endOn">The end condition of the enclosing container</param>
        /// <returns>The populated object</returns>
        internal static T NewAndPopulate<T>(ReadOnlySpan<Byte> key, JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            if (DictionaryCheck<T>.IsDictionary)
                return NewAndPopulateDictionary<T>(key, state, endOn);
            var members = ReadTyped<T>.GetMembers(out var v);
            for (; ; )
            {
                if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != ':'))
                    ReadException.ThrowExpectedKeyValueSeparator();
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedValue();
                if (members.TryGetValue(state, key, out var m))
                    m(ref v, state, Utf8JsonParser.EndOnObject);
                else
                    Utf8JsonParser.SkipUnknown(ref d, e, Utf8JsonParser.EndOnObject);
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfObject();
                var c = Utf8Parser.ReadAsciiChar(ref d, e);
                if (c == '}')
                    break;
                if (c != ',')
                    ReadException.ThrowExpectedValueSeparator();
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfObject();
                if (!Utf8JsonParser.ReadKey(state, ref key, ref d, e, Utf8JsonParser.EndOnColon))
                {
                    ++d;
                    break;
                }
            }
            return v;
        }

        #endregion // Object

        /// <summary>
        /// True if the type is a generic dictionary (computed once per type)
        /// </summary>
        static class DictionaryCheck<T>
        {
            public static readonly bool IsDictionary = Get();

            static bool Get()
            {
                var t = typeof(T);
                if (!t.IsGenericType)
                    return false;
                var args = t.GetGenericArguments();
                if (args.Length != 2)
                    return false;
                return typeof(IDictionary<,>).MakeGenericType(args).IsAssignableFrom(t);
            }
        }

        static readonly Utf8Range TypeKey = Utf8Range.Create("$type");
        static readonly Utf8Range ValueKey = Utf8Range.Create("$value");
        static readonly Utf8Range ValuesKey = Utf8Range.Create("$values");

        /// <summary>
        /// Skip the '}' of an empty object and return a new (empty) instance.
        /// </summary>
        /// <remarks>Uses <see cref="ReadTyped{T}.GetMembers(out T)"/>, so the member look up is built even for dictionaries (indexers are excluded from the look up).</remarks>
        static T ReturnEmpty<T>(ref Byte* d)
        {
            ++d;
            ReadTyped<T>.GetMembers(out var v);
            return v;
        }

        /// <summary>
        /// Read the root value of a runtime type (null if there is no data).
        /// </summary>
        static Object InternalCreate(Type t, JsonParserState state)
        {
            ref var d = ref state.D;
            var e = state.E;
#if VERBOSE
            try
            {
#endif//VERBOSE
                if (Utf8Parser.SkipWhite(ref d, e))
                    return null;
                return ReadTypeCache.Get(t).Create(state, Utf8JsonParser.EndOnObject);
#if VERBOSE
            }
            catch (Exception ex)
            {
                throw new Exception("for type \"" + t.CleanTypename() + "\"", ex);
            }
#endif//VERBOSE
        }

        /// <summary>
        /// Read the root value (default if there is no data).
        /// </summary>
        static T InternalCreate<T>(JsonParserState state)
        {
            ref var d = ref state.D;
            var e = state.E;
#if VERBOSE
            try
            {
#endif//VERBOSE
                if (Utf8Parser.SkipWhite(ref d, e))
                    return default(T);
                var create = ReadTyped<T>.Create;
                if (create == null)
                {
                    ReadTypeCache.Get(typeof(T));
                    create = ReadTyped<T>.Create;
                }
                return create(state, Utf8JsonParser.EndOnObject);
#if VERBOSE
            }
            catch (Exception ex)
            {
                throw new Exception("for type \"" + typeof(T).CleanTypename() + "\"", ex);
            }
#endif//VERBOSE
        }

        #region Exception details

        /// <summary>
        /// Create a description of the current position: "(row,col) : Near ==&gt;text before^text after&lt;==".
        /// </summary>
        /// <remarks>Rows are counted on CR characters only. The <paramref name="filename"/> is not used.
        /// The byte offset is used as a char index into the decoded text, so the position is off (and this can throw) when the text before the error contains non ASCII chars.</remarks>
        static String GetThrowDetails(JsonParserState state, String filename)
        {
            var start = state.S;
            var s = Utf8Parser.UTF8.GetString(start, (int)(state.E - start));
            var o = (int)(state.D - start);
            int row = 1;
            int col = o + 1;
            int p = o;
            while (p > 0)
            {
                --p;
                if (s[p] == 13)
                {
                    if (col > o)
                        col = (o - p) + 1;
                    ++row;
                }
            }
            var sp = Math.Max(0, o - 24);
            var ep = Math.Min(s.Length, o + 8);
            var loc = "(" + row + "," + col + ") : Near ==>" + Filter(s.Substring(sp, o - sp)) + "^" + Filter(s.Substring(o, ep - o)) + "<==";
            return loc;
        }

        /// <summary>
        /// Replace all control chars (below 32) with a space.
        /// </summary>
        static String Filter(String s)
        {
            var l = s.Length;

            Char[] rented = null;
            var sb = l <= 4096 ? stackalloc Char[l] : (rented = ArrayPool<Char>.Shared.Rent(l)).AsSpan();
            try
            {
                for (int i = 0; i < l; ++i)
                {
                    var c = s[i];
                    if (c < 32)
                        c = (Char)32;
                    sb[i] = c;
                }
                return new String(sb.Slice(0, l));
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

        #endregion//Exception details

    }

}
