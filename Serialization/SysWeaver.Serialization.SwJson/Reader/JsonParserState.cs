using System;
using System.Threading;

namespace SysWeaver.Serialization.SwJson.Reader
{
    /// <summary>
    /// The state of one parse: the data pointers, temp buffers and reusable look up objects.
    /// Instances are pooled (max 32), get one with <see cref="Get(byte*, int)"/> and return it with <see cref="Dispose"/>.
    /// </summary>
    /// <remarks>
    /// Not thread safe, an instance must only be used by one parse at a time.
    /// The data must stay pinned while the state is in use.
    /// </remarks>
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

        /// <summary>
        /// The start of the data (used for error positions).
        /// </summary>
        public Byte* S;
        /// <summary>
        /// The end of the data (exclusive).
        /// </summary>
        public Byte* E;
        /// <summary>
        /// The current position, advanced by the parsing methods (usually accessed by ref).
        /// </summary>
        public Byte* D;
        /// <summary>
        /// A temp char buffer for decoding strings, grows as needed (replaced by a larger array).
        /// </summary>
        public Char[] Temp;
        /// <summary>
        /// A temp byte buffer for unescaped keys, grows as needed (replaced by a larger array).
        /// </summary>
        public Byte[] TempB;
        /// <summary>
        /// A reusable range used for dictionary look ups of keys in the data.
        /// </summary>
        public readonly Utf8Range Range = new Utf8Range();
        /// <summary>
        /// A reusable memory manager that wraps pointers into the data as <see cref="Memory{T}"/>.
        /// </summary>
        public readonly UnmanagedMemoryManager<Byte> Mem = new UnmanagedMemoryManager<byte>();

        /// <summary>
        /// The next free instance in the pool.
        /// </summary>
        public JsonParserState Next;



        static volatile JsonParserState First;
        static volatile int Count;

        /// <summary>
        /// Get a pooled (or new) state for parsing the data.
        /// </summary>
        /// <param name="d">The start of the (pinned) UTF8 data</param>
        /// <param name="l">The length of the data in bytes</param>
        /// <returns>A state positioned at the start of the data, dispose to return it to the pool</returns>
        /// <remarks>The pool is a lock free stack without ABA protection.</remarks>
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
        /// <summary>
        /// Return the state to the pool (dropped if the pool already holds 32 instances, temp buffers larger than 64K chars/bytes are released).
        /// Must only be called once per <see cref="Get(byte*, int)"/>, and the state must not be used afterwards.
        /// </summary>
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
