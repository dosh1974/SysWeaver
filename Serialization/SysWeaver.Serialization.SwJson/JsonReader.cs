using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using SysWeaver.Serialization.SwJson.Reader;

namespace SysWeaver.Serialization.SwJson
{


    /// <summary>
    /// Methods for creating an object given some json
    /// </summary>
    public unsafe static class JsonReader
    {
        /// <summary>
        /// Create an object from a byte array, containing an UTF8 encoded json string
        /// </summary>
        /// <typeparam name="T">The type of the object to create</typeparam>
        /// <param name="data">UTF8 encoded data containing a json representation of an object</param>
        /// <param name="filename">An optional filename to use when reporting exceptions</param>
        /// <returns>The object represented in the supplied json</returns>
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
        /// Create an object from a json string
        /// </summary>
        /// <typeparam name="T">The type of the object to create</typeparam>
        /// <param name="s">The json object string</param>
        /// <param name="filename">An optional filename to use when reporting exceptions</param>
        /// <returns>The object represented in the supplied json string</returns>
        public static T Create<T>(String s, String filename = null)
        {
            var utf8 = Utf8Parser.UTF8;
            var size = utf8.GetMaxByteCount(s.Length);
            var data = size <= 4096 ? stackalloc Byte[size] : GC.AllocateUninitializedArray<Byte>(size).AsSpan();
            var l = utf8.GetBytes(s, data);
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
        /// Create an object from a byte array, containing an UTF8 encoded json string
        /// </summary>
        /// <param name="type">The type of object to create</param>
        /// <param name="data">UTF8 encoded data containing a json representation of an object</param>
        /// <param name="filename">An optional filename to use when reporting exceptions</param>
        /// <returns>The object represented in the supplied json</returns>
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
        /// Create an object from a json string
        /// </summary>
        /// <param name="type">The type of object to create</param>
        /// <param name="s">The json object string</param>
        /// <param name="filename">An optional filename to use when reporting exceptions</param>
        /// <returns>The object represented in the supplied json string</returns>
        public static Object Create(Type type, String s, String filename = null)
        {
            var utf8 = Utf8Parser.UTF8;
            var size = utf8.GetMaxByteCount(s.Length);
            var data = size <= 4096 ? stackalloc Byte[size] : GC.AllocateUninitializedArray<Byte>(size).AsSpan();
            var l = utf8.GetBytes(s, data);
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

        #region Arrays

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
#if VERBOSE
            if (!t.IsAssignableFrom(newType))
                throw new Exception("Can't assign a value of type \"" + newType.FullName + "\" to a member of type \"" + t.FullName + "\"");
#endif//VERBOSE
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
            var ld = new List<T>(1024);
            var createTyped = ReadTyped<T>.Create;
            for (; ;)
            {
                if ((Char)(*d) == ']')
                {
                    ++d;
                    break;
                }
                ld.Add(createTyped(state, Utf8JsonParser.EndOnArray));
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfArray();
                c = Utf8Parser.ReadAsciiChar(ref d, e);
                if (c == ']')
                    break;
                if (c != ',')
                    ReadException.ThrowExpectedValueSeparator();
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfArray();
            }
            var count = ld.Count;
            if (count <= 0)
                return [];
//            var a = new T[count];
            var a = GC.AllocateUninitializedArray<T>(count);
            for (int i = 0; i < count; ++i)
                a[i] = ld[i];
            return a;
        }

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
            var ld = new List<T>();
            var createTyped = ReadTyped<T>.Create;
            for (; ; )
            {
                if ((Char)(*d) == ']')
                {
                    ++d;
                    break;
                }
                ld.Add(createTyped(state, Utf8JsonParser.EndOnArray));
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfArray();
                c = Utf8Parser.ReadAsciiChar(ref d, e);
                if (c == ']')
                    break;
                if (c != ',')
                    ReadException.ThrowExpectedValueSeparator();
                if (Utf8Parser.SkipWhite(ref d, e))
                    ReadException.ThrowExpectedEndOfArray();
            }
            var ct = typeof(C);
            if (ct == typeof(List<T>))
                return ld;
            return (ICollection<T>)Activator.CreateInstance(ct, ld);
        }

        #endregion//Arrays

        #region Object

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T CreateNullableObject<T>(JsonParserState state, Func<Char, bool> endOn)
        {
            return Utf8JsonParser.IsNullState(state) ? default(T) : CreateObject<T>(state, endOn);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T CreateSealedNullableObject<T>(JsonParserState state, Func<Char, bool> endOn)
        {
            return Utf8JsonParser.IsNullState(state) ? default(T) : CreateSealedObject<T>(state, endOn);
        }

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
#if VERBOSE
            if (!t.IsAssignableFrom(newType))
                throw new Exception("Can't assign a value of type \"" + newType.FullName + "\" to a member of type \"" + t.FullName + "\"");
#endif//VERBOSE
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

        internal static T NewAndPopulate<T>(ReadOnlySpan<Byte> key, JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            var t = typeof(T);
            if (t.IsGenericType)
            {
                var args = t.GetGenericArguments();
                if (args.Length == 2)
                    if (typeof(IDictionary<,>).MakeGenericType(args).IsAssignableFrom(t))
                        return NewAndPopulateDictionary<T>(key, state, endOn);
            }
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

        static readonly Utf8Range TypeKey = Utf8Range.Create("$type");
        static readonly Utf8Range ValueKey = Utf8Range.Create("$value");
        static readonly Utf8Range ValuesKey = Utf8Range.Create("$values");

        static T ReturnEmpty<T>(ref Byte* d)
        {
            ++d;
            ReadTyped<T>.GetMembers(out var v);
            return v;
        }

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
                throw new Exception("for type \"" + t.FullName + "\"", ex);
            }
#endif//VERBOSE
        }

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
                ReadTypeCache.Get(typeof(T));
                return ReadTyped<T>.Create(state, Utf8JsonParser.EndOnObject);
#if VERBOSE
            }
            catch (Exception ex)
            {
                throw new Exception("for type \"" + typeof(T).FullName + "\"", ex);
            }
#endif//VERBOSE
        }

        #region Exception details

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

        static String Filter(String s)
        {
            var l = s.Length;
            var sb = GC.AllocateUninitializedArray<Char>(l);
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                if (c < 32)
                    c = (Char)32;
                sb[i] = c;
            }
            return new String(sb);
        }

        #endregion//Exception details

    }

}
