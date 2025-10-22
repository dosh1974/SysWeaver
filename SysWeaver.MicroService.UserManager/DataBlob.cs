using System;
using SysWeaver.Compression;
using SysWeaver.Serialization;
using System.Collections.Concurrent;
using System.IO;

namespace SysWeaver.Auth
{
    public sealed class DataBlob
    {
        public static DataBlob Get(String serType = "json", String compType = "br", CompEncoderLevels level = CompEncoderLevels.Balanced)
        {
            var b = Blobs;
            serType = serType.TrimStart('.').FastToLower();
            compType = compType.TrimStart('.').FastToLower();
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

        public Byte[] ToData<T>(T data)
        {
            var mem = Ser.Serialize(data);
            var ml = mem.Length;
            var b = GC.AllocateUninitializedArray<Byte>(ml + (ml >> 3) + 1024);
            var size = Comp.Compress(mem.Span, b, Level);
            var d = GC.AllocateUninitializedArray<Byte>(size);
            b.AsSpan().Slice(0, size).CopyTo(d);
            return d;
        }

        public T FromData<T>(ReadOnlySpan<Byte> data)
        {
            var l = data.Length;
            using var ms = new MemoryStream((l << 1) + 2048);
            Comp.Decompress(data, ms);
            return Ser.Create<T>(new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length));
        }

    }

}
