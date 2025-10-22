using System;
using System.Threading;

namespace SysWeaver.Serialization.SwJson.Reader
{
    unsafe sealed class JsonParserState : IDisposable
    {
        JsonParserState(Byte* d, int l)
        {
            Set(d, l);
        }

        void Set(Byte* d, int l)
        {
            S = d;
            D = d;
            E = d + l;
            if (l > 1024)
                l = 1024;
            Temp = GC.AllocateUninitializedArray<Char>(l);
            TempB = GC.AllocateUninitializedArray<Byte>(l >> 1);
        }

        public Byte* S;
        public Byte* E;
        public Byte* D;
        public Char[] Temp;
        public Byte[] TempB;
        public readonly Utf8Range Range = new Utf8Range();
        public readonly UnmanagedMemoryManager<Byte> Mem = new UnmanagedMemoryManager<byte>();

        public JsonParserState Next;



        static volatile JsonParserState First;
        static volatile int Count;

        public static JsonParserState Get(Byte* d, int l)
        {
            for (; ; )
            {
                var t = First;
                if (t == null)
                    return new JsonParserState(d, l);
                if (Interlocked.CompareExchange(ref First, t.Next, t) == t)
                {
                    Interlocked.Decrement(ref Count);
                    t.Set(d, l);
                    return t;
                }
            }

        }
        public void Dispose()
        {
            if (Count >= 32)
                return;
            for (; ;)
            {
                var t = First;
                this.Next = t;
                if (Interlocked.CompareExchange(ref First, this, t) == t)
                {
                    Interlocked.Increment(ref Count);
                    return;
                }
            }
        }

    }

}
