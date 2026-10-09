using System;
using System.Buffers;
using System.Collections.Concurrent;
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
    /// <para>Supported: public read/write properties and public non-readonly fields, arrays, <see cref="List{T}"/>, <see cref="HashSet{T}"/> and other <see cref="ICollection{T}"/> types with a public parameterless (or <see cref="List{T}"/>) constructor
    /// (a <see cref="List{T}"/> or <see cref="HashSet{T}"/> is created for interfaces like <see cref="IList{T}"/> and <see cref="ISet{T}"/>),
    /// other <see cref="IEnumerable{T}"/> types without serialized members (like <see cref="Queue{T}"/>, <see cref="Stack{T}"/>, <see cref="ConcurrentQueue{T}"/> and <see cref="ConcurrentBag{T}"/>,
    /// a json array read as an <see cref="IEnumerable{T}"/>, <see cref="IReadOnlyCollection{T}"/> or <see cref="IReadOnlyList{T}"/> creates a <see cref="List{T}"/>),
    /// generic dictionaries (keys of any number/date/<see cref="Guid"/>/<see cref="String"/>/enum type), enums (names or numbers), nullable value types, <see cref="Byte"/> arrays (base64 or number arrays) and boxed <see cref="Object"/> values.</para>
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
            //  The items are read into a stack or pooled buffer, only the final array is allocated
            Unsafe.SkipInit(out InlineItems<T> inline);
            var items = InlineItems<T>.Use ? inline : default(Span<T>);
            T[] rented = null;
            int count = -1;
            try
            {
                count = ReadItems(state, ref items, ref rented);
                if (count <= 0)
                    return [];
                var a = GC.AllocateUninitializedArray<T>(count);
                items.Slice(0, count).CopyTo(a);
                return a;
            }
            finally
            {
                if (rented != null)
                    ReturnRented(rented, count);
            }
        }

        /// <summary>
        /// Room for the first items of an array on the stack (for item types up to 8 bytes, so at most 128 bytes per nesting level)
        /// </summary>
        [InlineArray(Length)]
        struct InlineItems<T>
        {
            public const int Length = 16;

            /// <summary>
            /// True if the item type is small enough to use the stack buffer
            /// </summary>
            public static bool Use
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Unsafe.SizeOf<T>() <= 8;
            }

            T E;
        }

        /// <summary>
        /// Return a buffer rented by <see cref="ReadItems{T}(JsonParserState, ref Span{T}, ref T[])"/>, references in the used part are cleared
        /// </summary>
        /// <param name="rented">The buffer</param>
        /// <param name="count">The number of items in the buffer, negative if unknown (an exception was thrown, the whole buffer is cleared)</param>
        static void ReturnRented<T>(T[] rented, int count)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                if (count >= 0)
                    rented.AsSpan(0, count).Clear();
                else
                    Array.Clear(rented);
            }
            ArrayPool<T>.Shared.Return(rented);
        }

        /// <summary>
        /// Read the items of an array (after the '[' and any white space) into a buffer that is replaced by a larger one (rented from <see cref="ArrayPool{T}.Shared"/>) as needed.
        /// The position is set to after the closing ']' (a trailing comma before the ']' is accepted).
        /// </summary>
        /// <param name="state">The parser state</param>
        /// <param name="items">The buffer, can be empty (a buffer is rented for the first item)</param>
        /// <param name="rented">The rented buffer (that <paramref name="items"/> is), the caller must return it to the pool (see <see cref="ReturnRented{T}(T[], int)"/>), also on an exception</param>
        /// <returns>The number of items read</returns>
        static int ReadItems<T>(JsonParserState state, ref Span<T> items, ref T[] rented)
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
                if (count == items.Length)
                    items = GrowItems(items, ref rented, count);
                items[count] = ReadItem(state, createTyped);
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
                    return (T)(Object)checked((Int32)FusedParsers.ReadInt64(state, endOn));
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (T)(Object)FusedParsers.ReadInt64(state, endOn);
            }
            else if (typeof(T) == typeof(Double))
            {
                return (T)(Object)FusedParsers.ReadDouble(state, endOn);
            }
            else if (typeof(T) == typeof(Boolean))
            {
                return (T)(Object)FusedParsers.ReadBoolean(state, endOn);
            }
            else if (typeof(T) == typeof(String))
            {
                return (T)(Object)Utf8JsonParser.ReadQuotedString(state);
            }
            return create(state, endOn);
        }

        /// <summary>
        /// Rent a buffer twice the size (at least 16 items), copy the first <paramref name="count"/> items and return the old rented buffer (if any) to the pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static T[] GrowItems<T>(Span<T> items, ref T[] rented, int count)
        {
            var nb = ArrayPool<T>.Shared.Rent(Math.Max(InlineItems<T>.Length, count << 1));
            items.Slice(0, count).CopyTo(nb);
            var old = rented;
            rented = nb;
            if (old != null)
                ReturnRented(old, count);
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
            //  The items are read into a stack or pooled buffer, then the collection is created with the exact size
            Unsafe.SkipInit(out InlineItems<T> inline);
            var items = InlineItems<T>.Use ? inline : default(Span<T>);
            T[] rented = null;
            int count = -1;
            try
            {
                count = ReadItems(state, ref items, ref rented);
                return CollectionFactory<T, C>.Create(items.Slice(0, count));
            }
            finally
            {
                if (rented != null)
                    ReturnRented(rented, count);
            }
        }

        /// <summary>
        /// Creates a collection of type <typeparamref name="C"/> from the items read (computed once per type).
        /// <see cref="List{T}"/> and <see cref="HashSet{T}"/> are created directly, other types with a compiled call to a public constructor taking a <see cref="List{T}"/>
        /// (or something it's assignable to), else with <see cref="Activator.CreateInstance(Type, object[])"/>.
        /// </summary>
        static class CollectionFactory<T, C>
        {
            public static readonly ItemsCreator<T, ICollection<T>> Create = GetCreate();

            static ICollection<T> CreateList(ReadOnlySpan<T> items) => ItemsToList(items);

            static ICollection<T> CreateHashSet(ReadOnlySpan<T> items)
            {
                //  Same as new HashSet<T>(list) (that sizes the set to the number of items and adds them in order)
                var h = new HashSet<T>(items.Length);
                foreach (var x in items)
                    h.Add(x);
                return h;
            }

            static ItemsCreator<T, ICollection<T>> GetCreate()
            {
                var ct = typeof(C);
                if (ct == typeof(List<T>))
                    return CreateList;
                if (ct == typeof(HashSet<T>))
                    return CreateHashSet;
                //  Interfaces (like ICollection<T>, IList<T> or ISet<T>) can't be created, use a List<T> or HashSet<T>
                if (ct.IsInterface)
                {
                    if (ct.IsAssignableFrom(typeof(List<T>)))
                        return CreateList;
                    if (ct.IsAssignableFrom(typeof(HashSet<T>)))
                        return CreateHashSet;
                }
                //  The constructor that Activator.CreateInstance(ct, List<T>) would use (compiled instead of reflection)
                var lt = typeof(List<T>);
                var ctor = ct.GetConstructor(BindingFlags.Instance | BindingFlags.Public, Type.DefaultBinder, [lt], null);
                if (ctor == null)
                    return items => (ICollection<T>)Activator.CreateInstance(ct, ItemsToList(items));
                var p = Expression.Parameter(lt, "l");
                var ctorParam = ctor.GetParameters()[0].ParameterType;
                var create = Expression.Lambda<Func<List<T>, ICollection<T>>>(Expression.Convert(Expression.New(ctor, ctorParam == lt ? p : Expression.Convert(p, ctorParam)), typeof(ICollection<T>)), p).Compile();
                return items => create(ItemsToList(items));
            }
        }

        /// <summary>
        /// Read an <see cref="IEnumerable{T}"/> that isn't a collection (or null), see <see cref="ReadTypeCache"/>.
        /// A json array is read into the type created by <see cref="EnumerableFactory{T, C}"/> (a <see cref="List{T}"/> for interfaces like <see cref="IEnumerable{T}"/>),
        /// an object is read like any other object (a <c>"$type"</c> member can name the type, followed by <c>"$values"</c> or members).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <typeparam name="C">The (declared) type to create</typeparam>
        /// <param name="state">The parser state, positioned at the value</param>
        /// <param name="endOn">The end condition of the enclosing container</param>
        /// <returns>The value read, null for a json null</returns>
        internal static C CreateEnumerable<T, C>(JsonParserState state, Func<Char, bool> endOn) where C : class
        {
            ref var d = ref state.D;
            var e = state.E;
            if (Utf8JsonParser.IsNull(ref d, e))
                return null;
            if ((Char)(*d) != '[')
                return CreateObject<C>(state, endOn);
            ++d;
            if (Utf8Parser.SkipWhite(ref d, e))
                ReadException.ThrowExpectedArray();
            //  The items are read into a stack or pooled buffer, then the value is created
            Unsafe.SkipInit(out InlineItems<T> inline);
            var items = InlineItems<T>.Use ? inline : default(Span<T>);
            T[] rented = null;
            int count = -1;
            try
            {
                count = ReadItems(state, ref items, ref rented);
                return EnumerableFactory<T, C>.Create(items.Slice(0, count));
            }
            finally
            {
                if (rented != null)
                    ReturnRented(rented, count);
            }
        }

        /// <summary>
        /// Creates an <see cref="IEnumerable{T}"/> of type <typeparamref name="C"/> from the items read (computed once per type).
        /// If a <see cref="List{T}"/> is assignable to <typeparamref name="C"/> (like <see cref="IEnumerable{T}"/>, <see cref="IReadOnlyCollection{T}"/> and <see cref="IReadOnlyList{T}"/>) a <see cref="List{T}"/> is created,
        /// else a public constructor taking a <see cref="List{T}"/> (or something it's assignable to, like <see cref="IEnumerable{T}"/>) is used,
        /// like for <see cref="Queue{T}"/>, <see cref="ConcurrentQueue{T}"/> and <see cref="ConcurrentBag{T}"/>.
        /// The items of a <see cref="Stack{T}"/> and <see cref="ConcurrentStack{T}"/> are pushed in reverse order (they are written in enumeration order, top first).
        /// Other types throw a <see cref="NotSupportedException"/> when read from a json array.
        /// </summary>
        static class EnumerableFactory<T, C> where C : class
        {
            public static readonly ItemsCreator<T, C> Create = GetCreate();

            static ItemsCreator<T, C> GetCreate()
            {
                var ct = typeof(C);
                var lt = typeof(List<T>);
                if (ct.IsAssignableFrom(lt))
                    return items => (C)(Object)ItemsToList(items);
                var ctor = ct.IsAbstract ? null : ct.GetConstructor(BindingFlags.Instance | BindingFlags.Public, Type.DefaultBinder, [lt], null);
                if (ctor == null)
                    return items => throw new NotSupportedException("Can't create a \"" + ct.CleanTypename() + "\" from a json array, a public constructor taking an IEnumerable<" + typeof(T).CleanTypename() + "> is required");
                var p = Expression.Parameter(lt, "l");
                var ctorParam = ctor.GetParameters()[0].ParameterType;
                var create = Expression.Lambda<Func<List<T>, C>>(Expression.New(ctor, ctorParam == lt ? p : Expression.Convert(p, ctorParam)), p).Compile();
                var reverse = ct.IsGenericType && ((ct.GetGenericTypeDefinition() == typeof(Stack<>)) || (ct.GetGenericTypeDefinition() == typeof(ConcurrentStack<>)));
                if (reverse)
                    return items =>
                    {
                        var l = ItemsToList(items);
                        l.Reverse();
                        return create(l);
                    };
                return items => create(ItemsToList(items));
            }
        }

        /// <summary>
        /// Create a collection (or another value) from the items read
        /// </summary>
        delegate R ItemsCreator<T, R>(ReadOnlySpan<T> items);

        /// <summary>
        /// Create a <see cref="List{T}"/> with the items (the capacity is the number of items)
        /// </summary>
        static List<T> ItemsToList<T>(ReadOnlySpan<T> items)
        {
            var count = items.Length;
            var l = new List<T>(count);
            CollectionsMarshal.SetCount(l, count);
            items.CopyTo(CollectionsMarshal.AsSpan(l));
            return l;
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
        /// Boxed values are written without type information, so: strings that are exactly what the <see cref="JsonWriter"/> writes for a <see cref="DateOnly"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/> or <see cref="TimeSpan"/>
        /// become that type (so that json => object => json gives the same json, a <see cref="TimeOnly"/> becomes a <see cref="TimeSpan"/> and a local <see cref="DateTime"/> a <see cref="DateTimeOffset"/>, other strings stay strings),
        /// <c>true</c>/<c>false</c> a <see cref="Boolean"/>, integral numbers (in the <see cref="Int64"/> range, without an exponent) an <see cref="Int64"/> and other numbers (and the NaN, Infinity and -Infinity tokens of older versions) a <see cref="Double"/>.
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
                    return CreateBoxedString(state);
                //  The value ends where the enclosing container's values end (a value that ends an array is followed by a ']')
                ReadOnlySpan<Byte> token = default;
                Utf8Parser.GetAsciiRangeNoLast(ref token, ref d, e, endOn);
                return CreateBoxedScalar(token);
            }
            return CreateObject<Object>(state, endOn);
        }

        /// <summary>
        /// Read a boxed json string (positioned at the opening quote): a <see cref="DateOnly"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/> or <see cref="TimeSpan"/>
        /// if the text is exactly what the <see cref="JsonWriter"/> writes for such a value (so that json => object => json gives the same json), else the string.
        /// See <see cref="SpanParsers.ToBoxedDateOrTime(ReadOnlySpan{byte})"/>.
        /// </summary>
        /// <remarks>
        /// Dates and times are parsed directly from the UTF8 (no string is created).
        /// </remarks>
        static Object CreateBoxedString(JsonParserState state)
        {
            ref var d = ref state.D;
            var p = d + 1;
            var rem = new ReadOnlySpan<Byte>(p, (int)(state.E - p));
            var end = rem.IndexOf((Byte)'"');
            //  An escape (or the end of the data) makes the value null, the text after an escaped quote is never part of a date or time
            if ((end >= SpanParsers.MinBoxedDateOrTimeLength) && (end <= SpanParsers.MaxBoxedDateOrTimeLength))
            {
                var dt = SpanParsers.ToBoxedDateOrTime(rem.Slice(0, end));
                if (dt != null)
                {
                    d = p + end + 1;
                    return dt;
                }
            }
            var v = Utf8JsonParser.ReadQuotedString(state);
            //  The string had escapes (or non ASCII chars), the unescaped string can still be a date or time
            if (v.Length != end)
            {
                var l = v.Length;
                if ((l >= SpanParsers.MinBoxedDateOrTimeLength) && (l <= SpanParsers.MaxBoxedDateOrTimeLength) && System.Text.Ascii.IsValid(v))
                {
                    Span<Byte> b = stackalloc Byte[l];
                    System.Text.Ascii.FromUtf16(v, b, out _);
                    var dt = SpanParsers.ToBoxedDateOrTime(b);
                    if (dt != null)
                        return dt;
                }
            }
            return v;
        }

        /// <summary>
        /// The boxed booleans (a boxed bool is immutable, so all reads can share them)
        /// </summary>
        static readonly Object BoxedTrue = true;
        static readonly Object BoxedFalse = false;

        /// <summary>
        /// 10^0 .. 10^19 (all powers of 10 that fit in an ulong)
        /// </summary>
        static readonly ulong[] PowersOf10 = [1UL, 10UL, 100UL, 1000UL, 10000UL, 100000UL, 1000000UL, 10000000UL, 100000000UL, 1000000000UL, 10000000000UL, 100000000000UL, 1000000000000UL,
            10000000000000UL, 100000000000000UL, 1000000000000000UL, 10000000000000000UL, 100000000000000000UL, 1000000000000000000UL, 10000000000000000000UL];

        /// <summary>
        /// Convert an unquoted boxed json value: <c>true</c> / <c>false</c> (case insensitive, like <see cref="Boolean.TryParse(string, out bool)"/>) to a <see cref="Boolean"/>,
        /// integral numbers (that <see cref="Decimal.Parse(string, IFormatProvider)"/> accepts, in the <see cref="Int64"/> range) to an <see cref="Int64"/> and other numbers to a <see cref="Double"/>
        /// (numbers with an exponent and the NaN, Infinity and -Infinity tokens of older versions are parsed as a double).
        /// </summary>
        /// <remarks>
        /// [-]digits[.digits] with at most 19 digits (the common case) is converted directly from the UTF8, to the same value as the decimal that Decimal.Parse returns (no string is created).
        /// </remarks>
        static Object CreateBoxedScalar(ReadOnlySpan<Byte> token)
        {
            if (SpanParsers.TryParseSimpleDecimal(token, out var n, out var k, out var neg))
            {
                //  Integral (all decimals are zero) and in the Int64 range: an Int64 (-0 is 0)
                var pow = PowersOf10[k];
                var q = n / pow;
                if ((q * pow) == n)
                {
                    if (!neg)
                    {
                        if (q <= (ulong)Int64.MaxValue)
                            return (Int64)q;
                    }
                    else
                    {
                        if (q <= (1UL << 63))
                            return unchecked(-(Int64)q);
                    }
                }
                //  The same decimal as Decimal.Parse (the mantissa and the scale, trailing zeros are kept)
                return (Double)new Decimal((int)(uint)n, (int)(uint)(n >> 32), 0, neg, (Byte)k);
            }
            var l = token.Length;
            if ((l == 4) && System.Text.Ascii.EqualsIgnoreCase(token, "true"u8))
                return BoxedTrue;
            if ((l == 5) && System.Text.Ascii.EqualsIgnoreCase(token, "false"u8))
                return BoxedFalse;
            //  One char per byte (like Utf8Parser.ReadAsciiStringNoLast)
            var vv = System.Text.Encoding.Latin1.GetString(token);
            if (Boolean.TryParse(vv, out var br))
                return br ? BoxedTrue : BoxedFalse;
            //  Numbers with an exponent can't be parsed as a Decimal (and may be out of its range)
            //  The tokens NaN, Infinity and -Infinity are written by older versions (NaN and infinities are now written as null)
            if ((vv.AsSpan().IndexOfAny('e', 'E') >= 0) || (vv is "NaN" or "Infinity" or "-Infinity"))
                return Double.Parse(vv, NumberStyles.Float, CultureInfo.InvariantCulture);
            var val = Decimal.Parse(vv, CultureInfo.InvariantCulture);
            if ((Math.Round(val) == val) && (val >= Int64.MinValue) && (val <= Int64.MaxValue))
                return (Int64)val;
            return (Double)val;
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
            var first = MatchFirstMember<T>(ref d, e);
            if (first >= 0)
                return NewAndPopulateMembers<T>(default, first, state);
            ReadOnlySpan<Byte> header = default;
            if (!Utf8JsonParser.ReadKey(state, ref header, ref d, e, Utf8JsonParser.EndOnColon))
                return ReturnEmpty<T>(ref d);
            return NewAndPopulate<T>(header, state, endOn);
        }

        /// <summary>
        /// Match the expected first member in place (its quoted name, at the position), the position is moved to after the name if matched
        /// </summary>
        /// <returns>The index of the member, -1 if not matched (the key must be read)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int MatchFirstMember<T>(ref Byte* d, Byte* e)
        {
            //  Dictionaries are populated with their entries
            if ((DictionaryInterface<T>.Created != null) || DictionaryCheck<T>.IsDictionary)
                return -1;
            var o = ReadTyped<T>.Order;
            if ((o == null) || (*d != '"'))
                return -1;
            var keys = o.Keys;
            var mi = o.Next[keys.Length];
            if ((uint)mi >= (uint)keys.Length)
                return -1;
            var k = keys[mi];
            var kl = k.Length;
            var p = d + 1;
            if (((e - p) > kl) && (p[kl] == '"') && new ReadOnlySpan<Byte>(p, kl).SequenceEqual(k))
            {
                d = p + kl + 1;
                return mi;
            }
            return -1;
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
            var first = MatchFirstMember<T>(ref d, e);
            if (first >= 0)
                return NewAndPopulateMembers<T>(default, first, state);
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
            //  A member name is never "$type"
            var first = MatchFirstMember<T>(ref d, e);
            if (first >= 0)
                return NewAndPopulateMembers<T>(default, first, state);
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
            if (!isNew)
                return NewAndPopulate<T>(spanVal, state, endOn);
            var cp = ReadTypeCache.Get(t).Cp;
            if (cp == null)
                ReadException.ThrowCantCreate(t);
            return (T)cp(spanVal, state, endOn);
        }

        /// <summary>
        /// Create a dictionary and add all key-value pairs, starting with the already read first <paramref name="key"/>.
        /// The position is set to after the closing '}'.
        /// </summary>
        /// <remarks>Uses the dictionary's Add method, so a duplicated key throws.</remarks>
        internal static T NewAndPopulateDictionary<T>(ReadOnlySpan<Byte> key, JsonParserState state, Func<Char, bool> endOn)
        {
            //  A Dictionary<K, V> is created with the exact capacity (after all entries are read)
            var read = DictionaryReader<T>.Read;
            if (read != null)
                return read(key, state);
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
            var di = DictionaryInterface<T>.Created;
            if (di != null)
                return (T)di.Cp(key, state, endOn);
            if (DictionaryCheck<T>.IsDictionary)
                return NewAndPopulateDictionary<T>(key, state, endOn);
            return NewAndPopulateMembers<T>(key, -1, state);
        }

        /// <summary>
        /// Create an object (not a dictionary) and populate it, starting with the first member (the position is after its key).
        /// Unknown members are skipped. The position is set to after the closing '}'.
        /// </summary>
        /// <typeparam name="T">The type to create</typeparam>
        /// <param name="key">The UTF8 name of the first member (unescaped), not used if <paramref name="first"/> is the member</param>
        /// <param name="first">The index of the first member (if its key was matched in place, see <see cref="MatchFirstMember{T}(ref byte*, byte*)"/>), else -1</param>
        /// <param name="state">The parser state</param>
        /// <returns>The populated object</returns>
        static T NewAndPopulateMembers<T>(ReadOnlySpan<Byte> key, int first, JsonParserState state)
        {
            ref var d = ref state.D;
            var e = state.E;
            var members = ReadTyped<T>.GetMemberOrder(out var v);
            var keys = members.Keys;
            var assigners = members.Assigners;
            var next = members.Next;
            var count = keys.Length;
            //  The index of the previous member (count before the first member)
            var prev = count;
            //  The member (index) when the key was matched in place, else the key is in "key"
            int mi = first;
            bool matched = first >= 0;
            for (; ; )
            {
                if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != ':'))
                    ReadException.ThrowExpectedKeyValueSeparator();
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedValue();
                if (!matched)
                {
                    //  Usually the member that followed the previous member the last time, else a look up (and remember the order)
                    mi = next[prev];
                    if (!(((uint)mi < (uint)count) && key.SequenceEqual(keys[mi])))
                    {
                        if (members.Lookup.TryGetValue(state, key, out mi))
                            next[prev] = mi;
                        else
                            mi = -1;
                    }
                }
                if (mi >= 0)
                {
                    assigners[mi](ref v, state, Utf8JsonParser.EndOnObject);
                    prev = mi;
                }
                else
                {
                    Utf8JsonParser.SkipUnknown(ref d, e, Utf8JsonParser.EndOnObject);
                }
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfObject();
                var c = Utf8Parser.ReadAsciiChar(ref d, e);
                if (c == '}')
                    break;
                if (c != ',')
                    ReadException.ThrowExpectedValueSeparator();
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfObject();
                //  The expected member is matched in place: its quoted name (a member name has no quotes or escapes, so it's the same key that ReadKey would read)
                mi = next[prev];
                if (((uint)mi < (uint)count) && (*d == '"'))
                {
                    var k = keys[mi];
                    var kl = k.Length;
                    var p = d + 1;
                    if (((e - p) > kl) && (p[kl] == '"') && new ReadOnlySpan<Byte>(p, kl).SequenceEqual(k))
                    {
                        d = p + kl + 1;
                        matched = true;
                        continue;
                    }
                }
                matched = false;
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
        /// The readers of the <see cref="Dictionary{TKey, TValue}"/> that is created for a dictionary interface (<see cref="IDictionary{TKey, TValue}"/> or <see cref="IReadOnlyDictionary{TKey, TValue}"/>),
        /// null for other types (computed once per type), see <see cref="Helper.GetCreatedType(Type)"/>
        /// </summary>
        static class DictionaryInterface<T>
        {
            public static readonly ReadTypeCache Created = Get();

            static ReadTypeCache Get()
            {
                var ct = Helper.GetCreatedType(typeof(T));
                if ((ct == null) || (ct.GetGenericTypeDefinition() != typeof(Dictionary<,>)))
                    return null;
                return ReadTypeCache.Get(ct);
            }
        }

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
            var di = DictionaryInterface<T>.Created;
            if (di != null)
                return (T)di.CreateNewBoxed();
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
        /// Create a description of the current position: "[filename](row,col) : Near ==&gt;text before^text after&lt;==".
        /// </summary>
        /// <remarks>Rows are counted on LF characters.</remarks>
        static String GetThrowDetails(JsonParserState state, String filename)
        {
            var start = state.S;
            var len = (int)(state.E - start);
            var s = Utf8Parser.UTF8.GetString(start, len);
            //  The byte offset to a char offset (clamped, the position may be outside of the data)
            var bo = Math.Clamp((int)(state.D - start), 0, len);
            var o = Math.Min(Utf8Parser.UTF8.GetCharCount(start, bo), s.Length);
            int row = 1;
            int col = o + 1;
            int p = o;
            while (p > 0)
            {
                --p;
                if (s[p] == 10)
                {
                    if (col > o)
                        col = o - p;
                    ++row;
                }
            }
            var sp = Math.Max(0, o - 24);
            var ep = Math.Min(s.Length, o + 8);
            var loc = filename + "(" + row + "," + col + ") : Near ==>" + Filter(s.Substring(sp, o - sp)) + "^" + Filter(s.Substring(o, ep - o)) + "<==";
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
