using System;
using SysWeaver.Compression;
using SysWeaver.Serialization;
using System.Collections.Concurrent;
using System.IO;
using System.Buffers;

namespace SysWeaver.Auth
{
    /// <summary>
    /// Serializes and compresses objects to a byte array and back
    /// </summary>
    public sealed class DataBlob
    {
        /// <summary>
        /// Get (a cached) blob handler for a serializer and compression type
        /// </summary>
        /// <param name="serType">The serializer type (extension), ex: "json"</param>
        /// <param name="compType">The compression type (http encoding or extension), ex: "br"</param>
        /// <param name="level">The compression level</param>
        /// <returns>The blob handler</returns>
        public static DataBlob Get(String serType = "json", String compType = "br", CompEncoderLevels level = CompEncoderLevels.Balanced)
        {
            var b = Blobs;
            serType = serType.FastTrimStartToLower('.');
            compType = compType.FastTrimStartToLower('.');
            var key = String.Join('|', serType, compType, (int)level);
            if (b.TryGetValue(key, out var bl))
                return bl;
            bl = new DataBlob(serType, compType, level);
            if (b.TryAdd(key, bl))
                return bl;
            return b[key];
        }

        static readonly ConcurrentDictionary<String, DataBlob> Blobs = new(StringComparer.Ordinal);

        DataBlob(String serType, String compType, CompEncoderLevels level)
        {
            Ser = SerManager.Get(serType);
            Comp = CompManager.GetFromHttp(compType);
            Level = level;
        }

        readonly ICompType Comp;
        readonly ISerializerType Ser;
        readonly CompEncoderLevels Level;

        /// <summary>
        /// Serialize and compress an object
        /// </summary>
        /// <typeparam name="T">The type of the object</typeparam>
        /// <param name="data">The object</param>
        /// <returns>The compressed data</returns>
        public Byte[] ToData<T>(T data)
        {
            var mem = Ser.Serialize(data);
            var ml = mem.Length;
            Byte[] rented = null;
            var l = ml + (ml >> 3) + 1024;
            Span<Byte> b = l <= 4096 ? stackalloc Byte[l] : (rented = ArrayPoolStream.Rent(l));
            try
            {
                var size = Comp.Compress(mem.Span, b, Level);
                var d = GC.AllocateUninitializedArray<Byte>(size);
                b.Slice(0, size).CopyTo(d);
                return d;
            }
            finally
            {
                if (rented != null)
                    ArrayPoolStream.Return(rented);
            }
        }

        /// <summary>
        /// Decompress and deserialize an object
        /// </summary>
        /// <typeparam name="T">The type of the object</typeparam>
        /// <param name="data">The compressed data (from <see cref="ToData{T}(T)"/>)</param>
        /// <returns>The object</returns>
        public T FromData<T>(ReadOnlySpan<Byte> data)
        {
            var l = data.Length;
            using var ms = new MemoryStream((l << 1) + 2048);
            Comp.Decompress(data, ms);
            return Ser.Create<T>(new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length));
        }

    }

}
