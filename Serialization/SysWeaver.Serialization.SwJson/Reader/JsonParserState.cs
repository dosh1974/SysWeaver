using System;

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
        /// The max number of pooled instances
        /// </summary>
        const int MaxPooled = 32;

        /// <summary>
        /// The pooled (free) instances, the first <see cref="PoolCount"/> are used, only accessed while holding <see cref="PoolLock"/>
        /// </summary>
        static readonly JsonParserState[] Pool = new JsonParserState[MaxPooled];

        /// <summary>
        /// The number of pooled instances, only accessed while holding <see cref="PoolLock"/>
        /// </summary>
        static int PoolCount;

        /// <summary>
        /// Protects the pool (the lock is only held while taking or adding an instance).
        /// The pool used to be a lock free stack without ABA protection, so two parses could get the same instance.
        /// </summary>
        static readonly Object PoolLock = new Object();

        /// <summary>
        /// Get a pooled (or new) state for parsing the data.
        /// </summary>
        /// <param name="d">The start of the (pinned) UTF8 data</param>
        /// <param name="l">The length of the data in bytes</param>
        /// <returns>A state positioned at the start of the data, dispose to return it to the pool</returns>
        /// <remarks>Thread safe, an instance is never returned to more than one caller (until it's disposed).</remarks>
        public static JsonParserState Get(Byte* d, int l)
        {
            JsonParserState t = null;
            lock (PoolLock)
            {
                var c = PoolCount;
                if (c > 0)
                {
                    --c;
                    t = Pool[c];
                    Pool[c] = null;
                    PoolCount = c;
                }
            }
            if (t == null)
                return new JsonParserState(d, l);
            t.Set(d, l);
            return t;
        }

        /// <summary>
        /// Return the state to the pool (dropped if the pool already holds 32 instances, temp buffers larger than 64K chars/bytes are released).
        /// Must only be called once per <see cref="Get(byte*, int)"/>, and the state must not be used afterwards.
        /// </summary>
        public void Dispose()
        {
            if (Temp.Length > MaxKeptTempSize)
                Temp = null;
            if (TempB.Length > MaxKeptTempSize)
                TempB = null;
            //  Don't keep pointers to the (no longer pinned) data
            S = null;
            D = null;
            E = null;
            lock (PoolLock)
            {
                var c = PoolCount;
                if (c >= MaxPooled)
                    return;
                Pool[c] = this;
                PoolCount = c + 1;
            }
        }

    }

}
