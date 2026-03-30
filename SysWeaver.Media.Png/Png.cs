using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using SysWeaver.Compression;
using SysWeaver.IO;

namespace SysWeaver.Media
{
    /// <summary>
    /// Portable PNG loader, decodes to 32-bit argb Byte array only.
    /// Do NOT support interlaced images - otherwise covers all files in the PngSuite: http://www.schaik.com/pngsuite/
    /// It's verified against the WIC png codec and agrees with a MSE of zero for all files except 16-bit per pixel gray scale images (the PNG WIC doesn't seem to support 16 bpp grayscale images at all).
    /// </summary>
    public static class Png
    {

        /// <summary>
        /// Determine if some data seems to be a png file (by inspecting the first 8 bytes)
        /// </summary>
        /// <param name="data">The data to test</param>
        /// <returns>Tru if the data header matches a png header</returns>
        public static bool IsPng(Byte[] data)
        {
            if (data.Length < 8)
                return false;
            for (int i = 0; i < Header.Length; ++ i)
            {
                if (data[i] != Header[i])
                    return false;
            }
            return true;
        }                

        /// <summary>
        /// Represents a png chunk
        /// </summary>
        public sealed class Chunk
        {
            public Chunk(uint length, uint id, Byte[] data, uint crc32)
            {
                Length = length;
                Id = id;
                Data = data;
                Crc32 = crc32;
            }
            public override string ToString()
            {
                return String.Join("", (Char)((Id >> 24) & 0xff), (Char)((Id >> 16) & 0xff), (Char)((Id >> 8) & 0xff), (Char)((Id >> 0) & 0xff), " [0x", Id.ToString("x8"), "] @ ", Length);
            }
            public readonly uint Length;
            public readonly uint Id;
            public readonly Byte[] Data;
            public readonly uint Crc32;
        }

        /// <summary>
        /// Read all png chunks from a a png file stream
        /// </summary>
        /// <param name="pngFile">The png file stream</param>
        /// <returns>The png chunks in the file</returns>
        public static IEnumerable<Chunk> ReadChunks(Stream pngFile)
        {
            var end = Id_IEND;
            using var r = EndianAwareBinaryReader.OpenBigEndian(pngFile, Encoding.UTF8);
            //  Validate header
            for (int i = 0; i < 8; ++i)
            {
                if (Header[i] != r.ReadByte())
                    throw new IOException("Invalid PNG stream, header no valid!");
            }
            //  Iterate over hunks
            for (; ; )
            {
                uint length = r.ReadUInt32();
                uint hunkId = r.ReadUInt32();
                var pos = pngFile.Position;
                var endPos = pos += length;
                var data = new Byte[length];
                if (r.Read(data, 0, (int)length) != (int)length)
                    throw new IOException("Invalid PNG stream, not enough data to read hunk!");
                if (pngFile.Position != endPos)
                    throw new IOException("Invalid PNG stream, declared chunk size doesn't match the parsed chunk size!");
                uint crc = r.ReadUInt32();
                yield return new Chunk(length, hunkId, data, crc);
                if (hunkId == end)
                    break;
            }
        }

        static readonly uint[] CrcTable = GetCrcTable();

        static uint[] GetCrcTable()
        {
            var crcTable = new uint[256];
            for (uint n = 0; n <= 255; n++)
            {
                var c = n;
                for (var k = 0; k <= 7; k++)
                {
                    if ((c & 1) == 1)
                        c = 0xEDB88320 ^ ((c >> 1) & 0x7FFFFFFF);
                    else
                        c = ((c >> 1) & 0x7FFFFFFF);
                }
                crcTable[n] = c;
            }
            return crcTable;
        }

        /// <summary>
        /// Helper method to compute the Crc32 checksum.
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="offset">Offset in the data</param>
        /// <param name="len">Length to compute for</param>
        /// <param name="crc">Initial crc</param>
        /// <returns>The Crc32 check sum for the specified range</returns>
        public static uint CalcCrc32(Byte[] data, long offset, long len, uint crc = 0)
        {
            var end = offset + Math.Max(0, Math.Min(data.LongLength - offset, len));
            var c = crc ^ 0xffffffff;
            while (offset < end)
            {
                var b = data[offset];
                ++offset;
                c = CrcTable[(c ^ b) & 255] ^ ((c >> 8) & 0xFFFFFF);
            }
            return c ^ 0xffffffff;
        }

        /// <summary>
        /// Helper method to compute the Crc32 checksum.
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="crc">Initial crc</param>
        /// <returns>The crc32 for the data</returns>
        public static uint CalcCrc32(ReadOnlySpan<Byte> data, uint crc = 0)
        {
            var c = crc ^ 0xffffffff;
            foreach (var b in data)
                c = CrcTable[(c ^ b) & 255] ^ ((c >> 8) & 0xFFFFFF);
            return c ^ 0xffffffff;
        }

        /// <summary>
        /// Helper method to compute the Adler32 checksum.
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="offset">Offset in the data</param>
        /// <param name="len">Length to compute for</param>
        /// <param name="crc">Initial crc</param>
        /// <returns>The Adler32 checksum for the specified range</returns>
        public static uint CalcAdler32(ReadOnlySpan<Byte> data, int offset, int len, uint crc = 1)
        {
            var end = offset + Math.Max(0, Math.Min(data.Length - offset, len));
            var s1 = crc & 0xffff;
            var s2 = (crc >> 16) & 0xffff;
            while (offset < end)
            {
                var b = data[offset];
                s1 = (s1 + (uint)b) % 65521;
                s2 = (s2 + s1) % 65521;
            }
            return (s2 << 16) + s1;
        }

        /// <summary>
        /// Helper method to compute the Adler32 checksum.
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="crc">Initial crc</param>
        /// <returns>The Adler32 checksum for the data</returns>
        public static uint CalcAdler32(ReadOnlySpan<Byte> data, uint crc = 1)
        { 
            var s1 = crc & 0xffff;
            var s2 = (crc >> 16) & 0xffff;
            foreach (var b in data)
            {
                s1 = (s1 + (uint)b) % 65521;
                s2 = (s2 + s1) % 65521;
            }
            return (s2 << 16) + s1;
        }


