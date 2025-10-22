using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SysWeaver.Serialization.SwJson.Reader
{
    sealed class Utf8Range : IEquatable<Utf8Range>
    {
#if DEBUG
        public override String ToString() => Utf8Parser.UTF8.GetString(Mem.Span);
#endif//DEBUG

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<Byte> AsSpan() => Mem.Span;


        public String GetString() => Utf8Parser.UTF8.GetString(Mem.Span);

        public static Utf8Range Create(String s)
        {
            var c = Cache;
            if (c.TryGetValue(s, out var r))
                return r;
            lock (c)
            {
                if (c.TryGetValue(s, out r))
                    return r;
                var d = Utf8Parser.UTF8.GetBytes(s);
                r = new Utf8Range();
                r.Mem = new ReadOnlyMemory<byte>(d, 0, d.Length);
                c[s] = r;
                return r;
            }
        }

        static readonly ConcurrentDictionary<String, Utf8Range> Cache = new ConcurrentDictionary<string, Utf8Range>(StringComparer.Ordinal);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => Equals(obj as Utf8Range);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            var m = Mem;
            return (m.Length << 8) | m.Span[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Utf8Range other) => Mem.Span.SequenceEqual(other.Mem.Span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ReadOnlySpan<Byte> om) => Mem.Span.SequenceEqual(om);



        public ReadOnlyMemory<Byte> Mem;

        public Utf8Range Clone() => new Utf8Range
        {
            Mem = Mem
        };

    }

}
