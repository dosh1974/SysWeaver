using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Extension methods for text encodings
    /// </summary>
    public static class EncodingExt
    {

        /// <summary>
        /// Get a string from bytes without any Bom (pre-amble).
        /// If the data starts with the preamble of the encoding (the byte order mark), it is skipped (only once), the rest is decoded.
        /// </summary>
        /// <param name="encoding">The text encoding to use, null is UTF8 (that have a preamble)</param>
        /// <param name="data">The encoded text data, optionally starting with the preamble of the encoding</param>
        /// <returns>The decoded text (without the preamble)</returns>
        /// <remarks>
        /// The preamble of the encoding instance is used, so an encoding without a preamble (ex: new UTF8Encoding(false)) will keep any byte order mark in the text (as the char '﻿').
        /// Invalid data is decoded using the fallback of the encoding (it may throw a DecoderFallbackException for an encoding with an exception fallback).
        /// </remarks>
        /// <exception cref="DecoderFallbackException">The data is invalid and the encoding uses an exception fallback</exception>
        public static String GetStringWithoutBom(this Encoding encoding, ReadOnlySpan<Byte> data)
        {
            encoding ??= Encoding.UTF8;
            // The built in encodings return a static span (no allocation), the base Encoding returns GetPreamble() (empty for all built in encodings without a preamble)
            var pre = encoding.Preamble;
            if ((pre.Length > 0) && data.StartsWith(pre))
                data = data.Slice(pre.Length);
            return encoding.GetString(data);
        }
    }

    /// <summary>
    /// Extension methods for reading all (remaining) data of a stream.
    /// All methods reads from the current position of the stream until the end of the stream.
    /// FileStream's are read using FileReadOnlyMemory, other seekable streams are read directly into a buffer of the expected size (the remaining length).
    /// </summary>
    public static class StreamExt
    {

        /// <summary>
        /// Read all text of a stream.
        /// A byte order mark (the preamble of the encoding) at the start of the data is removed.
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns>The text (an empty string if the stream has no remaining data)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        public static String ReadAllText(this Stream stream, Encoding encoding = null, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
            {
                using var mem = FileReadOnlyMemory.Read(fs, leaveOpen);
                return encoding.GetStringWithoutBom(mem.Memory.Span);
            }
            using var x = leaveOpen ? null : stream;
            var buf = ReadAllPooled(stream, out var length);
            try
            {
                return encoding.GetStringWithoutBom(new ReadOnlySpan<Byte>(buf, 0, length));
            }
            finally
            {
                ArrayPoolStream.Return(buf);
            }
        }

        /// <summary>
        /// Read all text of a stream.
        /// A byte order mark (the preamble of the encoding) at the start of the data is removed.
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns>The text (an empty string if the stream has no remaining data)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        public static async Task<String> ReadAllTextAsync(this Stream stream, Encoding encoding = null, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
            {
                using var mem = await FileReadOnlyMemory.ReadAsync(fs, leaveOpen).ConfigureAwait(false);
                return encoding.GetStringWithoutBom(mem.Memory.Span);
            }
            using var x = leaveOpen ? null : stream;
            var (buf, length) = await ReadAllPooledAsync(stream).ConfigureAwait(false);
            try
            {
                return encoding.GetStringWithoutBom(new ReadOnlySpan<Byte>(buf, 0, length));
            }
            finally
            {
                ArrayPoolStream.Return(buf);
            }
        }



        /// <summary>
        /// Read all lines of text in a stream (see StringTools.GetLines).
        /// A byte order mark (the preamble of the encoding) at the start of the data is removed.
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns>The lines of text (an empty array if the stream has no remaining data)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        /// <remarks>The text is decoded into a temporary (stack or pooled) buffer, only the lines are allocated</remarks>
        public static async Task<String[]> ReadAllLinesAsync(this Stream stream, Encoding encoding = null, bool leaveOpen = false, bool trim = false, bool removeEmpty = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
            {
                using var mem = await FileReadOnlyMemory.ReadAsync(fs, leaveOpen).ConfigureAwait(false);
                return mem.Memory.Span.ToStringArray(encoding, trim, removeEmpty);
            }
            using var x = leaveOpen ? null : stream;
            var (buf, length) = await ReadAllPooledAsync(stream).ConfigureAwait(false);
            try
            {
                return new ReadOnlySpan<Byte>(buf, 0, length).ToStringArray(encoding, trim, removeEmpty);
            }
            finally
            {
                ArrayPoolStream.Return(buf);
            }
        }

        /// <summary>
        /// Read all lines of text in a stream (see StringTools.GetLines).
        /// A byte order mark (the preamble of the encoding) at the start of the data is removed.
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns>The lines of text (an empty array if the stream has no remaining data)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        /// <remarks>The text is decoded into a temporary (stack or pooled) buffer, only the lines are allocated</remarks>
        public static String[] ReadAllLines(this Stream stream, Encoding encoding = null, bool leaveOpen = false, bool trim = false, bool removeEmpty = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
            {
                using var mem = FileReadOnlyMemory.Read(fs, leaveOpen);
                return mem.Memory.Span.ToStringArray(encoding, trim, removeEmpty);
            }
            using var x = leaveOpen ? null : stream;
            var buf = ReadAllPooled(stream, out var length);
            try
            {
                return new ReadOnlySpan<Byte>(buf, 0, length).ToStringArray(encoding, trim, removeEmpty);
            }
            finally
            {
                ArrayPoolStream.Return(buf);
            }
        }

        /// <summary>
        /// The max size of a buffer that is allocated up front (based on the remaining length of a seekable stream)
        /// </summary>
        const int MaxBuf = 1 << 30;

        /// <summary>
        /// The initial buffer size when the length is unknown
        /// </summary>
        const int UnknownSizeBuf = 65536;

        /// <summary>
        /// Get the expected number of remaining bytes in the stream
        /// </summary>
        /// <param name="stream">The stream</param>
        /// <returns>The remaining number of bytes in a seekable stream, or -1 if unknown (not seekable, too large or failed to get the length / position)</returns>
        static int GetKnownSize(Stream stream)
        {
            try
            {
                if (stream.CanSeek)
                {
                    var l = stream.Length - stream.Position;
                    if (l <= 0)
                        return 0;
                    if (l <= MaxBuf)
                        return (int)l;
                }
            }
            catch
            {
            }
            return -1;
        }

        /// <summary>
        /// Read all remaining data of a stream into a pooled buffer.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="length">The number of bytes read</param>
        /// <returns>A buffer rented from ArrayPoolStream (return it with ArrayPoolStream.Return), the first length bytes are valid</returns>
        static Byte[] ReadAllPooled(Stream stream, out int length)
        {
            var size = GetKnownSize(stream);
            if (size < 0)
            {
                using var us = new ArrayPoolStream(UnknownSizeBuf);
                stream.CopyTo(us);
                return us.Detach(out length);
            }
            var buf = ArrayPoolStream.Rent(size == 0 ? 1 : size);
            int n;
            int r = 0;
            Byte probe = 0;
            bool probed = false;
            try
            {
                n = stream.ReadAtLeast(buf, size, false);
                if (n >= size)
                {
                    //  Make sure that the end of the stream is reached (in the unused part of the buffer if possible)
                    if (n < buf.Length)
                    {
                        r = stream.Read(buf, n, buf.Length - n);
                        if (r > 0)
                            n += r;
                    }
                    else
                    {
                        probed = true;
                        r = stream.Read(new Span<Byte>(ref probe));
                    }
                }
            }
            catch
            {
                ArrayPoolStream.Return(buf);
                throw;
            }
            if (r <= 0)
            {
                length = n;
                return buf;
            }
            //  The stream is longer than expected, read the rest
            return ReadRest(stream, buf, n, probed ? new ReadOnlySpan<Byte>(in probe) : default, out length);
        }

        /// <summary>
        /// The stream is longer than expected, read the rest of it
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="buf">A pooled buffer with the data read so far (it's returned to the pool by this method)</param>
        /// <param name="n">The number of valid bytes in buf</param>
        /// <param name="extra">Additional data read after the data in buf</param>
        /// <param name="length">The number of valid bytes in the returned buffer</param>
        /// <returns>A buffer rented from ArrayPoolStream (return it with ArrayPoolStream.Return), the first length bytes are valid</returns>
        static Byte[] ReadRest(Stream stream, Byte[] buf, int n, ReadOnlySpan<Byte> extra, out int length)
        {
            using var ms = new ArrayPoolStream(n << 1);
            ms.Write(new ReadOnlySpan<Byte>(buf, 0, n));
            ms.Write(extra);
            ArrayPoolStream.Return(buf);
            stream.CopyTo(ms);
            return ms.Detach(out length);
        }

        /// <summary>
        /// Read all remaining data of a stream into a pooled buffer.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <returns>A buffer rented from ArrayPoolStream (return it with ArrayPoolStream.Return) and the number of valid bytes in it</returns>
        static async ValueTask<(Byte[], int)> ReadAllPooledAsync(Stream stream)
        {
            var size = GetKnownSize(stream);
            int length;
            if (size < 0)
            {
                using var us = new ArrayPoolStream(UnknownSizeBuf);
                await stream.CopyToAsync(us).ConfigureAwait(false);
                var ud = us.Detach(out length);
                return (ud, length);
            }
            var buf = ArrayPoolStream.Rent(size == 0 ? 1 : size);
            int n;
            try
            {
                n = await stream.ReadAtLeastAsync(buf, size, false).ConfigureAwait(false);
                if (n >= size)
                {
                    //  Make sure that the end of the stream is reached (in the unused part of the buffer if possible)
                    if (n < buf.Length)
                    {
                        var r = await stream.ReadAsync(buf.AsMemory(n)).ConfigureAwait(false);
                        if (r > 0)
                            n += r;
                        else
                            return (buf, n);
                    }
                    else
                    {
                        var probe = ArrayPoolStream.Rent(1);
                        try
                        {
                            var r = await stream.ReadAsync(probe.AsMemory(0, 1)).ConfigureAwait(false);
                            if (r <= 0)
                                return (buf, n);
                            var t = ArrayPoolStream.Rent(n + 1);
                            new ReadOnlySpan<Byte>(buf, 0, n).CopyTo(t);
                            t[n] = probe[0];
                            ArrayPoolStream.Return(buf);
                            buf = t;
                            ++n;
                        }
                        finally
                        {
                            ArrayPoolStream.Return(probe);
                        }
                    }
                }
                else
                {
                    return (buf, n);
                }
            }
            catch
            {
                ArrayPoolStream.Return(buf);
                throw;
            }
            //  The stream is longer than expected, read the rest
            using var ms = new ArrayPoolStream(n << 1);
            ms.Write(new ReadOnlySpan<Byte>(buf, 0, n));
            ArrayPoolStream.Return(buf);
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            var d = ms.Detach(out length);
            return (d, length);
        }

        /// <summary>
        /// Read all remaining data of a stream with a known expected size into a new array.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="size">The expected number of remaining bytes</param>
        /// <returns>An array with all remaining data (of the expected size unless the stream was shorter or longer)</returns>
        static Byte[] ReadAllKnownSize(Stream stream, int size)
        {
            var data = size == 0 ? [] : GC.AllocateUninitializedArray<Byte>(size);
            var n = stream.ReadAtLeast(data, size, false);
            if (n < size)
                return data.AsSpan(0, n).ToArray();
            //  Make sure that the end of the stream is reached
            Span<Byte> probe = stackalloc Byte[1];
            if (stream.Read(probe) <= 0)
                return data;
            //  The stream is longer than expected, read the rest
            using var ms = new ArrayPoolStream((size + 1) << 1);
            ms.Write(data);
            ms.Write(probe);
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Read all remaining data of a stream with a known expected size into a new array.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="size">The expected number of remaining bytes</param>
        /// <returns>An array with all remaining data (of the expected size unless the stream was shorter or longer)</returns>
        static async ValueTask<Byte[]> ReadAllKnownSizeAsync(Stream stream, int size)
        {
            var data = size == 0 ? [] : GC.AllocateUninitializedArray<Byte>(size);
            var n = await stream.ReadAtLeastAsync(data, size, false).ConfigureAwait(false);
            if (n < size)
                return data.AsSpan(0, n).ToArray();
            //  Make sure that the end of the stream is reached
            var probe = ArrayPoolStream.Rent(1);
            try
            {
                if (await stream.ReadAsync(probe.AsMemory(0, 1)).ConfigureAwait(false) <= 0)
                    return data;
                //  The stream is longer than expected, read the rest
                using var ms = new ArrayPoolStream((size + 1) << 1);
                ms.Write(data);
                ms.WriteByte(probe[0]);
                await stream.CopyToAsync(ms).ConfigureAwait(false);
                return ms.ToArray();
            }
            finally
            {
                ArrayPoolStream.Return(probe);
            }
        }


        /// <summary>
        /// Read all remaining data of a stream into memory that must be disposed when no longer used (the memory may be pooled or memory mapped).
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns>The data, dispose it when done (an empty instance if the stream has no remaining data)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        public static async Task<IUnmanagedReadOnlyMemory<Byte>> ReadAllUnmanagedMemoryAsync(this Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
                return await FileReadOnlyMemory.ReadAsync(fs, leaveOpen).ConfigureAwait(false);
            using var x = leaveOpen ? null : stream;
            var (buf, length) = await ReadAllPooledAsync(stream).ConfigureAwait(false);
            return ArrayPoolStream.Lend(buf, length);
        }

        /// <summary>
        /// Read all remaining data of a stream into memory that must be disposed when no longer used (the memory may be pooled or memory mapped).
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns>The data, dispose it when done (an empty instance if the stream has no remaining data)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        public static IUnmanagedReadOnlyMemory<Byte> ReadAllUnmanagedMemory(this Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
                return FileReadOnlyMemory.Read(fs, leaveOpen);
            using var x = leaveOpen ? null : stream;
            var buf = ReadAllPooled(stream, out var length);
            return ArrayPoolStream.Lend(buf, length);
        }

        /// <summary>
        /// Read all remaining data of a stream.
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns>The data (managed memory, not pooled)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        public static async Task<ReadOnlyMemory<Byte>> ReadAllReadOnlyMemoryAsync(this Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
                return await FileReadOnlyMemory.ReadAllBytesAsync(fs, leaveOpen).ConfigureAwait(false);
            using var x = leaveOpen ? null : stream;
            var size = GetKnownSize(stream);
            if (size >= 0)
                return await ReadAllKnownSizeAsync(stream, size).ConfigureAwait(false);
            using var ms = new ArrayPoolStream(UnknownSizeBuf);
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.GetBufferMemory();
        }


        /// <summary>
        /// Read all remaining data of a stream.
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns>The data (managed memory, not pooled)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        public static async Task<Memory<Byte>> ReadAllMemoryAsync(this Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
                return await FileReadOnlyMemory.ReadAllBytesAsync(fs, leaveOpen).ConfigureAwait(false);
            using var x = leaveOpen ? null : stream;
            var size = GetKnownSize(stream);
            if (size >= 0)
                return await ReadAllKnownSizeAsync(stream, size).ConfigureAwait(false);
            using var ms = new ArrayPoolStream(UnknownSizeBuf);
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.GetBufferMemory();
        }

        /// <summary>
        /// Read all remaining data of a stream.
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns>The data (an empty array if the stream has no remaining data)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        public static async Task<Byte[]> ReadAllBytesAsync(this Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
                return await FileReadOnlyMemory.ReadAllBytesAsync(fs, leaveOpen).ConfigureAwait(false);
            using var x = leaveOpen ? null : stream;
            var size = GetKnownSize(stream);
            if (size >= 0)
                return await ReadAllKnownSizeAsync(stream, size).ConfigureAwait(false);
            using var ms = new ArrayPoolStream(UnknownSizeBuf);
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }

        /// <summary>
        /// Read all remaining data of a stream.
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns>The data (managed memory, not pooled)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        public static Memory<Byte> ReadAllMemory(this Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
                return FileReadOnlyMemory.ReadAllBytes(fs, leaveOpen);
            using var x = leaveOpen ? null : stream;
            var size = GetKnownSize(stream);
            if (size >= 0)
                return ReadAllKnownSize(stream, size);
            using var ms = new ArrayPoolStream(UnknownSizeBuf);
            stream.CopyTo(ms);
            return ms.GetBufferMemory();
        }

        /// <summary>
        /// Read all remaining data of a stream.
        /// </summary>
        /// <param name="stream">The stream to read from (from the current position)</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns>The data (an empty array if the stream has no remaining data)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        /// <exception cref="NotSupportedException">The stream doesn't support reading</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        /// <exception cref="IOException">An I/O error occurred</exception>
        public static Byte[] ReadAllBytes(this Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream is FileStream fs)
                return FileReadOnlyMemory.ReadAllBytes(fs, leaveOpen);
            using var x = leaveOpen ? null : stream;
            var size = GetKnownSize(stream);
            if (size >= 0)
                return ReadAllKnownSize(stream, size);
            using var ms = new ArrayPoolStream(UnknownSizeBuf);
            stream.CopyTo(ms);
            return ms.ToArray();
        }


    }

}