        /// <summary>
        /// Helper method to make a new png file (as bytes) from some png chunks, but replacing the image data with manipulated IDAT data (good when loading / manipulating a file)
        /// </summary>
        /// <param name="chunks">Chunks (from an existing file)</param>
        /// <param name="newData">The new IDAT data, this is just the raw scanline data (including the filter byte)</param>
        /// <param name="a">Compression method</param>
        /// <param name="b">Compression type</param>
        /// <returns>Png file data</returns>
        public static Byte[] MakePng(IEnumerable<Chunk> chunks, ReadOnlySpan<Byte> newData, Byte a = 120, Byte b = 1)
        {
            using (var ms = new MemoryStream())
            {
                WriteChunks(ms, chunks, newData, a, b);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Helper method to make a new png file (as bytes) from some png chunks, but replacing the image data with manipulated IDAT chunk (good when loading / manipulating a file)
        /// </summary>
        /// <param name="chunks">Chunks (from an existing file)</param>
        /// <param name="newDataChunk">The new IDAT chunk</param>
        /// <returns>Png file data</returns>
        public static Byte[] MakePng(IEnumerable<Chunk> chunks, Chunk newDataChunk)
        {
            using (var ms = new MemoryStream())
            {
                WriteChunks(ms, chunks, newDataChunk);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Helper method to make a new png file (as bytes) from some png chunks
        /// </summary>
        /// <param name="chunks">Chunks (from an existing file)</param>
        /// <returns>Png file data</returns>
        public static Byte[] MakePng(IEnumerable<Chunk> chunks)
        {
            using (var ms = new MemoryStream())
            {
                WriteChunks(ms, chunks);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Helper method to write a new png file from some png chunks, but replacing the image data with manipulated IDAT data (good when loading / manipulating a file)
        /// </summary>
        /// <param name="pngFile">The destination stream</param>
        /// <param name="chunks">Chunks (from an existing file)</param>
        /// <param name="newData">The new IDAT data, this is just the raw scanline data (including the filter byte)</param>
        /// <param name="a">Compression method</param>
        /// <param name="b">Compression type</param>
        /// <param name="comp">Compression level</param>
        public static void WriteChunks(Stream pngFile, IEnumerable<Chunk> chunks, ReadOnlySpan<Byte> newData, Byte a = 120, Byte b = 1, CompressionLevel comp = CompressionLevel.NoCompression)
        {
            var imageDataCrc = Png.CalcAdler32(newData);
            using (var ms = new MemoryStream(newData.Length + 4096))
            {
                ms.WriteByte(a);
                ms.WriteByte(b);
                using (var cms = new DeflateStream(ms, comp, true))
                    cms.Write(newData);
                var c0 = (Byte)(imageDataCrc >> 24);
                var c1 = (Byte)(imageDataCrc >> 16);
                var c2 = (Byte)(imageDataCrc >> 8);
                var c3 = (Byte)(imageDataCrc >> 0);
                ms.WriteByte(c0);
                ms.WriteByte(c1);
                ms.WriteByte(c2);
                ms.WriteByte(c3);
                ms.Flush();
                var dta = ms.ToArray();
                WriteChunks(pngFile, chunks, new Png.Chunk((uint)dta.LongLength, Png.Id_IDAT, dta, 0));
            }
        }

        /// <summary>
        /// Helper method to write a new png file from some png chunks, but replacing the image data with manipulated IDAT chunk (good when loading / manipulating a file)
        /// </summary>
        /// <param name="pngFile">The destination stream</param>
        /// <param name="chunks">Chunks (from an existing file)</param>
        /// <param name="newDataChunk">The new IDAT chunk</param>
       public static void WriteChunks(Stream pngFile, IEnumerable<Chunk> chunks, Chunk newDataChunk)
        {
            WriteChunks(pngFile, chunks.Where(x => (x.Id != Png.Id_IDAT) || (newDataChunk != null)).Select(x =>
            {
                if (x.Id != Png.Id_IDAT)
                    return x;
                var t = newDataChunk;
                newDataChunk = null;
                return t;
            }));
        }


        /// <summary>
        /// Helper method to write a new png file (as bytes) from some png chunks
        /// </summary>
        /// <param name="pngFile">The destination stream</param>
        /// <param name="chunks">Chunks (from an existing file)</param>
        public static void WriteChunks(Stream pngFile, IEnumerable<Chunk> chunks)
        {
            pngFile.Write(Header, 0, 8);
            Byte[] w = new byte[4];
            foreach (var c in chunks)
            {
                var data = c.Data;
                var len = data.Length;
                var d = (uint)len;
                w[0] = (Byte)(d >> 24);
                w[1] = (Byte)(d >> 16);
                w[2] = (Byte)(d >> 8);
                w[3] = (Byte)(d >> 0);
                pngFile.Write(w, 0, 4);
                d = c.Id;
                w[0] = (Byte)(d >> 24);
                w[1] = (Byte)(d >> 16);
                w[2] = (Byte)(d >> 8);
                w[3] = (Byte)(d >> 0);
                pngFile.Write(w, 0, 4);
                if (len > 0)
                    pngFile.Write(data, 0, len);
                var crc = CalcCrc32(w, 0);
                d = CalcCrc32(data, crc);
                w[0] = (Byte)(d >> 24);
                w[1] = (Byte)(d >> 16);
                w[2] = (Byte)(d >> 8);
                w[3] = (Byte)(d >> 0);
                pngFile.Write(w, 0, 4);
            }
        }

        /// <summary>
        /// Read an png file into bytes
        /// </summary>
        /// <param name="width">The png image width</param>
        /// <param name="height">The png image height</param>
        /// <param name="pngFile">The stream containg the png image</param>
        /// <returns>The bytes in Blue, Green, Red, Alpha order</returns>
        public static Byte[] DecodeArgb(out int width, out int height, Stream pngFile)
        {
            width = 0;
            height = 0;
            var image = new Image();
            using var r = EndianAwareBinaryReader.OpenBigEndian(pngFile, Encoding.UTF8);
            //  Validate header
            for (int i = 0; i < 8; ++i)
            {
                if (Header[i] != r.ReadByte())
                    throw new IOException("Invalid PNG stream, header no valid!");
            }
            //  Iterate over hunks
            for (;;)
            {

                uint length = r.ReadUInt32();
                uint hunkId = r.ReadUInt32();
                var pos = pngFile.Position;
                var endPos = pos += length;
                ChunkProcessor p;
                if (Chunks.TryGetValue(hunkId, out p))
                {
                    if (!p(r, image, length))
                        break;
                }
                else
                {
                    while (length > 8)
                    {
                        r.ReadUInt64();
                        length -= 8;
                    }
                    while (length > 0)
                    {
                        r.ReadByte();
                        --length;
                    }
                }
                if (pngFile.Position != endPos)
                    throw new IOException("Invalid PNG stream, declared chunk size doesn't match the parsed chunk size!");
                uint crc = r.ReadUInt32();
            }
            width = image.Width;
            height = image.Height;
            return image.ImageData;
        }

        public const int Red = 2;
        public const int Green = 1;
        public const int Blue = 0;
        public const int Alpha = 3;

        public const Byte Grayscale = 0;
        public const Byte Rgb = 2;
        public const Byte Indexed = 3;
        public const Byte GrayscaleAlpha = 4;
        public const Byte Rgba = 6;

        public class ImageInfo
        {
            public int Width;
            public int Height;
            public Byte BitDepth;
            public Byte ColorType;
            public Byte CompressionMethod;
            public Byte FilterMethod;
            public Byte InterlaceMethodMethod;
            public Byte ChannelCount;
            public int Pitch;
            public int PixelBytes;
        }

        /// <summary>
        /// Decodes the information in the IHDR chunk
        /// </summary>
        /// <param name="ihdr">The IHDR chunk</param>
        /// <returns>The decoded image information</returns>
        public static ImageInfo DecodeIHDR(Chunk ihdr)
        {
            var image = new ImageInfo();
            using var ms = new MemoryStream(ihdr.Data, false);
            using var r = EndianAwareBinaryReader.OpenBigEndian(ms);
            image.Width = r.ReadInt32();
            image.Height = r.ReadInt32();
            image.BitDepth = r.ReadByte();
            image.ColorType = r.ReadByte();
            image.CompressionMethod = r.ReadByte();
            image.FilterMethod = r.ReadByte();
            image.InterlaceMethodMethod = r.ReadByte();
            if (image.ColorType >= ChannelCounts.Length)
                throw new IOException("Invalid PNG stream, color type " + image.ColorType + " is unknown!");
            image.ChannelCount = ChannelCounts[image.ColorType];
            image.PixelBytes = (((int)image.BitDepth * image.ChannelCount) + 7) >> 3;
            image.Pitch = (int)(((ulong)image.Width * image.BitDepth * image.ChannelCount + 7) >> 3);
            return image;
        }

        /// <summary>
        /// Get the filters used (one per row)
        /// </summary>
        /// <param name="s">Stream containing the raw image data, including the filter byte</param>
        /// <param name="info">Information about the image</param>
        /// <param name="leaveOpen">True to leave the input stream open</param>
        /// <returns>An array with the filter used, one per row</returns>
        public static Byte[] GetFilters(Stream s, ImageInfo info, bool leaveOpen = false)
        {
            var h = info.Height;
            var skip = info.Pitch;
            Byte[] temp = null;
            if (!s.CanSeek)
                temp = new byte[skip];
            var dest = new Byte[h];
            using (leaveOpen ? null : s)
            {
                for (int i = 0; i < h; ++ i)
                {
                    dest[i] = (Byte)s.ReadByte();
                    if (temp == null)
                        s.Position += skip;
                    else
                        s.ReadExactly(temp, 0, skip);
                }
            }
            return dest;
        }

        /// <summary>
        /// Get the filters used (one per row)
        /// </summary>
        /// <param name="imageData">Data containing raw image data including the filter byte</param>
        /// <param name="info">Information about the image</param>
        /// <param name="offset">Offset into the data for the first filter byte</param>
        /// <returns>An array with the filter used, one per row</returns>
        public static Byte[] GetFilters(Byte[] imageData, ImageInfo info, int offset = 0)
        {
            var pitch = info.Pitch;
            var pitchFilter = info.Pitch + 1;
            return Enumerable.Range(0, info.Height).Select(x => imageData[offset + pitchFilter * x]).ToArray();
        }

        /// <summary>
        /// Takes some filtered raw data (inlcuding the filter byte) and decodes it (make it all use filter 0, none).
        /// </summary>
        /// <param name="stream">The raw image data (first) byte is the filter of the first row</param>
        /// <param name="info">Information about the image</param>
        /// <param name="leaveOpen">True to leave the input stream open</param>
        /// <param name="keepFilterByte">If true, the original filter byte is kept</param>
        /// <returns>Unfiltered raw image data (including the filter byte)</returns>
        public static Byte[] UnfilterData(Stream stream, ImageInfo info, bool leaveOpen = true, bool keepFilterByte = false)
        {
            using (leaveOpen ? stream : null)
            {
                var pitch = info.Pitch;
                var pitchFilter = info.Pitch + 1;
                var maxP = pitchFilter * info.Height;
                var data = new Byte[maxP];
                var image = new Image();
                var pb = info.PixelBytes;
                image.PrevUnfilteredScanline = new byte[pitch + pb];
                image.UncompressedScanline = new byte[pitch + pb];
                image.UnfilteredScanline = new byte[pitch + pb];
                image.PixelBytes = info.PixelBytes;
                image.Pitch = pitch;
                //  Process scanlines
                int p = 0;
                for (; ; )
                {
                    var filter = stream.ReadByte();
                    if (filter < 0)
                        break;
                    //  Decompress
                    stream.ReadExactly(image.UncompressedScanline, pb, pitch);
                    //if (read != pitch)
                        //throw new IOException("Invalid PNG stream, the pitch of the decoded scan line doesn't match the width specified in the header!");
                    //  Filter
                    if ((filter >= 0) && (filter <= Filters.Length))
                        Filters[filter](image);
                    //  Decode
                    if (p >= maxP)
                        throw new IOException("Invalid PNG stream, data contains more scanlines than specified in the header!");
                    data[p] = (Byte)filter;
                    Array.Copy(image.UnfilteredScanline, pb, data, p + 1, pitch);
                    p += pitchFilter;
                    //  Swap scanlines
                    var temp = image.UnfilteredScanline;
                    image.UnfilteredScanline = image.PrevUnfilteredScanline;
                    image.PrevUnfilteredScanline = temp;
                }
                if (p < maxP)
                    throw new IOException("Invalid PNG stream, data contains less scanlines than specified in the header!");
                return data;
            }
        }

        /// <summary>
        /// Decompresses the data found in the IDAT header and unfilters it
        /// </summary>
        /// <param name="stream">The data found in an IDAT header without the header (2 bytes) and footer (4 bytes)</param>
        /// <param name="info">Information about the image</param>
        /// <param name="leaveOpen">True to leave the input stream open</param>
        /// <param name="keepFilterByte">If true, the original filter byte is kept</param>
        /// <returns>Unfiltered raw image data (including the filter byte)</returns>
        public static Byte[] DecodeAndUnfilterData(Stream stream, ImageInfo info, bool leaveOpen = true, bool keepFilterByte = false)
        {
            using var g = new DeflateStream(stream, CompressionMode.Decompress, leaveOpen);
            return UnfilterData(g, info, true, keepFilterByte);
        }

        /// <summary>
        /// Decompresses the data found in the IDAT header and unfilters it
        /// </summary>
        /// <param name="idat">The data found in an IDAT header</param>
        /// <param name="info">Information about the image</param>
        /// <param name="leaveOpen">True to leave the input stream open</param>
        /// <returns>Unfiltered raw image data (including the filter byte)</returns>
        public static Byte[] DecodeAndUnfilterIDAT(Stream idat, ImageInfo info, bool leaveOpen = true)
        {
            using (leaveOpen ? idat : null)
            {
                if (idat.ReadByte() < 0)
                    throw new Exception("Invalid IDAT data!");
                if (idat.ReadByte() < 0)
                    throw new Exception("Invalid IDAT data!");
                var len = idat.Length - idat.Position - 4;
                if (len < 0)
                    throw new Exception("Invalid IDAT data!");
                return DecodeAndUnfilterData(new LengthLimitedStream(idat, len), info, false);
            }
        }

        #region Implementation

        sealed class Image : ImageInfo
        {
            public uint Position;
            public uint[] Palette;
            public Byte[] UncompressedScanline;
            public Byte[] UnfilteredScanline;
            public Byte[] PrevUnfilteredScanline;
            public Byte[] ImageData;
            public int ImageDataOffset;
            public UInt16[] ColorTrans;
            public Byte[] IndexedTrans;
        }


        #region Chunks

        /// <summary>
        /// Convert a chunk name as a string to the 32 bit value
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static uint Get(String s)
        {
            uint u = 0;
            u |= (Byte)s[0];
            u <<= 8;
            u |= (Byte)s[1];
            u <<= 8;
            u |= (Byte)s[2];
            u <<= 8;
            u |= (Byte)s[3];
            return u;
        }

        /// <summary>
        /// IHDR chunk as a 32-bit integer
        /// </summary>
        public static readonly uint Id_IHDR = Get("IHDR");
        /// <summary>
        /// PLTE chunk as a 32-bit integer
        /// </summary>
        public static readonly uint Id_PLTE = Get("PLTE");
        /// <summary>
        /// IDAT chunk as a 32-bit integer
        /// </summary>
        public static readonly uint Id_IDAT = Get("IDAT");
        /// <summary>
        /// IEND chunk as a 32-bit integer
        /// </summary>
        public static readonly uint Id_IEND = Get("IEND");
        /// <summary>
        /// tRNS chunk as a 32-bit integer
        /// </summary>
        public static readonly uint Id_tRNS = Get("tRNS");

        delegate bool ChunkProcessor(BinaryReader r, Image image, uint chunkLength);

        static readonly Dictionary<uint, ChunkProcessor> Chunks = new Dictionary<uint, ChunkProcessor>()
        {
            { Id_IHDR,  IHDR},
            { Id_PLTE,  PLTE},
            { Id_IDAT,  IDAT},
            { Id_IEND,  IEND},
            { Id_tRNS,  tRNS },

        };

        #region IHDR

        static bool tRNS(BinaryReader r, Image image, uint chunkLength)
        {
            switch (image.ColorType)
            {
                case 0:
                    image.ColorTrans = new ushort[1];
                    image.ColorTrans[0] = r.ReadUInt16();
                    return true;
                case 2:
                    image.ColorTrans = new ushort[3];
                    image.ColorTrans[0] = r.ReadUInt16();
                    image.ColorTrans[1] = r.ReadUInt16();
                    image.ColorTrans[2] = r.ReadUInt16();
                    return true;
                case 3:
                    image.IndexedTrans = r.ReadBytes((int)chunkLength);
                    return true;
            }
            r.ReadBytes((int)chunkLength);
            return true;
        }

        static readonly Byte[] ChannelCounts = new byte[] { 1, 0, 3, 1, 2, 0, 4 };

        static bool IHDR(BinaryReader r, Image image, uint chunkLength)
        {
            image.Width = r.ReadInt32();
            image.Height = r.ReadInt32();
            image.BitDepth = r.ReadByte();
            image.ColorType = r.ReadByte();
            image.CompressionMethod = r.ReadByte();
            image.FilterMethod = r.ReadByte();
            image.InterlaceMethodMethod = r.ReadByte();
            if (image.ColorType >= ChannelCounts.Length)
                throw new IOException("Invalid PNG stream, color type " + image.ColorType + " is unknown!");
            image.ChannelCount = ChannelCounts[image.ColorType];
            image.PixelBytes = (((int)image.BitDepth * image.ChannelCount) + 7) >> 3;
            image.Pitch = (int)(((ulong)image.Width * image.BitDepth * image.ChannelCount + 7) >> 3);
            image.UncompressedScanline = new byte[image.Pitch + image.PixelBytes];
            image.UnfilteredScanline = new byte[image.Pitch + image.PixelBytes];
            image.PrevUnfilteredScanline = new byte[image.Pitch + image.PixelBytes];
            image.ImageDataOffset = 0;
            return true;
        }

        #endregion//IHDR

        #region PLTE

        static bool PLTE(BinaryReader r, Image image, uint chunkLength)
        {
            int count = (int)(chunkLength / 3);
            image.Palette = new uint[count];
            for (int i = 0; i < count; ++i)
            {
                uint c = r.ReadByte();
                c <<= 8;
                c |= r.ReadByte();
                c <<= 8;
                c |= r.ReadByte();
                image.Palette[i] = c | 0xff000000u;
            }
            return true;
        }

        #endregion//PLTE

        #region IDAT

        static bool IDAT(BinaryReader r, Image image, uint chunkLength)
        {
            bool first = image.ImageData == null;
            if (first)
                image.ImageData = new Byte[r.BaseStream.Length - r.BaseStream.Position];
            int size = (int)chunkLength;
            r.Read(image.ImageData, image.ImageDataOffset, size);
            image.ImageDataOffset += size;
            return true;
        }

        #endregion//IDAT

        #region IEND

        static bool IEND(BinaryReader r, Image image, uint chunkLength)
        {
            if (image.ImageData == null)
                return false;
            Action<Image> decoder;
            if (!Decoders.TryGetValue(Tuple.Create((int)image.ColorType, (int)image.BitDepth), out decoder))
                throw new IOException("Invalid PNG stream, the combination of color type " + image.ColorType + " and bitdepth " + image.BitDepth + " is invalid!");
            using (var g = new DeflateStream(new MemoryStream(image.ImageData, 2, image.ImageDataOffset - 6), CompressionMode.Decompress, false))
            {
                int maxP = image.Width * image.Height * 4;
                image.ImageData = new Byte[maxP];
                //  Process scanlines
                for (;;)
                {
                    var filter = g.ReadByte();
                    if (filter < 0)
                        break;
                    //  Decompress
                    var read = g.Read(image.UncompressedScanline, image.PixelBytes, image.Pitch);
                    if (read != image.Pitch)
                        throw new IOException("Invalid PNG stream, the pitch of the decoded scan line doesn't match the width specified in the header!");
                    //  Filter
                    if ((filter >= 0) && (filter <= Filters.Length))
                        Filters[filter](image);
                    //  Decode
                    if (image.Position >= maxP)
                        throw new IOException("Invalid PNG stream, data contains more scanlines than specified in the header!");
                    decoder(image);
                    //  Swap scanlines
                    var temp = image.UnfilteredScanline;
                    image.UnfilteredScanline = image.PrevUnfilteredScanline;
                    image.PrevUnfilteredScanline = temp;
                }
                if (image.Position < maxP)
                    throw new IOException("Invalid PNG stream, data contains less scanlines than specified in the header!");
            }
            return false;
        }

        #endregion//IEND


        #endregion//Chunks

        #region Filters

        public const int FilterIndexNone = 0;
        public const int FilterIndexSub = 1;
        public const int FilterIndexUp = 2;
        public const int FilterIndexAverage = 3;
        public const int FilterIndexPaeth = 4;

        /// <summary>
        /// Applying a png filter to a scanline
        /// </summary>
        /// <param name="dest">The destination buffer</param>
        /// <param name="destOffset">The offset to write the filtered scanline, starting with the filter byte</param>
        /// <param name="bytesPerRow">Number of bytes per row, including the filter byte</param>
        /// <param name="src">The source bytes</param>
        /// <param name="srcOffset">The offset to read the first source byte of the unfiltered scanline (do not point to any filter byte, none is required)</param>
        /// <param name="srcPitch">The number of bytes from one source scanline to the next</param>
        /// <param name="y">The current row, used to determine special cases for the first scanline</param>
        /// <param name="filter">[0, 4] The filter to apply: 0:None, 1:Sub, 2:Up, 3:Average, 4:Paeth</param>
        /// <param name="bytesPerPixel">Bytes per pixel, </param>
        public static void ApplyFilterToScanline(Byte[] dest, int destOffset, int bytesPerRow, Byte[] src, int srcOffset, int srcPitch, int y, int filter, int bytesPerPixel)
        {
            dest[destOffset] = (Byte)filter;
            ++destOffset;
            var pixelBytes = bytesPerRow - 1;
            switch (filter)
            {
                //  None
                case 0:
                    Buffer.BlockCopy(src, srcOffset, dest, destOffset, pixelBytes);
                    break;
                //  Sub
                case 1:
                    {
                        for (int i = 0; i < pixelBytes; ++i)
                        {
                            var di = i - bytesPerPixel;
                            var prev = di < 0 ? 0 : src[di + srcOffset];
                            var n = src[i + srcOffset];
                            dest[destOffset] = (Byte)(n - prev);
                            ++destOffset;
                        }
                    }
                    break;
                //  Up
                case 2:
                    if (y == 0)
                    {
                        Buffer.BlockCopy(src, srcOffset, dest, destOffset, pixelBytes);
                        break;
                    }
                    for (int i = 0; i < pixelBytes; ++i)
                    {
                        var prevY = src[srcOffset - srcPitch];
                        var n = src[srcOffset];
                        dest[destOffset] = (Byte)(n - prevY);
                        ++destOffset;
                        ++srcOffset;
                    }
                    break;
                // Average
                case 3:
                    if (y == 0)
                    {
                        for (int i = 0; i < pixelBytes; ++i)
                        {
                            var di = i - bytesPerPixel;
                            var prevX = di < 0 ? 0 : src[srcOffset + di];
                            var prev = prevX >> 1;
                            var n = src[srcOffset + i];
                            dest[destOffset] = (Byte)(n - prev);
                            ++destOffset;
                        }
                        break;
                    }
                    for (int i = 0; i < pixelBytes; ++i)
                    {
                        var di = i - bytesPerPixel;
                        var prevX = di < 0 ? 0 : src[srcOffset + di];
                        var prevY = src[srcOffset - srcPitch + i];
                        var prev = (prevX + prevY) >> 1;
                        var n = src[srcOffset + i];
                        dest[destOffset] = (Byte)(n - prev);
                        ++destOffset;
                    }
                    break;
                //  Paeth
                case 4:
                    for (int i = 0; i < pixelBytes; ++i)
                    {
                        var di = i - bytesPerPixel;
                        int a = di < 0 ? 0 : src[srcOffset + di];
                        int b, c;
                        if (y <= 0)
                        {
                            b = 0;
                            c = 0;
                        }else
                        {
                            b = src[srcOffset - srcPitch + i];
                            c = di < 0 ? 0 : src[srcOffset - srcPitch + di];
                        }
                        var p = a + b - c;
                        var pa = p - a;
                        var pb = p - b;
                        var pc = p - c;
                        if (pa < 0)
                            pa = -pa;
                        if (pb < 0)
                            pb = -pb;
                        if (pc < 0)
                            pc = -pc;
                        int prev;
                        if (pa <= pb)
                        {
                            if (pa <= pc)
                            {
                                prev = a;
                            }else
                            {
                                prev = c;
                            }
                        }else
                        {
                            if (pb <= pc)
                            {
                                prev = b;
                            }else
                            {
                                prev = c;
                            }
                        }
                        var n = src[srcOffset + i];
                        dest[destOffset] = (Byte)(n - prev);
                        ++destOffset;
                    }
                    break;
                default:
                    throw new Exception("Invalid filter!");
            }
        }

        static void FilterNone(Image image)
        {
            var s = image.UncompressedScanline;
            var d = image.UnfilteredScanline;
            var o = image.PixelBytes;
            var e = image.Pitch + o;
            for (int i = o; i < e; ++i)
                d[i] = s[i];
        }
        static void FilterSub(Image image)
        {
            var s = image.UncompressedScanline;
            var d = image.UnfilteredScanline;
            var o = image.PixelBytes;
            var e = image.Pitch + o;
            for (int i = o; i < e; ++i)
                d[i] = (Byte)(s[i] + d[i - o]);
        }
        static void FilterUp(Image image)
        {
            var s = image.UncompressedScanline;
            var d = image.UnfilteredScanline;
            var p = image.PrevUnfilteredScanline;
            var o = image.PixelBytes;
            var e = image.Pitch + o;
            for (int i = o; i < e; ++i)
                d[i] = (Byte)(s[i] + p[i]);
        }
        static void FilterAverage(Image image)
        {
            var s = image.UncompressedScanline;
            var d = image.UnfilteredScanline;
            var p = image.PrevUnfilteredScanline;
            var o = image.PixelBytes;
            var e = image.Pitch + o;
            for (int i = o; i < e; ++i)
                d[i] = (Byte)(s[i] + ((p[i] + d[i - o]) >> 1));
        }
        static void FilterPaeth(Image image)
        {
            var s = image.UncompressedScanline;
            var d = image.UnfilteredScanline;
            var p = image.PrevUnfilteredScanline;
            var o = image.PixelBytes;
            var e = image.Pitch + o;
            for (int i = o; i < e; ++i)
                d[i] = (Byte)(s[i] + Paeth(d[i - o], p[i], p[i - o]));
        }

        public static Byte Paeth(Byte a, Byte b, Byte c)
        {
            var p = a + b - c;
            var pa = p - a;
            var pb = p - b;
            var pc = p - c;
            var ma = pa >> (sizeof(int) * 8 - 1);
            var mb = pb >> (sizeof(int) * 8 - 1);
            var mc = pc >> (sizeof(int) * 8 - 1);
            pa += ma;
            pb += mb;
            pc += mc;
            pa ^= ma;
            pb ^= mb;
            pc ^= mc;
            Byte pr;
            if ((pa <= pb) && (pa <= pc))
            {
                pr = a;
            }
            else
            {
                if (pb <= pc)
                    pr = b;
                else
                    pr = c;
            }
            return pr;
        }

        static readonly Action<Image>[] Filters = new Action<Image>[]
        {
            FilterNone, FilterSub, FilterUp, FilterAverage, FilterPaeth

        };

        #endregion//Filters

        #region Color decoders


        static readonly Byte[] Bpp1 = { 0, 0xff };
        static readonly Byte[] Bpp2 = { 0,
                                                    (Byte)(((1 * 255) + 1) / 3),
                                                    (Byte)(((2 * 255) + 1) / 3),
                                                0xff };
        static readonly Byte[] Bpp4 = { 0,
                                                    (Byte)(((1 * 255) + 7) / 15),
                                                    (Byte)(((2 * 255) + 7) / 15),
                                                    (Byte)(((3 * 255) + 7) / 15),
                                                    (Byte)(((4 * 255) + 7) / 15),
                                                    (Byte)(((5 * 255) + 7) / 15),
                                                    (Byte)(((6 * 255) + 7) / 15),
                                                    (Byte)(((7 * 255) + 7) / 15),
                                                    (Byte)(((8 * 255) + 7) / 15),
                                                    (Byte)(((9 * 255) + 7) / 15),
                                                    (Byte)(((10 * 255) + 7) / 15),
                                                    (Byte)(((11 * 255) + 7) / 15),
                                                    (Byte)(((12 * 255) + 7) / 15),
                                                    (Byte)(((13 * 255) + 7) / 15),
                                                    (Byte)(((14 * 255) + 7) / 15),
                                                0xff };


        static readonly Dictionary<Tuple<int, int>, Action<Image>> Decoders = new Dictionary<Tuple<int, int>, Action<Image>>()
        {
            { Tuple.Create(0, 1), Decode_Greyscale_1 },
            { Tuple.Create(0, 2), Decode_Greyscale_2 },
            { Tuple.Create(0, 4), Decode_Greyscale_4 },
            { Tuple.Create(0, 8), Decode_Greyscale_8 },
            { Tuple.Create(0, 16), Decode_Greyscale_16 },

            { Tuple.Create(2, 8), Decode_Rgb_8 },
            { Tuple.Create(2, 16), Decode_Rgb_16 },

            { Tuple.Create(3, 1), Decode_Index_1 },
            { Tuple.Create(3, 2), Decode_Index_2 },
            { Tuple.Create(3, 4), Decode_Index_4 },
            { Tuple.Create(3, 8), Decode_Index_8 },

            { Tuple.Create(4, 8), Decode_GreyscaleAlpha_8 },
            { Tuple.Create(4, 16), Decode_GreyscaleAlpha_16 },

            { Tuple.Create(6, 8), Decode_Rgba_8 },
            { Tuple.Create(6, 16), Decode_Rgba_16 },

        };

        static void Decode_Greyscale_1(Image image)
        {
            uint alphaMask = image.ColorTrans == null ? 0xffffffffU : (uint)image.ColorTrans[0];
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            Byte data = 0;
            for (int i = 0; i < w; ++i, p += 4, data <<= 1)
            {
                var ix = i & 7;
                if (ix == 0)
                {
                    data = s[o];
                    ++o;
                }
                var index = data >> 7;
                Byte t = Bpp1[index];
                var a = (Byte)0xff;
                if (((uint)index) == alphaMask)
                {
                    a = 0;
                    t = 0;
                }
                d[p + Alpha] = a;
                d[p + Red] = t;
                d[p + Green] = t;
                d[p + Blue] = t;
            }
            image.Position = p;
        }

        static void Decode_Greyscale_2(Image image)
        {
            uint alphaMask = image.ColorTrans == null ? 0xffffffffU : (uint)image.ColorTrans[0];
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            Byte data = 0;
            for (int i = 0; i < w; ++i, p += 4, data <<= 2)
            {
                var ix = i & 3;
                if (ix == 0)
                {
                    data = image.UnfilteredScanline[o];
                    ++o;
                }
                var index = data >> 6;
                Byte t = Bpp2[index];
                var a = (Byte)0xff;
                if (((uint)index) == alphaMask)
                {
                    a = 0;
                    t = 0;
                }
                d[p + Alpha] = a;
                d[p + Red] = t;
                d[p + Green] = t;
                d[p + Blue] = t;
            }
            image.Position = p;
        }

        static void Decode_Greyscale_4(Image image)
        {
            uint alphaMask = image.ColorTrans == null ? 0xffffffffU : (uint)image.ColorTrans[0];
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            Byte data = 0;
            for (int i = 0; i < w; ++i, p += 4, data <<= 4)
            {
                var ix = i & 1;
                if (ix == 0)
                {
                    data = image.UnfilteredScanline[o];
                    ++o;
                }
                var index = data >> 4;
                Byte t = Bpp4[index];
                var a = (Byte)0xff;
                if (((uint)index) == alphaMask)
                {
                    a = 0;
                    t = 0;
                }
                d[p + Alpha] = a;
                d[p + Red] = t;
                d[p + Green] = t;
                d[p + Blue] = t;
            }
            image.Position = p;
        }

        static void Decode_Greyscale_8(Image image)
        {
            uint alphaMask = image.ColorTrans == null ? 0xffffffffU : (uint)image.ColorTrans[0];
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            for (int i = 0; i < w; ++i, p += 4)
            {
                var data = s[o];
                ++o;
                var a = (Byte)0xff;
                if (((uint)data) == alphaMask)
                {
                    a = 0;
                    data = 0;
                }
                d[p + Alpha] = a;
                d[p + Red] = data;
                d[p + Green] = data;
                d[p + Blue] = data;
            }
            image.Position = p;
        }

        static void Decode_Greyscale_16(Image image)
        {
            uint alphaMask = image.ColorTrans == null ? 0xffffffffU : (uint)image.ColorTrans[0];
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            for (int i = 0; i < w; ++i, p += 4)
            {
                var data = s[o];
                var t = (((uint)data) << 8) | (s[o + 1]);
                o += 2;
                var a = (Byte)0xff;
                if (((uint)t) == alphaMask)
                {
                    a = 0;
                    data = 0;
                }
                d[p + Alpha] = a;
                d[p + Red] = data;
                d[p + Green] = data;
                d[p + Blue] = data;
            }
            image.Position = p;
        }

        static void Decode_Rgb_8(Image image)
        {
            bool masked = image.ColorTrans != null;
            uint alphaMaskR = masked ? (uint)image.ColorTrans[0] : 0xffffffffU;
            uint alphaMaskG = masked ? (uint)image.ColorTrans[1] : 0xffffffffU;
            uint alphaMaskB = masked ? (uint)image.ColorTrans[2] : 0xffffffffU;
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            for (int i = 0; i < w; ++i, p += 4)
            {
                var r = s[o];
                var g = s[o + 1];
                var b = s[o + 2];
                var a = (Byte)0xff;
                if (masked && (r == alphaMaskR) && (g == alphaMaskG) && (b == alphaMaskB))
                    a = 0;
                d[p + Alpha] = a;
                d[p + Red] = r;
                d[p + Green] = g;
                d[p + Blue] = b;
                o += 3;
            }
            image.Position = p;
        }

        static void Decode_Rgb_16(Image image)
        {
            bool masked = image.ColorTrans != null;
            uint alphaMaskR = masked ? (uint)image.ColorTrans[0] : 0xffffffffU;
            uint alphaMaskG = masked ? (uint)image.ColorTrans[1] : 0xffffffffU;
            uint alphaMaskB = masked ? (uint)image.ColorTrans[2] : 0xffffffffU;
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            for (int i = 0; i < w; ++i, p += 4)
            {
                var r = s[o];
                var g = s[o + 2];
                var b = s[o + 4];
                var a = (Byte)0xff;
                if (masked)
                {
                    var rr = (((uint)r) << 8) | s[o + 1];
                    var gg = (((uint)g) << 8) | s[o + 3];
                    var bb = (((uint)b) << 8) | s[o + 5];
                    if ((rr == alphaMaskR) && (gg == alphaMaskG) && (bb == alphaMaskB))
                        a = 0;
                }
                d[p + Alpha] = a;
                d[p + Red] = r;
                d[p + Green] = g;
                d[p + Blue] = b;
                o += 6;
            }
            image.Position = p;
        }

        static void Decode_Index_1(Image image)
        {
            var alpha = image.IndexedTrans;
            var alphaLen = alpha?.Length ?? 0;
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            var pl = image.Palette;
            Byte data = 0;
            for (int i = 0; i < w; ++i, p += 4, data <<= 1)
            {
                var ix = i & 7;
                if (ix == 0)
                {
                    data = s[o];
                    ++o;
                }
                var index = data >> 7;
                var t = pl[index];
                d[p + Alpha] = ((alpha != null) && (index < alphaLen)) ? alpha[index] : (Byte)0xff;
                d[p + Red] = (Byte)(t >> 16);
                d[p + Green] = (Byte)(t >> 8);
                d[p + Blue] = (Byte)(t >> 0);
            }
            image.Position = p;
        }

        static void Decode_Index_2(Image image)
        {
            var alpha = image.IndexedTrans;
            var alphaLen = alpha?.Length ?? 0;
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            var pl = image.Palette;
            Byte data = 0;
            for (int i = 0; i < w; ++i, p += 4, data <<= 2)
            {
                var ix = i & 3;
                if (ix == 0)
                {
                    data = s[o];
                    ++o;
                }
                var index = data >> 6;
                var t = pl[index];
                d[p + Alpha] = ((alpha != null) && (index < alphaLen)) ? alpha[index] : (Byte)0xff;
                d[p + Red] = (Byte)(t >> 16);
                d[p + Green] = (Byte)(t >> 8);
                d[p + Blue] = (Byte)(t >> 0);
            }
            image.Position = p;
        }

        static void Decode_Index_4(Image image)
        {
            var alpha = image.IndexedTrans;
            var alphaLen = alpha?.Length ?? 0;
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            var pl = image.Palette;
            Byte data = 0;
            for (int i = 0; i < w; ++i, p += 4, data <<= 4)
            {
                var ix = i & 1;
                if (ix == 0)
                {
                    data = s[o];
                    ++o;
                }
                var index = data >> 4;
                var t = pl[index];
                d[p + Alpha] = ((alpha != null) && (index < alphaLen)) ? alpha[index] : (Byte)0xff;
                d[p + Red] = (Byte)(t >> 16);
                d[p + Green] = (Byte)(t >> 8);
                d[p + Blue] = (Byte)(t >> 0);
            }
            image.Position = p;
        }

        static void Decode_Index_8(Image image)
        {
            var alpha = image.IndexedTrans;
            var alphaLen = alpha?.Length ?? 0;
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            var pl = image.Palette;
            for (int i = 0; i < w; ++i, p += 4)
            {
                var index = s[o];
                var t = pl[index];
                ++o;
                d[p + Alpha] = ((alpha != null) && (index < alphaLen)) ? alpha[index] : (Byte)0xff;
                d[p + Red] = (Byte)(t >> 16);
                d[p + Green] = (Byte)(t >> 8);
                d[p + Blue] = (Byte)(t >> 0);
            }
            image.Position = p;
        }

        static void Decode_GreyscaleAlpha_8(Image image)
        {
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            for (int i = 0; i < w; ++i, p += 4)
            {
                var data = s[o];
                d[p + Alpha] = s[o + 1];
                d[p + Red] = data;
                d[p + Green] = data;
                d[p + Blue] = data;
                o += 2;
            }
            image.Position = p;
        }

        static void Decode_GreyscaleAlpha_16(Image image)
        {
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            for (int i = 0; i < w; ++i, p += 4)
            {
                var data = s[o];
                d[p + Alpha] = s[o + 2];
                d[p + Red] = data;
                d[p + Green] = data;
                d[p + Blue] = data;
                o += 4;
            }
            image.Position = p;
        }

        static void Decode_Rgba_8(Image image)
        {
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            for (int i = 0; i < w; ++i, p += 4)
            {
                d[p + Alpha] = s[o + 3];
                d[p + Red] = s[o];
                d[p + Green] = s[o + 1];
                d[p + Blue] = s[o + 2];
                o += 4;
            }
            image.Position = p;
        }

        static void Decode_Rgba_16(Image image)
        {
            var o = image.PixelBytes;
            var s = image.UnfilteredScanline;
            var p = image.Position;
            var d = image.ImageData;
            var w = image.Width;
            for (int i = 0; i < w; ++i, p += 4)
            {
                d[p + Alpha] = s[o + 6];
                d[p + Red] = s[o];
                d[p + Green] = s[o + 2];
                d[p + Blue] = s[o + 4];
                o += 8;
            }
            image.Position = p;
        }


        #endregion//Color decoders

        static readonly Byte[] Header = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

        #endregion//Implementation

    }

}
