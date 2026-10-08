using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SysWeaver.Serialization.SwJson.Reader
{
    /// <summary>
    /// The dictionary key parsers (from the unescaped UTF8 bytes of a json key)
    /// </summary>
    static class DictionaryKey
    {
        /// <summary>
        /// An expression that parses a dictionary key: any type supported by <see cref="SpanParsers"/>, a <see cref="String"/> (using <see cref="ReadTypeCache.ParState"/>) or an enum (names or numbers)
        /// </summary>
        /// <param name="keyType">The key type</param>
        /// <param name="keyExp">The key bytes, a ReadOnlySpan&lt;Byte&gt; expression</param>
        /// <returns>An expression of the key type</returns>
        /// <exception cref="Exception">The key type isn't supported</exception>
        public static Expression GetExpression(Type keyType, Expression keyExp)
        {
            var keyValueExp = SpanParsers.GetExpression(keyType, keyExp);
            if (keyValueExp == null)
            {
                if (ReadTypeCache.JsonSpanReaders.TryGetValue(keyType, out var keyBuild))
                    keyValueExp = keyBuild(keyExp);
            }
            //  Enum keys (written as quoted numbers, names are accepted too)
            if ((keyValueExp == null) && keyType.IsEnum)
                keyValueExp = Expression.Call(Helper.SafeGetMethod(typeof(EnumReader<>).MakeGenericType(keyType), nameof(EnumReader<DayOfWeek>.Parse), BindingFlags.Public | BindingFlags.Static), keyExp);
            if (keyValueExp == null)
                throw new Exception("Unsupported key type \"" + keyType + "\"");
            return keyValueExp;
        }
    }

    /// <summary>
    /// Parse a dictionary key from the unescaped UTF8 bytes of a json key
    /// </summary>
    delegate K DictionaryKeyParser<K>(ReadOnlySpan<Byte> key, JsonParserState state);

    /// <summary>
    /// The (compiled) key parser of a key type, built on first use
    /// </summary>
    static class DictionaryKey<K>
    {
        static DictionaryKeyParser<K> P;

        /// <summary>
        /// The key parser
        /// </summary>
        /// <exception cref="Exception">The key type isn't supported</exception>
        public static DictionaryKeyParser<K> Parse
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => P ?? Init();
        }

        static DictionaryKeyParser<K> Init()
        {
            //  A race only compiles the same parser twice
            var keyExp = Expression.Parameter(typeof(ReadOnlySpan<Byte>), "key");
            var p = Expression.Lambda<DictionaryKeyParser<K>>(DictionaryKey.GetExpression(typeof(K), keyExp), keyExp, ReadTypeCache.ParState).Compile();
            P = p;
            return p;
        }
    }

    /// <summary>
    /// Room for the first entries of a dictionary (on the stack)
    /// </summary>
    [InlineArray(Length)]
    struct InlineEntries<K, V>
    {
        public const int Length = 8;

        KeyValuePair<K, V> E;
    }

    /// <summary>
    /// Reads a <see cref="Dictionary{TKey, TValue}"/>: all entries are read first, then the dictionary is created with the exact capacity
    /// (adding to a dictionary created with the default capacity resizes it for every prime it passes: 3, 7, 17, 37..).
    /// </summary>
    unsafe static class DictionaryReader
    {
        /// <summary>
        /// Create a dictionary and add all key-value pairs, starting with the already read first <paramref name="key"/> (the position is after the key).
        /// The position is set to after the closing '}'.
        /// </summary>
        /// <remarks>
        /// The entries are added in order using the dictionary's Add method (after all of them are read), so a duplicated key throws (when the object has been read).
        /// The first <see cref="InlineEntries{K, V}.Length"/> entries are kept on the stack, more entries in a buffer rented from the <see cref="ArrayPool{T}"/>.
        /// </remarks>
        public static Dictionary<K, V> Read<K, V>(ReadOnlySpan<Byte> key, JsonParserState state)
        {
            ref var d = ref state.D;
            var e = state.E;
            var parseKey = DictionaryKey<K>.Parse;
            var readValue = ReadTyped<V>.Create;
            if (readValue == null)
            {
                ReadTypeCache.Get(typeof(V));
                readValue = ReadTyped<V>.Create;
            }
            var endOn = Utf8JsonParser.EndOnObject;
            InlineEntries<K, V> inline = default;
            Span<KeyValuePair<K, V>> entries = inline;
            KeyValuePair<K, V>[] rented = null;
            int count = 0;
            try
            {
                for (; ; )
                {
                    if (Utf8Parser.SkipWhite(ref d, e) || (Utf8Parser.ReadAsciiChar(ref d, e) != ':'))
                        ReadException.ThrowExpectedKeyValueSeparator();
                    if (Utf8Parser.SkipWhite(ref d, e))
                        ReadException.ThrowExpectedValue();
                    //  The key is parsed first, it may point into a temp buffer that reading the value can overwrite
                    var k = parseKey(key, state);
                    var v = readValue(state, endOn);
                    if (count == entries.Length)
                        entries = Grow(ref rented, entries, count);
                    entries[count] = new KeyValuePair<K, V>(k, v);
                    ++count;
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
                var dict = new Dictionary<K, V>(count);
                for (int i = 0; i < count; ++i)
                {
                    ref var kv = ref entries[i];
                    dict.Add(kv.Key, kv.Value);
                }
                return dict;
            }
            finally
            {
                if (rented != null)
                {
                    if (RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<K, V>>())
                        rented.AsSpan(0, count).Clear();
                    ArrayPool<KeyValuePair<K, V>>.Shared.Return(rented);
                }
            }
        }

        /// <summary>
        /// Move the entries to a (larger) rented buffer, the previous rented buffer (if any) is returned to the pool
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static Span<KeyValuePair<K, V>> Grow<K, V>(ref KeyValuePair<K, V>[] rented, Span<KeyValuePair<K, V>> entries, int count)
        {
            var pool = ArrayPool<KeyValuePair<K, V>>.Shared;
            var nb = pool.Rent(count << 1);
            entries.Slice(0, count).CopyTo(nb);
            var old = rented;
            rented = nb;
            if (old != null)
            {
                if (RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<K, V>>())
                    old.AsSpan(0, count).Clear();
                pool.Return(old);
            }
            return nb;
        }
    }

    /// <summary>
    /// Read a dictionary of type <typeparamref name="T"/>, given the already read first key
    /// </summary>
    delegate T DictionaryCreator<T>(ReadOnlySpan<Byte> key, JsonParserState state);

    /// <summary>
    /// The <see cref="DictionaryReader.Read{K, V}"/> for <typeparamref name="T"/> if it's a <see cref="Dictionary{TKey, TValue}"/> (computed once per type)
    /// </summary>
    static class DictionaryReader<T>
    {
        /// <summary>
        /// The reader, null if <typeparamref name="T"/> isn't a <see cref="Dictionary{TKey, TValue}"/> (other dictionary types are populated using their Add method)
        /// </summary>
        public static readonly DictionaryCreator<T> Read = Get();

        static DictionaryCreator<T> Get()
        {
            var t = typeof(T);
            if (!(t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(Dictionary<,>))))
                return null;
            var m = Helper.SafeGetMethod(typeof(DictionaryReader), nameof(DictionaryReader.Read), BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(t.GetGenericArguments());
            return (DictionaryCreator<T>)m.CreateDelegate(typeof(DictionaryCreator<T>));
        }
    }

}
