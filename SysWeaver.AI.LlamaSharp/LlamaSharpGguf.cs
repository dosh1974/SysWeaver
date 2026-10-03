using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SysWeaver.AI
{
    /// <summary>
    /// Meta data about a GGUF file where the multi token prediction (MTP / nextn) layers have been removed
    /// </summary>
    public sealed class LlamaSharpStrippedGguf
    {
        /// <summary>
        /// The file name of the stripped model
        /// </summary>
        public String Filename;

        /// <summary>
        /// The size of the stripped model in bytes
        /// </summary>
        public long Length;

        /// <summary>
        /// The number of layers that was removed
        /// </summary>
        public int RemovedLayers;
    }


    /// <summary>
    /// Minimal GGUF reader / writer, used to make models loadable by the bundled llama.cpp version.
    /// Some models (ex: qwen35) includes multi token prediction (MTP / nextn) layers as the last blocks,
    /// older llama.cpp versions treats these as regular layers and fails to load the model (missing tensor 'blk.N.ssm_conv1d.weight').
    /// </summary>
    static class LlamaSharpGguf
    {
        const uint Magic = 0x46554747; // "GGUF"
        const uint DefaultAlignment = 32;

        const uint TypeUInt8 = 0;
        const uint TypeInt8 = 1;
        const uint TypeUInt16 = 2;
        const uint TypeInt16 = 3;
        const uint TypeUInt32 = 4;
        const uint TypeInt32 = 5;
        const uint TypeFloat32 = 6;
        const uint TypeBool = 7;
        const uint TypeString = 8;
        const uint TypeArray = 9;
        const uint TypeUInt64 = 10;
        const uint TypeInt64 = 11;
        const uint TypeFloat64 = 12;

        sealed class Kv
        {
            public override string ToString() => Key;
            public String Key;
            public uint Type;
            /// <summary>
            /// Value of integer types, else 0
            /// </summary>
            public long IntValue;
            /// <summary>
            /// Value of string types, else null
            /// </summary>
            public String StringValue;
            public long Start;
            public long End;
        }

        sealed class Tensor
        {
            public override string ToString() => Name;
            public String Name;
            public ulong[] Dims;
            public uint Type;
            public ulong Offset;
            /// <summary>
            /// Size including padding (distance to the next tensor)
            /// </summary>
            public ulong PaddedSize;
        }

        sealed class Info
        {
            public uint Version;
            public List<Kv> Kvs;
            public List<Tensor> Tensors;
            public long DataStart;
            public String Arch;
            public Kv BlockCount;
            public Kv NextN;
        }

        static String ReadString(BinaryReader r)
        {
            var len = r.ReadUInt64();
            if (len > int.MaxValue)
                throw new InvalidDataException("Invalid GGUF string length");
            return Encoding.UTF8.GetString(r.ReadBytes((int)len));
        }

        static void WriteString(BinaryWriter w, String s)
        {
            var b = Encoding.UTF8.GetBytes(s);
            w.Write((ulong)b.Length);
            w.Write(b);
        }

        static int GetSize(uint type)
            => type switch
            {
                TypeUInt8 or TypeInt8 or TypeBool => 1,
                TypeUInt16 or TypeInt16 => 2,
                TypeUInt32 or TypeInt32 or TypeFloat32 => 4,
                TypeUInt64 or TypeInt64 or TypeFloat64 => 8,
                _ => throw new InvalidDataException("Unknown GGUF value type " + type),
            };

        static void SkipValue(BinaryReader r, uint type)
        {
            if (type == TypeString)
            {
                var len = r.ReadUInt64();
                r.BaseStream.Seek((long)len, SeekOrigin.Current);
                return;
            }
            if (type == TypeArray)
            {
                var at = r.ReadUInt32();
                var count = r.ReadUInt64();
                if ((at == TypeString) || (at == TypeArray))
                {
                    for (ulong i = 0; i < count; ++i)
                        SkipValue(r, at);
                    return;
                }
                r.BaseStream.Seek((long)count * GetSize(at), SeekOrigin.Current);
                return;
            }
            r.BaseStream.Seek(GetSize(type), SeekOrigin.Current);
        }

        static bool TryReadInt(BinaryReader r, uint type, out long value)
        {
            switch (type)
            {
                case TypeUInt8: value = r.ReadByte(); return true;
                case TypeInt8: value = r.ReadSByte(); return true;
                case TypeUInt16: value = r.ReadUInt16(); return true;
                case TypeInt16: value = r.ReadInt16(); return true;
                case TypeUInt32: value = r.ReadUInt32(); return true;
                case TypeInt32: value = r.ReadInt32(); return true;
                case TypeUInt64: value = (long)r.ReadUInt64(); return true;
                case TypeInt64: value = r.ReadInt64(); return true;
            }
            value = 0;
            return false;
        }

        static void WriteInt(BinaryWriter w, uint type, long value)
        {
            switch (type)
            {
                case TypeUInt8: w.Write((byte)value); return;
                case TypeInt8: w.Write((sbyte)value); return;
                case TypeUInt16: w.Write((ushort)value); return;
                case TypeInt16: w.Write((short)value); return;
                case TypeUInt32: w.Write((uint)value); return;
                case TypeInt32: w.Write((int)value); return;
                case TypeUInt64: w.Write((ulong)value); return;
                case TypeInt64: w.Write(value); return;
            }
            throw new InvalidDataException("Not an integer GGUF value type " + type);
        }

        static Info Read(Stream s)
        {
            using var r = new BinaryReader(s, Encoding.UTF8, true);
            if (r.ReadUInt32() != Magic)
                throw new InvalidDataException("Not a GGUF file");
            var version = r.ReadUInt32();
            //  Version 1 used 32 bit counts and lengths
            if (version < 2)
                throw new InvalidDataException("Unsupported GGUF version " + version);
            var tensorCount = r.ReadUInt64();
            var kvCount = r.ReadUInt64();
            var kvs = new List<Kv>((int)Math.Min(kvCount, 4096));
            uint alignment = DefaultAlignment;
            String arch = null;
            for (ulong i = 0; i < kvCount; ++i)
            {
                var kv = new Kv
                {
                    Start = s.Position,
                    Key = ReadString(r),
                    Type = r.ReadUInt32(),
                };
                if (kv.Type == TypeString)
                    kv.StringValue = ReadString(r);
                else if (TryReadInt(r, kv.Type, out var v))
                    kv.IntValue = v;
                else
                    SkipValue(r, kv.Type);
                kv.End = s.Position;
                kvs.Add(kv);
                if (kv.Key == "general.alignment")
                    alignment = (uint)kv.IntValue;
                else if (kv.Key == "general.architecture")
                    arch = kv.StringValue;
            }
            var tensors = new List<Tensor>((int)Math.Min(tensorCount, 1 << 20));
            for (ulong i = 0; i < tensorCount; ++i)
            {
                var t = new Tensor
                {
                    Name = ReadString(r),
                };
                var nd = r.ReadUInt32();
                t.Dims = new ulong[nd];
                for (int d = 0; d < nd; ++d)
                    t.Dims[d] = r.ReadUInt64();
                t.Type = r.ReadUInt32();
                t.Offset = r.ReadUInt64();
                tensors.Add(t);
            }
            if (alignment == 0)
                alignment = DefaultAlignment;
            var dataStart = Align(s.Position, alignment);
            var dataLen = (ulong)(s.Length - dataStart);
            var sorted = tensors.OrderBy(x => x.Offset).ToArray();
            for (int i = 0; i < sorted.Length; ++i)
            {
                var end = i + 1 < sorted.Length ? sorted[i + 1].Offset : dataLen;
                sorted[i].PaddedSize = end - sorted[i].Offset;
            }
            var info = new Info
            {
                Version = version,
                Kvs = kvs,
                Tensors = tensors,
                DataStart = dataStart,
                Arch = arch,
            };
            if (arch != null)
            {
                info.BlockCount = kvs.Find(x => x.Key == arch + ".block_count");
                info.NextN = kvs.Find(x => x.Key == arch + ".nextn_predict_layers");
            }
            return info;
        }

        static long Align(long pos, uint alignment)
            => (pos + alignment - 1) / alignment * alignment;

        /// <summary>
        /// Get the number of multi token prediction layers that can be removed
        /// </summary>
        static int GetStrippableLayers(Info info)
        {
            var bc = info.BlockCount;
            var nn = info.NextN;
            if ((bc == null) || (nn == null) || (nn.IntValue <= 0) || (nn.IntValue >= bc.IntValue))
                return 0;
            //  The last layers must contain the nextn tensors
            var first = bc.IntValue - nn.IntValue;
            for (long i = first; i < bc.IntValue; ++i)
            {
                var prefix = String.Concat("blk.", i.ToString(), ".nextn.");
                if (!info.Tensors.Any(x => x.Name.StartsWith(prefix, StringComparison.Ordinal)))
                    return 0;
            }
            return (int)nn.IntValue;
        }

        static bool IsInLayer(String tensorName, long firstLayer)
        {
            if (!tensorName.StartsWith("blk.", StringComparison.Ordinal))
                return false;
            var e = tensorName.IndexOf('.', 4);
            return (e > 4) && long.TryParse(tensorName.AsSpan(4, e - 4), out var layer) && (layer >= firstLayer);
        }

        /// <summary>
        /// Check if a GGUF file have multi token prediction (MTP / nextn) layers that can be removed
        /// </summary>
        /// <param name="filename">The GGUF file</param>
        /// <param name="arch">The architecture of the model</param>
        /// <returns>The number of layers that can be removed, 0 if none</returns>
        public static int GetStrippableLayers(String filename, out String arch)
        {
            using var s = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            var info = Read(s);
            arch = info.Arch;
            return GetStrippableLayers(info);
        }

        /// <summary>
        /// Create a copy of a GGUF file without the multi token prediction (MTP / nextn) layers
        /// </summary>
        /// <param name="source">The source GGUF file</param>
        /// <param name="dest">The destination file</param>
        /// <returns>The number of layers that was removed</returns>
        public static int StripMtpLayers(String source, String dest)
        {
            using var s = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
            var info = Read(s);
            var removed = GetStrippableLayers(info);
            if (removed <= 0)
                throw new InvalidDataException("The model doesn't have any multi token prediction layers that can be removed");
            var bc = info.BlockCount;
            var nn = info.NextN;
            var firstRemoved = bc.IntValue - removed;
            var keep = info.Tensors.Where(x => !IsInLayer(x.Name, firstRemoved)).ToList();
            var keepSet = new HashSet<Tensor>(keep);
            //  Assign new offsets (in the original data order, sizes includes padding so offsets stays aligned)
            var newOffsets = new Dictionary<Tensor, ulong>();
            ulong offset = 0;
            var dataOrder = keep.OrderBy(x => x.Offset).ToArray();
            foreach (var t in dataOrder)
            {
                newOffsets[t] = offset;
                offset += t.PaddedSize;
            }
            var alignmentKv = info.Kvs.Find(x => x.Key == "general.alignment");
            var alignment = (alignmentKv == null) || (alignmentKv.IntValue <= 0) ? DefaultAlignment : (uint)alignmentKv.IntValue;
            using (var d = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
            {
                using (var w = new BinaryWriter(d, Encoding.UTF8, true))
                {
                    w.Write(Magic);
                    w.Write(info.Version);
                    w.Write((ulong)keep.Count);
                    w.Write((ulong)(info.Kvs.Count - 1));
                    var buffer = new byte[1 << 16];
                    foreach (var kv in info.Kvs)
                    {
                        if (kv == nn)
                            continue;
                        if (kv == bc)
                        {
                            WriteString(w, kv.Key);
                            w.Write(kv.Type);
                            WriteInt(w, kv.Type, firstRemoved);
                            continue;
                        }
                        //  Copy the raw key value
                        s.Position = kv.Start;
                        var left = kv.End - kv.Start;
                        while (left > 0)
                        {
                            var rd = s.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
                            if (rd <= 0)
                                throw new EndOfStreamException();
                            w.Write(buffer, 0, rd);
                            left -= rd;
                        }
                    }
                    foreach (var t in keep)
                    {
                        WriteString(w, t.Name);
                        w.Write((uint)t.Dims.Length);
                        foreach (var x in t.Dims)
                            w.Write(x);
                        w.Write(t.Type);
                        w.Write(newOffsets[t]);
                    }
                    w.Flush();
                    var pad = Align(d.Position, alignment) - d.Position;
                    for (long i = 0; i < pad; ++i)
                        w.Write((byte)0);
                    w.Flush();
                }
                //  Copy tensor data
                var copyBuffer = new byte[1 << 22];
                foreach (var t in dataOrder)
                {
                    s.Position = info.DataStart + (long)t.Offset;
                    var left = (long)t.PaddedSize;
                    while (left > 0)
                    {
                        var rd = s.Read(copyBuffer, 0, (int)Math.Min(copyBuffer.Length, left));
                        if (rd <= 0)
                            throw new EndOfStreamException();
                        d.Write(copyBuffer, 0, rd);
                        left -= rd;
                    }
                }
            }
            return removed;
        }

        /// <summary>
        /// Get (or create) a copy of a model without the multi token prediction (MTP / nextn) layers.
        /// Uses the FileMetaData system, so the copy is reused as long as the source file is unchanged (and old copies are pruned).
        /// </summary>
        /// <param name="filename">The source model file</param>
        /// <param name="name">The model name (for logging)</param>
        /// <param name="msg">Optional message host</param>
        /// <returns>The file name of the stripped model, or null if the model can't be stripped</returns>
        public static String GetStrippedModel(String filename, String name, IMessageHost msg)
        {
            int layers;
            String arch;
            try
            {
                layers = GetStrippableLayers(filename, out arch);
            }
            catch
            {
                return null;
            }
            if (layers <= 0)
                return null;
            msg?.AddMessage(String.Concat("The model ", name.ToQuoted(), " (", arch, ") has ", layers.ToString(), " multi token prediction (MTP) layer(s) that the bundled llama.cpp doesn't support, using a copy of the model without them"), MessageLevels.Warning);
            try
            {
                var meta = FileMetaData.Process<LlamaSharpStrippedGguf>("Gguf", filename, (src, destBase, existing) =>
                {
                    var dest = destBase + ".gguf";
                    var fi = new FileInfo(dest);
                    if ((existing != null) && fi.Exists && (fi.Length == existing.Length) && String.Equals(existing.Filename, fi.FullName, StringComparison.OrdinalIgnoreCase))
                        return null;
                    msg?.AddMessage(String.Concat("Creating a copy of ", src.ToQuoted(), " without MTP layers at ", fi.FullName.ToQuoted(), ", this may take a while"), MessageLevels.Warning);
                    var temp = dest + ".tmp";
                    try
                    {
                        var removed = StripMtpLayers(src, temp);
                        File.Move(temp, dest, true);
                        fi.Refresh();
                        return new LlamaSharpStrippedGguf
                        {
                            Filename = fi.FullName,
                            Length = fi.Length,
                            RemovedLayers = removed,
                        };
                    }
                    finally
                    {
                        try
                        {
                            File.Delete(temp);
                        }
                        catch
                        {
                        }
                    }
                }, 30, "NoMtp");
                var fn = meta?.Filename;
                if ((fn == null) || (!File.Exists(fn)))
                    return null;
                return fn;
            }
            catch (Exception ex)
            {
                msg?.AddMessage(String.Concat("Failed to create a copy of the model ", name.ToQuoted(), " without MTP layers"), ex, MessageLevels.Error);
                return null;
            }
        }
    }
}
