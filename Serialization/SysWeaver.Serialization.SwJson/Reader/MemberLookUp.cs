using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver.Serialization.SwJson.Reader
{

    interface IMemberLookUp<T>
    {
        bool TryGetValue(JsonParserState state, ReadOnlySpan<Byte> key, out T value);
    }

    static class MemberLookUp<T>
    {
        const int DictPos = 5;

        sealed class SingleLookup : IMemberLookUp<T>
        {
            public SingleLookup(ICollection<KeyValuePair<Utf8Range, T>> values)
            {
#if DEBUG
                if (values.Count != 1)
                    throw new Exception("Invalid number of entries!");
#endif//DEBUG
                var f = values.First();
                Range = f.Key;
                Value = f.Value;
            }

            readonly Utf8Range Range;
            readonly T Value;

            public bool TryGetValue(JsonParserState state, ReadOnlySpan<Byte> key, out T value)
            {
                if (Range.Equals(key))
                {
                    value = Value;
                    return true;
                }   
                value = default;
                return false;
            }
        }

        sealed class EmptyLookup : IMemberLookUp<T>
        {
            public bool TryGetValue(JsonParserState state, ReadOnlySpan<Byte> key, out T value)
            {
                value = default;
                return false;
            }
        }

        sealed class ListLookup : IMemberLookUp<T>
        {
            public ListLookup(ICollection<KeyValuePair<Utf8Range, T>> values)
            {
                Values = values.ToArray();
            }

            readonly KeyValuePair<Utf8Range, T>[] Values;

            public bool TryGetValue(JsonParserState state, ReadOnlySpan<Byte> key, out T value)
            {
                var v = Values;
                var l = v.Length;
                for (int i = 0; i < l; ++ i)
                {
                    var t = v[i];
                    if (t.Key.Mem.Span.SequenceEqual(key))
                    { 
                        value = t.Value;
                        return true;
                    }
                }
                value = default;
                return false;
            }
        }

        sealed class DictionaryLookup : IMemberLookUp<T>
        {
            public DictionaryLookup(ICollection<KeyValuePair<Utf8Range, T>> values)
            {
                Values = new Dictionary<Utf8Range, T>(values).ToFrozenDictionary();
            }

            readonly IReadOnlyDictionary<Utf8Range, T> Values;

            public unsafe bool TryGetValue(JsonParserState state, ReadOnlySpan<Byte> key, out T value)
            {
                fixed (Byte* ptr = key)
                {
                    var mem = state.Mem;
                    var r = state.Range;
                    mem.Set(ptr, key.Length);
                    r.Mem = mem.Memory;
                    return Values.TryGetValue(r, out value);
                }
            }

        }

        sealed class LengthListLookup : IMemberLookUp<T>
        {
            public LengthListLookup(ICollection<KeyValuePair<Utf8Range, T>> values)
            {
                var maxLen = values.Max(x => x.Key.Mem.Length);
                var v = new List<KeyValuePair<Utf8Range, T>>[maxLen + 1];
                foreach (var x in values)
                {
                    var l = x.Key.Mem.Length;
                    var vals = v[l];
                    if (vals == null)
                    {
                        vals = new List<KeyValuePair<Utf8Range, T>>();
                        v[l] = vals;
                    }
                    vals.Add(x);
                }
                var vf = new IMemberLookUp<T>[maxLen + 1];
                Values = vf;
                for (int i = 0; i <= maxLen; ++i)
                {
                    var vals = v[i];
                    if (vals != null)
                    {
                        var len = vals.Count;
                        if (len == 1)
                        {
                            vf[i] = new SingleLookup(vals);
                            continue;
                        }
                        if (len <= DictPos)
                        {
                            vf[i] = new ListLookup(vals);
                            continue;
                        }
                        vf[i] = new DictionaryLookup(vals);
                    }
                }
            }

            readonly IMemberLookUp<T>[] Values;

            public bool TryGetValue(JsonParserState state, ReadOnlySpan<Byte> key, out T value)
            {
                var l = key.Length;
                var v = Values;
                if (l >= v.Length)
                {
                    value = default;
                    return false;
                }
                var vals = v[l];
                if (vals == null)
                {
                    value = default;
                    return false;
                }
                return vals.TryGetValue(state, key, out value);
            }

        }

        static readonly EmptyLookup Empty = new EmptyLookup();

        public static IMemberLookUp<T> Create(ICollection<KeyValuePair<Utf8Range, T>> values)
        {
            var len = values.Count;
            if (len <= 0)
                return Empty;
            if (len == 1)
                return new SingleLookup(values);
            if (len <= DictPos)
                return new ListLookup(values);
            return new LengthListLookup(values);
        }

    }

  }
