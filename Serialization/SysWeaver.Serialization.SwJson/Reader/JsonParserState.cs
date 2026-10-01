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

        /// <summary>
        /// The initial size of the temp buffers (they grow as needed, and are kept when the state is reused)
        /// </summary>
        const int InitialTempSize = 256;

        /// <summary>
        /// Temp buffers larger than this are released when the state is returned to the pool
        /// </summary>
        const int MaxKeptTempSize = 1 << 16;

        void Set(Byte* d, int l)
        {
            S = d;
            D = d;
            E = d + l;
            Temp ??= GC.AllocateUninitializedArray<Char>(InitialTempSize);
            TempB ??= GC.AllocateUninitializedArray<Byte>(InitialTempSize);
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
            if (Temp.Length > MaxKeptTempSize)
                Temp = null;
            if (TempB.Length > MaxKeptTempSize)
                TempB = null;
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
