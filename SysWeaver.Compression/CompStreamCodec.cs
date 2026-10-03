using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace SysWeaver.Compression
{
    /// <summary>
    /// A streaming encoder, with the same semantics as System.IO.Compression.BrotliEncoder.
    /// Implemented as a struct so that the generic code in <see cref="CompStreamCodec{TEncoder, TDecoder}"/> is specialized (no boxing, no interface calls).
    /// </summary>
    /// <typeparam name="TSelf">The implementing type</typeparam>
    public interface ICompStreamEncoder<TSelf> : IDisposable where TSelf : struct, ICompStreamEncoder<TSelf>
    {
        /// <summary>
        /// Create (or rent) an encoder, it's disposed (or returned) when done
        /// </summary>
        static abstract TSelf Create(CompEncoderLevels level);

        /// <summary>
        /// Get the max size of the compressed data
        /// </summary>
        static abstract int GetMaxCompressedLength(int inputSize);

        /// <summary>
        /// Compress all data in one go, <see cref="CompStreamCodec{TEncoder, TDecoder}.TryCompress(ref TEncoder, ReadOnlySpan{byte}, Span{byte}, out int)"/> can be used if there is no special one-shot api
        /// </summary>
        /// <returns>False if the compressed data doesn't fit in the destination</returns>
        static abstract bool TryCompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten, CompEncoderLevels level);

        /// <summary>
        /// Compress some data.
        /// Returns Done when all source data is consumed (and if isFinalBlock is true, when all data is written),
        /// DestinationTooSmall if there is more data to write (or source data to consume), InvalidData on errors.
        /// </summary>
        OperationStatus Compress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock);
    }

    /// <summary>
    /// A streaming decoder, with the same semantics as System.IO.Compression.BrotliDecoder.
    /// Implemented as a struct so that the generic code in <see cref="CompStreamCodec{TEncoder, TDecoder}"/> is specialized (no boxing, no interface calls).
    /// </summary>
    /// <typeparam name="TSelf">The implementing type</typeparam>
    public interface ICompStreamDecoder<TSelf> : IDisposable where TSelf : struct, ICompStreamDecoder<TSelf>
    {
        /// <summary>
        /// Create (or rent) a decoder, it's disposed (or returned) when done
        /// </summary>
        static abstract TSelf Create();

        /// <summary>
        /// Decompress all data in one go using a special one-shot api.
        /// Return false to use the streaming decoder instead (also return false on any failure, the streaming decoder determines the exception to throw).
        /// </summary>
        static abstract bool TryDecompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten);

        /// <summary>
        /// The number of bytes needed to determine if another (concatenated) stream follows a stream, 0 if the format doesn't support concatenated streams.
        /// For formats where concatenated streams decompress to the concatenated data (like gzip members and zstd frames).
        /// </summary>
        static abstract int NextHeaderSize { get; }

        /// <summary>
        /// Called when the decoder is done (and NextHeaderSize is greater than 0) and there is more data.
        /// Return true if the data is the start of another (concatenated) stream, the decoder must be ready to decode it.
        /// Return false to stop (any remaining data is ignored).
        /// </summary>
        /// <param name="next">The data after the stream, at least NextHeaderSize bytes unless the data ends before</param>
        bool BeginNext(ReadOnlySpan<Byte> next);

        /// <summary>
        /// Decompress some data.
        /// Returns Done when the end of the compressed data is reached, NeedMoreData if all source data is consumed,
        /// DestinationTooSmall if the destination is full (may also be returned for an empty destination), InvalidData if the data is invalid.
        /// </summary>
        OperationStatus Decompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten);
    }


    /// <summary>
    /// Implements all ICompEncoder and ICompDecoder methods for a streaming encoder and decoder.
    /// Small data is compressed in one go, larger data is streamed in 64 KB chunks.
    /// All temporary buffers are pooled, so the only heap allocations are the ones done by the encoder / decoder (and the tasks of the async methods).
    /// </summary>
    /// <typeparam name="TEncoder">The encoder</typeparam>
    /// <typeparam name="TDecoder">The decoder</typeparam>
    public static class CompStreamCodec<TEncoder, TDecoder>
        where TEncoder : struct, ICompStreamEncoder<TEncoder>
        where TDecoder : struct, ICompStreamDecoder<TDecoder>
    {
        #region Helpers

        /// <summary>
        /// Max number of input bytes to read into a stack buffer
        /// </summary>
        const int MaxStack = (1 << 10) - 128;

        /// <summary>
        /// Max number of input bytes to compress in one go (larger inputs are streamed)
        /// </summary>
        const int MaxBuffered = (1 << 16) - 128;

        /// <summary>
        /// Size of the input and output chunks used when streaming
        /// </summary>
        const int ChunkSize = 1 << 16;

        /// <summary>
        /// Get the number of bytes remaining in a stream if it's small enough to be compressed in one go
        /// </summary>
        /// <param name="s">The stream</param>
        /// <returns>The number of bytes remaining in the stream, or -1 if the stream is unseekable or too large</returns>
        static int GetBufferableLength(Stream s)
        {
            if (!s.CanSeek)
                return -1;
            long len;
            try
            {
                len = s.Length - s.Position;
            }
            catch (NotSupportedException)
            {
                return -1;
            }
            return (len >= 0) && (len < MaxBuffered) ? (int)len : -1;
        }

        /// <summary>
        /// Compress all data using an encoder (for encoders without a special one-shot api)
        /// </summary>
        /// <param name="enc">The encoder</param>
        /// <param name="source">The data to compress</param>
        /// <param name="destination">The destination</param>
        /// <param name="bytesWritten">The number of bytes written to the destination</param>
        /// <returns>False if the compressed data doesn't fit in the destination</returns>
        public static bool TryCompress(ref TEncoder enc, ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            for (; ; )
            {
                var status = enc.Compress(source, destination.Slice(bytesWritten), out var consumed, out var written, true);
                source = source.Slice(consumed);
                bytesWritten += written;
                if (status == OperationStatus.Done)
                    return true;
                if (status != OperationStatus.DestinationTooSmall)
                    throw new InvalidOperationException(CompHelpers.EncFailed);
                if (bytesWritten < destination.Length)
                    continue;
                //  The encoder may report DestinationTooSmall on an exact fit, probe if there is any more output
                Span<Byte> probe = stackalloc Byte[1];
                status = enc.Compress(source, probe, out _, out written, true);
                if ((written > 0) || (status != OperationStatus.Done))
                {
                    bytesWritten = 0;
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Feed data to an encoder, writing the output to a span
        /// </summary>
        /// <returns>The new position in the output span</returns>
        static int Encode(ref TEncoder enc, ReadOnlySpan<Byte> from, bool isFinal, Span<Byte> to, int pos)
        {
            Span<Byte> probe = stackalloc Byte[1];
            for (; ; )
            {
                var status = enc.Compress(from, to.Slice(pos), out var consumed, out var written, isFinal);
                pos += written;
                from = from.Slice(consumed);
                if (status == OperationStatus.Done)
                    return pos;
                if (status != OperationStatus.DestinationTooSmall)
                    throw new InvalidOperationException(CompHelpers.EncFailed);
                if (pos < to.Length)
                    continue;
                //  The encoder may report DestinationTooSmall on an exact fit, probe if there is any more output
                status = enc.Compress(from, probe, out consumed, out written, isFinal);
                if (written > 0)
                    throw new ArgumentException(CompHelpers.EncDestTooSmall, nameof(to));
                if (status != OperationStatus.Done)
                    throw new InvalidOperationException(CompHelpers.EncFailed);
                return pos;
            }
        }

        /// <summary>
        /// Feed data to an encoder, writing the output to a stream
        /// </summary>
        static void Encode(ref TEncoder enc, ReadOnlySpan<Byte> from, bool isFinal, Byte[] buf, int bufOffset, Stream to)
        {
            OperationStatus status;
            do
            {
                status = enc.Compress(from, buf.AsSpan(bufOffset, ChunkSize), out var consumed, out var written, isFinal);
                if (status == OperationStatus.InvalidData)
                    throw new InvalidOperationException(CompHelpers.EncFailed);
                from = from.Slice(consumed);
                if (written > 0)
                    to.Write(buf, bufOffset, written);
            }
            while (status == OperationStatus.DestinationTooSmall);
        }

        /// <summary>
        /// Check the decoder status after it was fed
        /// </summary>
        /// <returns>True if the decoder is done</returns>
        static bool IsDone(OperationStatus status)
        {
            if (status == OperationStatus.Done)
                return true;
            if (status == OperationStatus.InvalidData)
                throw new InvalidDataException(CompHelpers.DecInvalid);
            return false;
        }

        /// <summary>
        /// Call when the decoder reports DestinationTooSmall and the destination is full.
        /// The decoder reports DestinationTooSmall for an empty destination, probe if there is any more output.
        /// </summary>
        /// <returns>True if the decoder is done</returns>
        static bool ProbeIsDone(ref TDecoder dec, ReadOnlySpan<Byte> from, out int consumed)
        {
            Span<Byte> probe = stackalloc Byte[1];
            var status = dec.Decompress(from, probe, out consumed, out var written);
            if ((written > 0) || (status == OperationStatus.DestinationTooSmall))
                throw new ArgumentException(CompHelpers.DecDestTooSmall, "to");
            return IsDone(status);
        }

        /// <summary>
        /// Call when the decoder is done, to check if another (concatenated) stream follows
        /// </summary>
        /// <returns>True if another stream follows and the decoder is ready to decode it</returns>
        static bool BeginNext(ref TDecoder dec, ReadOnlySpan<Byte> rest) => (TDecoder.NextHeaderSize > 0) && (!rest.IsEmpty) && dec.BeginNext(rest);

        /// <summary>
        /// Move any unconsumed input to the start of the buffer and fill the rest from the stream
        /// </summary>
        /// <returns>The number of bytes in the input buffer</returns>
        static int Refill(Stream from, Byte[] buf, int inPos, int inEnd)
        {
            var rem = inEnd - inPos;
            if (rem > 0)
                Buffer.BlockCopy(buf, inPos, buf, 0, rem);
            var read = from.Read(buf, rem, ChunkSize - rem);
            if (read <= 0)
                throw new InvalidDataException(CompHelpers.DecTruncated);
            return rem + read;
        }

        static async ValueTask<int> RefillAsync(Stream from, Byte[] buf, int inPos, int inEnd)
        {
            var rem = inEnd - inPos;
            if (rem > 0)
                Buffer.BlockCopy(buf, inPos, buf, 0, rem);
            var read = await from.ReadAsync(buf.AsMemory(rem, ChunkSize - rem)).ConfigureAwait(false);
            if (read <= 0)
                throw new InvalidDataException(CompHelpers.DecTruncated);
            return rem + read;
        }

        /// <summary>
        /// Make sure that there is enough data in the input buffer to determine if another stream follows (the end of the stream is fine)
        /// </summary>
        /// <returns>The number of bytes in the input buffer (starting at 0)</returns>
        static int FillNext(Stream from, Byte[] buf, int inPos, int inEnd)
        {
            var rem = inEnd - inPos;
            if (rem > 0)
                Buffer.BlockCopy(buf, inPos, buf, 0, rem);
            var need = TDecoder.NextHeaderSize;
            if (rem < need)
                rem += from.ReadAtLeast(buf.AsSpan(rem, ChunkSize - rem), need - rem, false);
            return rem;
        }

        static async ValueTask<int> FillNextAsync(Stream from, Byte[] buf, int inPos, int inEnd)
        {
            var rem = inEnd - inPos;
            if (rem > 0)
                Buffer.BlockCopy(buf, inPos, buf, 0, rem);
            var need = TDecoder.NextHeaderSize;
            if (rem < need)
                rem += await from.ReadAtLeastAsync(buf.AsMemory(rem, ChunkSize - rem), need - rem, false).ConfigureAwait(false);
            return rem;
        }

        #endregion//Helpers

        #region Compress

        public static void Compress(Stream from, Stream to, CompEncoderLevels level)
        {
            var len = GetBufferableLength(from);
            if (len >= 0)
            {
                var maxLen = TEncoder.GetMaxCompressedLength(len);
                var temp = ArrayPoolStream.Rent(len + maxLen);
                try
                {
                    from.ReadExactly(temp, 0, len);
                    var destLen = Compress(new ReadOnlySpan<Byte>(temp, 0, len), temp.AsSpan(len, maxLen), level);
                    to.Write(temp, len, destLen);
                    return;
                }
                finally
                {
                    ArrayPoolStream.Return(temp);
                }
            }
            var enc = TEncoder.Create(level);
            var buf = ArrayPoolStream.Rent(ChunkSize * 2);
            try
            {
                bool isFinal;
                do
                {
                    var read = from.ReadAtLeast(buf.AsSpan(0, ChunkSize), ChunkSize, false);
                    isFinal = read <= 0;
                    Encode(ref enc, new ReadOnlySpan<Byte>(buf, 0, isFinal ? 0 : read), isFinal, buf, ChunkSize, to);
                }
                while (!isFinal);
            }
            finally
            {
                enc.Dispose();
                ArrayPoolStream.Return(buf);
            }
        }

        public static int Compress(Stream from, Span<Byte> to, CompEncoderLevels level)
        {
            var len = GetBufferableLength(from);
            if (len >= 0)
            {
                if (len < MaxStack)
                {
                    Span<Byte> src = stackalloc Byte[len];
                    from.ReadExactly(src);
                    return Compress(src, to, level);
                }
                var temp = ArrayPoolStream.Rent(len);
                try
                {
                    from.ReadExactly(temp, 0, len);
                    return Compress(new ReadOnlySpan<Byte>(temp, 0, len), to, level);
                }
                finally
                {
                    ArrayPoolStream.Return(temp);
                }
            }
            var enc = TEncoder.Create(level);
            var buf = ArrayPoolStream.Rent(ChunkSize);
            try
            {
                int pos = 0;
                bool isFinal;
                do
                {
                    var read = from.ReadAtLeast(buf.AsSpan(0, ChunkSize), ChunkSize, false);
                    isFinal = read <= 0;
                    pos = Encode(ref enc, new ReadOnlySpan<Byte>(buf, 0, isFinal ? 0 : read), isFinal, to, pos);
                }
                while (!isFinal);
                return pos;
            }
            finally
            {
                enc.Dispose();
                ArrayPoolStream.Return(buf);
            }
        }

        public static int Compress(ReadOnlySpan<Byte> from, Span<Byte> to, CompEncoderLevels level)
        {
            if (!TEncoder.TryCompress(from, to, out var written, level))
                throw new ArgumentException(CompHelpers.EncDestTooSmall, nameof(to));
            return written;
        }

        public static void Compress(ReadOnlySpan<Byte> from, Stream to, CompEncoderLevels level)
        {
            var len = from.Length;
            if (len < MaxBuffered)
            {
                var maxLen = TEncoder.GetMaxCompressedLength(len);
                var buf = ArrayPoolStream.Rent(maxLen);
                try
                {
                    var s = Compress(from, buf.AsSpan(0, maxLen), level);
                    to.Write(buf, 0, s);
                    return;
                }
                finally
                {
                    ArrayPoolStream.Return(buf);
                }
            }
            var enc = TEncoder.Create(level);
            var obuf = ArrayPoolStream.Rent(ChunkSize);
            try
            {
                Encode(ref enc, from, true, obuf, 0, to);
            }
            finally
            {
                enc.Dispose();
                ArrayPoolStream.Return(obuf);
            }
        }

        public static async Task CompressAsync(Stream from, Stream to, CompEncoderLevels level)
        {
            var len = GetBufferableLength(from);
            if (len >= 0)
            {
                var maxLen = TEncoder.GetMaxCompressedLength(len);
                var temp = ArrayPoolStream.Rent(len + maxLen);
                try
                {
                    await from.ReadExactlyAsync(temp.AsMemory(0, len)).ConfigureAwait(false);
                    var destLen = Compress(new ReadOnlySpan<Byte>(temp, 0, len), temp.AsSpan(len, maxLen), level);
                    await to.WriteAsync(temp.AsMemory(len, destLen)).ConfigureAwait(false);
                    return;
                }
                finally
                {
                    ArrayPoolStream.Return(temp);
                }
            }
            var enc = TEncoder.Create(level);
            var buf = ArrayPoolStream.Rent(ChunkSize * 2);
            try
            {
                bool isFinal;
                do
                {
                    var read = await from.ReadAtLeastAsync(buf.AsMemory(0, ChunkSize), ChunkSize, false).ConfigureAwait(false);
                    isFinal = read <= 0;
                    int inPos = 0;
                    var inEnd = isFinal ? 0 : read;
                    OperationStatus status;
                    do
                    {
                        status = enc.Compress(new ReadOnlySpan<Byte>(buf, inPos, inEnd - inPos), buf.AsSpan(ChunkSize, ChunkSize), out var consumed, out var written, isFinal);
                        if (status == OperationStatus.InvalidData)
                            throw new InvalidOperationException(CompHelpers.EncFailed);
                        inPos += consumed;
                        if (written > 0)
                            await to.WriteAsync(buf.AsMemory(ChunkSize, written)).ConfigureAwait(false);
                    }
                    while (status == OperationStatus.DestinationTooSmall);
                }
                while (!isFinal);
            }
            finally
            {
                enc.Dispose();
                ArrayPoolStream.Return(buf);
            }
        }

        public static async Task<int> CompressAsync(Stream from, Memory<Byte> to, CompEncoderLevels level)
        {
            var len = GetBufferableLength(from);
            if (len >= 0)
            {
                var temp = ArrayPoolStream.Rent(len);
                try
                {
                    await from.ReadExactlyAsync(temp.AsMemory(0, len)).ConfigureAwait(false);
                    return Compress(new ReadOnlySpan<Byte>(temp, 0, len), to.Span, level);
                }
                finally
                {
                    ArrayPoolStream.Return(temp);
                }
            }
            var enc = TEncoder.Create(level);
            var buf = ArrayPoolStream.Rent(ChunkSize);
            try
            {
                int pos = 0;
                bool isFinal;
                do
                {
                    var read = await from.ReadAtLeastAsync(buf.AsMemory(0, ChunkSize), ChunkSize, false).ConfigureAwait(false);
                    isFinal = read <= 0;
                    pos = Encode(ref enc, new ReadOnlySpan<Byte>(buf, 0, isFinal ? 0 : read), isFinal, to.Span, pos);
                }
                while (!isFinal);
                return pos;
            }
            finally
            {
                enc.Dispose();
                ArrayPoolStream.Return(buf);
            }
        }

        public static async Task CompressAsync(ReadOnlyMemory<Byte> from, Stream to, CompEncoderLevels level)
        {
            var len = from.Length;
            if (len < MaxBuffered)
            {
                var maxLen = TEncoder.GetMaxCompressedLength(len);
                var buf = ArrayPoolStream.Rent(maxLen);
                try
                {
                    var s = Compress(from.Span, buf.AsSpan(0, maxLen), level);
                    await to.WriteAsync(buf.AsMemory(0, s)).ConfigureAwait(false);
                    return;
                }
                finally
                {
                    ArrayPoolStream.Return(buf);
                }
            }
            var enc = TEncoder.Create(level);
            var obuf = ArrayPoolStream.Rent(ChunkSize);
            try
            {
                OperationStatus status;
                do
                {
                    status = enc.Compress(from.Span, obuf.AsSpan(0, ChunkSize), out var consumed, out var written, true);
                    if (status == OperationStatus.InvalidData)
                        throw new InvalidOperationException(CompHelpers.EncFailed);
                    from = from.Slice(consumed);
                    if (written > 0)
                        await to.WriteAsync(obuf.AsMemory(0, written)).ConfigureAwait(false);
                }
                while (status == OperationStatus.DestinationTooSmall);
            }
            finally
            {
                enc.Dispose();
                ArrayPoolStream.Return(obuf);
            }
        }

        #endregion//Compress


        #region Decompress

        public static void Decompress(Stream from, Stream to)
        {
            var dec = TDecoder.Create();
            var buf = ArrayPoolStream.Rent(ChunkSize * 2);
            try
            {
                int inPos = 0;
                int inEnd = 0;
                for (; ; )
                {
                    var status = dec.Decompress(new ReadOnlySpan<Byte>(buf, inPos, inEnd - inPos), buf.AsSpan(ChunkSize, ChunkSize), out var consumed, out var written);
                    inPos += consumed;
                    if (written > 0)
                        to.Write(buf, ChunkSize, written);
                    if (IsDone(status))
                    {
                        if (TDecoder.NextHeaderSize <= 0)
                            return;
                        inEnd = FillNext(from, buf, inPos, inEnd);
                        inPos = 0;
                        if (BeginNext(ref dec, new ReadOnlySpan<Byte>(buf, 0, inEnd)))
                            continue;
                        return;
                    }
                    if (status == OperationStatus.NeedMoreData)
                    {
                        inEnd = Refill(from, buf, inPos, inEnd);
                        inPos = 0;
                    }
                }
            }
            finally
            {
                dec.Dispose();
                ArrayPoolStream.Return(buf);
            }
        }

        public static int Decompress(Stream from, Span<Byte> to)
        {
            var len = GetBufferableLength(from);
            if (len >= 0)
            {
                if (len < MaxStack)
                {
                    Span<Byte> src = stackalloc Byte[len];
                    from.ReadExactly(src);
                    return Decompress(src, to);
                }
                var temp = ArrayPoolStream.Rent(len);
                try
                {
                    from.ReadExactly(temp, 0, len);
                    return Decompress(new ReadOnlySpan<Byte>(temp, 0, len), to);
                }
                finally
                {
                    ArrayPoolStream.Return(temp);
                }
            }
            var dec = TDecoder.Create();
            var buf = ArrayPoolStream.Rent(ChunkSize);
            try
            {
                int inPos = 0;
                int inEnd = 0;
                int pos = 0;
                for (; ; )
                {
                    var status = dec.Decompress(new ReadOnlySpan<Byte>(buf, inPos, inEnd - inPos), to.Slice(pos), out var consumed, out var written);
                    inPos += consumed;
                    pos += written;
                    bool done = IsDone(status);
                    if ((!done) && (status == OperationStatus.DestinationTooSmall))
                    {
                        if (pos < to.Length)
                            continue;
                        done = ProbeIsDone(ref dec, new ReadOnlySpan<Byte>(buf, inPos, inEnd - inPos), out consumed);
                        inPos += consumed;
                    }
                    if (done)
                    {
                        if (TDecoder.NextHeaderSize <= 0)
                            return pos;
                        inEnd = FillNext(from, buf, inPos, inEnd);
                        inPos = 0;
                        if (BeginNext(ref dec, new ReadOnlySpan<Byte>(buf, 0, inEnd)))
                            continue;
                        return pos;
                    }
                    inEnd = Refill(from, buf, inPos, inEnd);
                    inPos = 0;
                }
            }
            finally
            {
                dec.Dispose();
                ArrayPoolStream.Return(buf);
            }
        }

        public static int Decompress(ReadOnlySpan<Byte> from, Span<Byte> to)
        {
            if (TDecoder.TryDecompress(from, to, out var written))
                return written;
            var dec = TDecoder.Create();
            try
            {
                int pos = 0;
                for (; ; )
                {
                    var status = dec.Decompress(from, to.Slice(pos), out var consumed, out written);
                    from = from.Slice(consumed);
                    pos += written;
                    bool done = IsDone(status);
                    if ((!done) && (status == OperationStatus.DestinationTooSmall))
                    {
                        if (pos < to.Length)
                            continue;
                        done = ProbeIsDone(ref dec, from, out consumed);
                        from = from.Slice(consumed);
                    }
                    if (done)
                    {
                        if (BeginNext(ref dec, from))
                            continue;
                        return pos;
                    }
                    throw new InvalidDataException(CompHelpers.DecTruncated);
                }
            }
            finally
            {
                dec.Dispose();
            }
        }

        public static void Decompress(ReadOnlySpan<Byte> from, Stream to)
        {
            var dec = TDecoder.Create();
            var buf = ArrayPoolStream.Rent(ChunkSize);
            try
            {
                for (; ; )
                {
                    var status = dec.Decompress(from, buf.AsSpan(0, ChunkSize), out var consumed, out var written);
                    from = from.Slice(consumed);
                    if (written > 0)
                        to.Write(buf, 0, written);
                    if (IsDone(status))
                    {
                        if (BeginNext(ref dec, from))
                            continue;
                        return;
                    }
                    if (status == OperationStatus.NeedMoreData)
                        throw new InvalidDataException(CompHelpers.DecTruncated);
                }
            }
            finally
            {
                dec.Dispose();
                ArrayPoolStream.Return(buf);
            }
        }

        public static async Task DecompressAsync(Stream from, Stream to)
        {
            var dec = TDecoder.Create();
            var buf = ArrayPoolStream.Rent(ChunkSize * 2);
            try
            {
                int inPos = 0;
                int inEnd = 0;
                for (; ; )
                {
                    var status = dec.Decompress(new ReadOnlySpan<Byte>(buf, inPos, inEnd - inPos), buf.AsSpan(ChunkSize, ChunkSize), out var consumed, out var written);
                    inPos += consumed;
                    if (written > 0)
                        await to.WriteAsync(buf.AsMemory(ChunkSize, written)).ConfigureAwait(false);
                    if (IsDone(status))
                    {
                        if (TDecoder.NextHeaderSize <= 0)
                            return;
                        inEnd = await FillNextAsync(from, buf, inPos, inEnd).ConfigureAwait(false);
                        inPos = 0;
                        if (BeginNext(ref dec, new ReadOnlySpan<Byte>(buf, 0, inEnd)))
                            continue;
                        return;
                    }
                    if (status == OperationStatus.NeedMoreData)
                    {
                        inEnd = await RefillAsync(from, buf, inPos, inEnd).ConfigureAwait(false);
                        inPos = 0;
                    }
                }
            }
            finally
            {
                dec.Dispose();
                ArrayPoolStream.Return(buf);
            }
        }

        public static async Task<int> DecompressAsync(Stream from, Memory<Byte> to)
        {
            var len = GetBufferableLength(from);
            if (len >= 0)
            {
                var temp = ArrayPoolStream.Rent(len);
                try
                {
                    await from.ReadExactlyAsync(temp.AsMemory(0, len)).ConfigureAwait(false);
                    return Decompress(new ReadOnlySpan<Byte>(temp, 0, len), to.Span);
                }
                finally
                {
                    ArrayPoolStream.Return(temp);
                }
            }
            var dec = TDecoder.Create();
            var buf = ArrayPoolStream.Rent(ChunkSize);
            try
            {
                int inPos = 0;
                int inEnd = 0;
                int pos = 0;
                for (; ; )
                {
                    var status = dec.Decompress(new ReadOnlySpan<Byte>(buf, inPos, inEnd - inPos), to.Span.Slice(pos), out var consumed, out var written);
                    inPos += consumed;
                    pos += written;
                    bool done = IsDone(status);
                    if ((!done) && (status == OperationStatus.DestinationTooSmall))
                    {
                        if (pos < to.Length)
                            continue;
                        done = ProbeIsDone(ref dec, new ReadOnlySpan<Byte>(buf, inPos, inEnd - inPos), out consumed);
                        inPos += consumed;
                    }
                    if (done)
                    {
                        if (TDecoder.NextHeaderSize <= 0)
                            return pos;
                        inEnd = await FillNextAsync(from, buf, inPos, inEnd).ConfigureAwait(false);
                        inPos = 0;
                        if (BeginNext(ref dec, new ReadOnlySpan<Byte>(buf, 0, inEnd)))
                            continue;
                        return pos;
                    }
                    inEnd = await RefillAsync(from, buf, inPos, inEnd).ConfigureAwait(false);
                    inPos = 0;
                }
            }
            finally
            {
                dec.Dispose();
                ArrayPoolStream.Return(buf);
            }
        }

        public static async Task DecompressAsync(ReadOnlyMemory<Byte> from, Stream to)
        {
            var dec = TDecoder.Create();
            var buf = ArrayPoolStream.Rent(ChunkSize);
            try
            {
                for (; ; )
                {
                    var status = dec.Decompress(from.Span, buf.AsSpan(0, ChunkSize), out var consumed, out var written);
                    from = from.Slice(consumed);
                    if (written > 0)
                        await to.WriteAsync(buf.AsMemory(0, written)).ConfigureAwait(false);
                    if (IsDone(status))
                    {
                        if (BeginNext(ref dec, from.Span))
                            continue;
                        return;
                    }
                    if (status == OperationStatus.NeedMoreData)
                        throw new InvalidDataException(CompHelpers.DecTruncated);
                }
            }
            finally
            {
                dec.Dispose();
                ArrayPoolStream.Return(buf);
            }
        }

        #endregion//Decompress
    }
}
