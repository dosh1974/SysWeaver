using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace SysWeaver
{
    /// <summary>
    /// A read only dictionary with string keys that can be searched using a span of chars (no string is created for the key).
    /// Implemented by the frozen dictionaries with ordinal string keys returned by <see cref="DictionaryExt.Freeze{K, V}(IReadOnlyDictionary{K, V}, IEqualityComparer{K})"/>,
    /// use <see cref="SpanKeyLookupExt.TryGetValueSpan{V}(IReadOnlyDictionary{String, V}, ReadOnlySpan{Char}, out V)"/> to search any dictionary.
    /// </summary>
    /// <typeparam name="V">The value type</typeparam>
    public interface IReadOnlySpanKeyDictionary<V>
    {
        /// <summary>
        /// Get the value of a key, the chars of the key are compared ordinally (the same result as a lookup with a string with the same chars)
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value of the key, default if not found</param>
        /// <returns>True if the key was found</returns>
        bool TryGetValue(ReadOnlySpan<Char> key, [MaybeNullWhen(false)] out V value);
    }

    /// <summary>
    /// A read only set of strings that can be searched using a span of chars (no string is created for the key).
    /// Implemented by the frozen sets with ordinal string keys returned by <see cref="SetExt.Freeze{K}(IReadOnlySet{K}, IEqualityComparer{K})"/>,
    /// use <see cref="SpanKeyLookupExt.ContainsSpan(IReadOnlySet{String}, ReadOnlySpan{Char})"/> to search any set.
    /// </summary>
    public interface IReadOnlySpanKeySet
    {
        /// <summary>
        /// Check if the set contains a key, the chars of the key are compared ordinally (the same result as a lookup with a string with the same chars)
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>True if the key is in the set</returns>
        bool Contains(ReadOnlySpan<Char> key);
    }


    /// <summary>
    /// Lookups in string keyed dictionaries and sets using spans, and case insensitive lookups (in collections with lower / upper case keys) that don't create a lower / upper case string.
    /// </summary>
    /// <remarks>
    /// A span lookup doesn't allocate for:
    /// the dictionaries / sets returned by DictionaryExt.Freeze / SetExt.Freeze (any comparer that supports span lookups, like the ordinal comparers),
    /// <see cref="FrozenDictionary{TKey, TValue}"/>, <see cref="Dictionary{TKey, TValue}"/>, <see cref="ConcurrentDictionary{TKey, TValue}"/>, <see cref="FrozenSet{T}"/> and <see cref="HashSet{T}"/>
    /// with a comparer that implements <see cref="IAlternateEqualityComparer{TAlternate, T}"/> for spans (the ordinal comparers, the default string comparer).
    /// Other collections are searched with a string created from the span (the same result).
    /// </remarks>
    public static class SpanKeyLookupExt
    {
        /// <summary>
        /// Keys up to this length are lower / upper cased on the stack, longer keys are converted to a string
        /// </summary>
        const int MaxStackLength = 256;

        /// <summary>
        /// True if FastToLower returns the key itself because it's lower case ASCII (the common case: the key can be looked up as is, the fastest lookup).
        /// The same inline scan as FastToLower.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsLowerAscii(String key) => AsciiCase.FindChangeOrNonAscii(key, AsciiCase.ToLower) < 0;

        /// <summary>
        /// True if FastToUpper returns the key itself because it's upper case ASCII
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsUpperAscii(String key) => AsciiCase.FindChangeOrNonAscii(key, AsciiCase.ToUpper) < 0;

        /// <summary>
        /// True if no char of the span must change (all ASCII, no letter from first to first + 25)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsSameCaseAscii(ReadOnlySpan<Char> key, Char first)
        {
            if (key.Length > 16)
                return !key.ContainsAnyInRange(first, (Char)(first + 25)) && Ascii.IsValid(key);
            foreach (uint c in key)
                if (((c - first) < 26) || (c >= 0x80))
                    return false;
            return true;
        }

        /// <summary>
        /// Get the value of a key in a string keyed dictionary using a span (the same result as a lookup with a string with the same chars)
        /// </summary>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="d">The dictionary, must not be null</param>
        /// <param name="key">The key</param>
        /// <param name="value">The value of the key, default if not found</param>
        /// <returns>True if the key was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetValueSpan<V>(this IReadOnlyDictionary<String, V> d, ReadOnlySpan<Char> key, [MaybeNullWhen(false)] out V value)
        {
            if (d is IReadOnlySpanKeyDictionary<V> s)
                return s.TryGetValue(key, out value);
            var r = TryGetValueOther(d, key, out value);
            if (r >= 0)
                return r > 0;
            return d.TryGetValue(key.ToString(), out value);
        }

        /// <summary>
        /// Check if a string set contains a key using a span (the same result as a lookup with a string with the same chars)
        /// </summary>
        /// <param name="set">The set, must not be null</param>
        /// <param name="key">The key</param>
        /// <returns>True if the key is in the set</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsSpan(this IReadOnlySet<String> set, ReadOnlySpan<Char> key)
        {
            if (set is IReadOnlySpanKeySet s)
                return s.Contains(key);
            var r = ContainsOther(set, key);
            if (r >= 0)
                return r > 0;
            return set.Contains(key.ToString());
        }

        /// <summary>
        /// Get the value of a key in a dictionary with lower case keys, the same result (and exceptions) as d.TryGetValue(key.FastToLower(), out value), but no lower case string is created for the lookup.
        /// </summary>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="d">The dictionary (the keys should be lower case), must not be null</param>
        /// <param name="key">The key, any case, must not be null</param>
        /// <param name="value">The value of the lower cased key, default if not found</param>
        /// <returns>True if the lower cased key was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetValueLower<V>(this IReadOnlyDictionary<String, V> d, String key, [MaybeNullWhen(false)] out V value)
        {
            if ((key != null) && (key.Length <= MaxStackLength))
            {
                //  Already lower case ASCII (the common case): FastToLower would return the key, look it up as is
                if (IsLowerAscii(key))
                    return d.TryGetValue(key, out value);
                return TryGetValueCased(d, key, false, out value);
            }
            return d.TryGetValue(key.FastToLower(), out value);
        }

        /// <summary>
        /// Lower / upper case a key (at most MaxStackLength chars) on the stack and look it up with a span
        /// (not inlined: the callers' common path, a key that doesn't change, has no stack allocation)
        /// </summary>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool TryGetValueCased<V>(IReadOnlyDictionary<String, V> d, ReadOnlySpan<Char> key, bool upper, [MaybeNullWhen(false)] out V value)
        {
            Span<Char> c = stackalloc Char[key.Length];
            if (upper)
                ToUpper(key, c);
            else
                ToLower(key, c);
            if (d is IReadOnlySpanKeyDictionary<V> s)
                return s.TryGetValue(c, out value);
            var r = TryGetValueOther(d, c, out value);
            if (r >= 0)
                return r > 0;
            //  The same chars as FastToLower / FastToUpper (one string), throws like the original for a null dictionary
            return d.TryGetValue(c.ToString(), out value);
        }

        /// <summary>
        /// Lower / upper case a key (at most MaxStackLength chars) on the stack and look it up with a span
        /// </summary>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool ContainsCased(IReadOnlySet<String> set, ReadOnlySpan<Char> key)
        {
            Span<Char> c = stackalloc Char[key.Length];
            ToLower(key, c);
            if (set is IReadOnlySpanKeySet s)
                return s.Contains(c);
            var r = ContainsOther(set, c);
            if (r >= 0)
                return r > 0;
            return set.Contains(c.ToString());
        }

        /// <summary>
        /// Lower case chars (the same chars as FastToLower / ToLowerInvariant), ASCII using SIMD
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ToLower(ReadOnlySpan<Char> src, Span<Char> dst)
        {
            if (Ascii.ToLower(src, dst, out _) != OperationStatus.Done)
                src.ToLowerInvariant(dst);
        }

        /// <summary>
        /// Upper case chars (the same chars as FastToUpper / ToUpperInvariant), ASCII using SIMD
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ToUpper(ReadOnlySpan<Char> src, Span<Char> dst)
        {
            if (Ascii.ToUpper(src, dst, out _) != OperationStatus.Done)
                src.ToUpperInvariant(dst);
        }

        /// <summary>
        /// Get the value of a key in a dictionary with lower case keys, the same result as d.TryGetValue(new String(key).FastToLower(), out value), but no string is created for the lookup.
        /// </summary>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="d">The dictionary (the keys should be lower case), must not be null</param>
        /// <param name="key">The key, any case</param>
        /// <param name="value">The value of the lower cased key, default if not found</param>
        /// <returns>True if the lower cased key was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetValueLower<V>(this IReadOnlyDictionary<String, V> d, ReadOnlySpan<Char> key, [MaybeNullWhen(false)] out V value)
        {
            //  Already lower case ASCII: looked up as is (no copy)
            if (IsSameCaseAscii(key, 'A'))
                return d.TryGetValueSpan(key, out value);
            if (key.Length > MaxStackLength)
                return d.TryGetValue(ToLowerString(key), out value);
            return TryGetValueCased(d, key, false, out value);
        }

        /// <summary>
        /// A lower case string (invariant, the same chars as FastToLower) of a span
        /// </summary>
        static String ToLowerString(ReadOnlySpan<Char> key)
            => String.Create(key.Length, key, static (dst, src) => src.ToLowerInvariant(dst));

        /// <summary>
        /// Get the value of a key in a dictionary with upper case keys, the same result (and exceptions) as d.TryGetValue(key.FastToUpper(), out value), but no upper case string is created for the lookup.
        /// </summary>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="d">The dictionary (the keys should be upper case), must not be null</param>
        /// <param name="key">The key, any case, must not be null</param>
        /// <param name="value">The value of the upper cased key, default if not found</param>
        /// <returns>True if the upper cased key was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetValueUpper<V>(this IReadOnlyDictionary<String, V> d, String key, [MaybeNullWhen(false)] out V value)
        {
            if ((key != null) && (key.Length <= MaxStackLength))
            {
                //  Already upper case ASCII (the common case): FastToUpper would return the key, look it up as is
                if (IsUpperAscii(key))
                    return d.TryGetValue(key, out value);
                return TryGetValueCased(d, key, true, out value);
            }
            return d.TryGetValue(key.FastToUpper(), out value);
        }

        /// <summary>
        /// Get the value of a key in a dictionary with upper case keys, the same result as d.TryGetValue(new String(key).FastToUpper(), out value), but no string is created for the lookup.
        /// </summary>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="d">The dictionary (the keys should be upper case), must not be null</param>
        /// <param name="key">The key, any case</param>
        /// <param name="value">The value of the upper cased key, default if not found</param>
        /// <returns>True if the upper cased key was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetValueUpper<V>(this IReadOnlyDictionary<String, V> d, ReadOnlySpan<Char> key, [MaybeNullWhen(false)] out V value)
        {
            //  Already upper case ASCII: looked up as is (no copy)
            if (IsSameCaseAscii(key, 'a'))
                return d.TryGetValueSpan(key, out value);
            if (key.Length > MaxStackLength)
                return d.TryGetValue(String.Create(key.Length, key, static (dst, src) => src.ToUpperInvariant(dst)), out value);
            return TryGetValueCased(d, key, true, out value);
        }

        /// <summary>
        /// Check if a set with lower case keys contains a key, the same result (and exceptions) as set.Contains(key.FastToLower()), but no lower case string is created for the lookup.
        /// </summary>
        /// <param name="set">The set (the keys should be lower case), must not be null</param>
        /// <param name="key">The key, any case, must not be null</param>
        /// <returns>True if the lower cased key is in the set</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsLower(this IReadOnlySet<String> set, String key)
        {
            if ((key != null) && (key.Length <= MaxStackLength))
            {
                //  Already lower case ASCII (the common case): FastToLower would return the key, look it up as is
                if (IsLowerAscii(key))
                    return set.Contains(key);
                return ContainsCased(set, key);
            }
            return set.Contains(key.FastToLower());
        }

        /// <summary>
        /// Check if a set with lower case keys contains a key, the same result as set.Contains(new String(key).FastToLower()), but no string is created for the lookup.
        /// </summary>
        /// <param name="set">The set (the keys should be lower case), must not be null</param>
        /// <param name="key">The key, any case</param>
        /// <returns>True if the lower cased key is in the set</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsLower(this IReadOnlySet<String> set, ReadOnlySpan<Char> key)
        {
            //  Already lower case ASCII: looked up as is (no copy)
            if (IsSameCaseAscii(key, 'A'))
                return set.ContainsSpan(key);
            if (key.Length > MaxStackLength)
                return set.Contains(ToLowerString(key));
            return ContainsCased(set, key);
        }


        /// <summary>
        /// A span lookup in the other known dictionary implementations
        /// </summary>
        /// <returns>1 if found, 0 if not found, -1 if the dictionary doesn't support span lookups</returns>
        static int TryGetValueOther<V>(IReadOnlyDictionary<String, V> d, ReadOnlySpan<Char> key, out V value)
        {
            switch (d)
            {
                case FrozenDictionary<String, V> f:
                    if (f.TryGetAlternateLookup<ReadOnlySpan<Char>>(out var fl))
                        return fl.TryGetValue(key, out value) ? 1 : 0;
                    break;
                case Dictionary<String, V> h:
                    if (h.TryGetAlternateLookup<ReadOnlySpan<Char>>(out var hl))
                        return hl.TryGetValue(key, out value) ? 1 : 0;
                    break;
                case ConcurrentDictionary<String, V> c:
                    if (c.TryGetAlternateLookup<ReadOnlySpan<Char>>(out var cl))
                        return cl.TryGetValue(key, out value) ? 1 : 0;
                    break;
                case EmptyReadonlyDictionary<String, V>:
                    value = default;
                    return 0;
                case SingleReadonlyDictionary<String, V, ComparerKeyEquality<String>> one:
                    if (one.TryGetValue(key, out value, out var found))
                        return found ? 1 : 0;
                    break;
            }
            value = default;
            return -1;
        }

        /// <summary>
        /// A span lookup in the other known set implementations
        /// </summary>
        /// <returns>1 if found, 0 if not found, -1 if the set doesn't support span lookups</returns>
        static int ContainsOther(IReadOnlySet<String> set, ReadOnlySpan<Char> key)
        {
            switch (set)
            {
                case FrozenSet<String> f:
                    if (f.TryGetAlternateLookup<ReadOnlySpan<Char>>(out var fl))
                        return fl.Contains(key) ? 1 : 0;
                    break;
                case HashSet<String> h:
                    if (h.TryGetAlternateLookup<ReadOnlySpan<Char>>(out var hl))
                        return hl.Contains(key) ? 1 : 0;
                    break;
                case EmptyReadonlySet<String>:
                    return 0;
                case SingleReadonlySet<String, ComparerKeyEquality<String>> one:
                    if (one.TryContains(key, out var found))
                        return found ? 1 : 0;
                    break;
            }
            return -1;
        }
    }
}
