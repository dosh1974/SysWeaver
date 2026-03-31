using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver
{


    public static class EncodingExt
    {

        static readonly SemiFrozenDictionary<Encoding, Byte[]> Preambles = new SemiFrozenDictionary<Encoding, byte[]>();

        /// <summary>
        /// Get a string from bytes without any Bom (pre-amble)
        /// </summary>
        /// <param name="encoding"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static String GetStringWithoutBom(this Encoding encoding, ReadOnlySpan<Byte> data)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;
            var pp = Preambles;
            if (!pp.TryGetValue(encoding, out var pre))
            {
                pre = encoding.GetPreamble();
                pp.TryAdd(encoding, pre);
            }
            if (pre != null)
            {
                var l = pre.Length;
                if (l <= data.Length)
                {
                    if (pre.SequenceEqual(data[..l]))
                        data = data[l..];
                }
            }
            return encoding.GetString(data);
        }
    }

    public static class StreamExt
    {

        /// <summary>
        /// Read all text of a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ReadAllText(this Stream stream, Encoding encoding = null, bool leaveOpen = false)
        {
            using var mem = ReadAllUnmanagedMemory(stream, leaveOpen);
            return encoding.GetStringWithoutBom(mem.Memory.Span);
        }

        /// <summary>
        /// Read all text of a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask<String> ReadAllTextAsync(this Stream stream, Encoding encoding = null, bool leaveOpen = false)
        {
            using var mem = await ReadAllUnmanagedMemoryAsync(stream, leaveOpen).ConfigureAwait(false);
            return encoding.GetStringWithoutBom(mem.Memory.Span);
        }



        /// <summary>
        /// Read all lines of text in a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask<String[]> ReadAllLinesAsync(this Stream stream, Encoding encoding = null, bool leaveOpen = false, bool trim = false, bool removeEmpty = false)
            => (await ReadAllTextAsync(stream, encoding, leaveOpen).ConfigureAwait(false)).GetLines(trim, removeEmpty);

        /// <summary>
        /// Read all lines of text in a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String[] ReadAllLines(this Stream stream, Encoding encoding = null, bool leaveOpen = false, bool trim = false, bool removeEmpty = false)
            => ReadAllText(stream, encoding, leaveOpen).GetLines(trim, removeEmpty);

        const int MaxBuf = 1 << 30;

        static int GetBufferSize(Stream stream)
        {
            try
            {
                if (stream.CanSeek)
                {
                    var l = stream.Length - stream.Position;
                    if (l > MaxBuf)
                        return MaxBuf;
                    return (int)l;
                }
            }
            catch
            {
            }
            return 65536;
        }


        public static async ValueTask<IUnmanagedReadOnlyMemory<Byte>> ReadAllUnmanagedMemoryAsync(this Stream stream, bool leaveOpen = false)
        {
            if (stream is FileStream fs)
                return await FileReadOnlyMemory.ReadAsync(fs, leaveOpen).ConfigureAwait(false);
            using var x = leaveOpen ? null : stream;
            using var ms = new ArrayPoolStream(GetBufferSize(stream));
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.GetMemory();
        }

        public static IUnmanagedReadOnlyMemory<Byte> ReadAllUnmanagedMemory(this Stream stream, bool leaveOpen = false)
        {
            if (stream is FileStream fs)
                return FileReadOnlyMemory.Read(fs, leaveOpen);
            using var x = leaveOpen ? null : stream;
            using var ms = new ArrayPoolStream(GetBufferSize(stream));
            stream.CopyTo(ms);
            return ms.GetMemory();
        }

        public static async ValueTask<ReadOnlyMemory<Byte>> ReadAllReadOnlyMemoryAsync(this Stream stream, bool leaveOpen = false)
        {
            if (stream is FileStream fs)
                return await FileReadOnlyMemory.ReadAllBytesAsync(fs, leaveOpen).ConfigureAwait(false);
            using var x = leaveOpen ? null : stream;
            using var ms = new ArrayPoolStream(GetBufferSize(stream));
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.GetBufferMemory();
        }


        public static async ValueTask<Memory<Byte>> ReadAllMemoryAsync(this Stream stream, bool leaveOpen = false)
        {
            if (stream is FileStream fs)
                return await FileReadOnlyMemory.ReadAllBytesAsync(fs, leaveOpen).ConfigureAwait(false);
            using var x = leaveOpen ? null : stream;
            using var ms = new ArrayPoolStream(GetBufferSize(stream));
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.GetBufferMemory();
        }

        public static async ValueTask<Byte[]> ReadAllBytesAsync(this Stream stream, bool leaveOpen = false)
        {
            if (stream is FileStream fs)
                return await FileReadOnlyMemory.ReadAllBytesAsync(fs, leaveOpen).ConfigureAwait(false);
            using var x = leaveOpen ? null : stream;
            using var ms = new ArrayPoolStream(GetBufferSize(stream));
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }

        public static Memory<Byte> ReadAllMemory(this Stream stream, bool leaveOpen = false)
        {
            if (stream is FileStream fs)
                return FileReadOnlyMemory.ReadAllBytes(fs, leaveOpen);
            using var x = leaveOpen ? null : stream;
            using var ms = new ArrayPoolStream(GetBufferSize(stream));
            stream.CopyTo(ms);
            return ms.GetBufferMemory();
        }

        public static Byte[] ReadAllBytes(this Stream stream, bool leaveOpen = false)
        {
            if (stream is FileStream fs)
                return FileReadOnlyMemory.ReadAllBytes(fs, leaveOpen);
            using var x = leaveOpen ? null : stream;
            using var ms = new ArrayPoolStream(GetBufferSize(stream));
            stream.CopyTo(ms);
            return ms.ToArray();
        }


    }

}
