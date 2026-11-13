using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// A logarithmic latency histogram: 20 buckets per decade of microseconds (each bucket is about 12 % wide), from 1 µs to 1000 s.
    /// Percentiles are read as the geometric middle of their bucket (clamped to the exact min and max), so they are accurate to about ±6 %.
    /// </summary>
    public static class LatencyHistogram
    {
        /// <summary>
        /// Buckets per decade.
        /// </summary>
        public const int PerDecade = 20;

        /// <summary>
        /// The number of buckets (1 µs to 10^9 µs).
        /// </summary>
        public const int Count = 9 * PerDecade + 1;

        /// <summary>
        /// The bucket of a duration.
        /// </summary>
        /// <param name="us">The duration in µs</param>
        /// <returns>The bucket index</returns>
        public static int Index(long us)
        {
            if (us <= 1)
                return 0;
            var i = (int)(Math.Log10(us) * PerDecade);
            return i >= Count ? Count - 1 : i;
        }

        /// <summary>
        /// The lowest duration of a bucket in µs.
        /// </summary>
        public static double From(int index) => Math.Pow(10, index / (double)PerDecade);

        /// <summary>
        /// The highest duration of a bucket in µs.
        /// </summary>
        public static double To(int index) => Math.Pow(10, (index + 1) / (double)PerDecade);

        /// <summary>
        /// The duration that represents a bucket (its geometric middle) in µs.
        /// </summary>
        public static double Mid(int index) => Math.Pow(10, (index + 0.5) / (double)PerDecade);

        /// <summary>
        /// Add a value (not thread safe: a histogram that only one thread updates).
        /// </summary>
        public static void Add(long[] h, long us) => ++h[Index(us)];

        /// <summary>
        /// Add all values of a histogram to one that other threads update too (thread safe for the destination, only the buckets that have values).
        /// </summary>
        public static void AddAtomic(long[] dst, long[] src)
        {
            for (int i = 0; i < Count; ++i)
            {
                var v = src[i];
                if (v != 0)
                    Interlocked.Add(ref dst[i], v);
            }
        }

        /// <summary>
        /// Add all values of a histogram to another one (not thread safe for the destination).
        /// </summary>
        public static void AddTo(long[] dst, long[] src)
        {
            if (src == null)
                return;
            for (int i = 0; i < Count; ++i)
                dst[i] += Volatile.Read(ref src[i]);
        }

        /// <summary>
        /// Get a percentile in µs.
        /// </summary>
        /// <param name="h">The histogram</param>
        /// <param name="total">The total count</param>
        /// <param name="p">The percentile (0 to 1)</param>
        /// <param name="minUs">The exact minimum (clamps the result)</param>
        /// <param name="maxUs">The exact maximum (clamps the result)</param>
        /// <returns>The value in µs, 0 if there are no values</returns>
        public static double Percentile(long[] h, long total, double p, long minUs, long maxUs)
        {
            if ((h == null) || (total <= 0))
                return 0;
            var target = Math.Max(1, (long)Math.Ceiling(total * p));
            long sum = 0;
            for (int i = 0; i < Count; ++i)
            {
                sum += h[i];
                if (sum >= target)
                    return Math.Clamp(Mid(i), minUs, Math.Max(minUs, maxUs));
            }
            return maxUs;
        }
    }

    /// <summary>
    /// The counters of a set of requests.
    /// A worker records its requests with <see cref="AddLocal"/> into counters of its own (no locks or interlocked operations in the request path)
    /// and merges them into the shared counters once per second with <see cref="AddAtomic"/>.
    /// </summary>
    public sealed class BenchCounters
    {
        public long Count;
        public long Errors;
        public long SumUs;
        public long SumTtfbUs;
        public long MinUs = long.MaxValue;
        public long MaxUs;
        public long WireSent;
        public long WireRecv;
        public long BodySent;
        public long BodyRecv;
        public long Connections;

        /// <summary>
        /// The latency histogram (null if not kept).
        /// </summary>
        public readonly long[] Hist;

        public BenchCounters(bool histogram)
        {
            if (histogram)
                Hist = new long[LatencyHistogram.Count];
        }

        /// <summary>
        /// Add a request (not thread safe: counters that only one thread updates, other threads may read them while they change).
        /// </summary>
        public void AddLocal(long us, long ttfbUs, bool error, long wireSent, long wireRecv, long bodySent, long bodyRecv, bool newConnection)
        {
            ++Count;
            if (error)
                ++Errors;
            SumUs += us;
            SumTtfbUs += ttfbUs;
            WireSent += wireSent;
            WireRecv += wireRecv;
            BodySent += bodySent;
            BodyRecv += bodyRecv;
            if (newConnection)
                ++Connections;
            if (us < MinUs)
                MinUs = us;
            if (us > MaxUs)
                MaxUs = us;
            if (Hist != null)
                LatencyHistogram.Add(Hist, us);
        }

        /// <summary>
        /// Add counters to these ones while other threads do the same (thread safe for these counters, the source must not change meanwhile).
        /// </summary>
        /// <param name="c">The counters to add</param>
        /// <param name="histogram">Add the histogram too (if both have one)</param>
        public void AddAtomic(BenchCounters c, bool histogram)
        {
            if (c.Count <= 0)
                return;
            Interlocked.Add(ref Count, c.Count);
            if (c.Errors != 0)
                Interlocked.Add(ref Errors, c.Errors);
            Interlocked.Add(ref SumUs, c.SumUs);
            Interlocked.Add(ref SumTtfbUs, c.SumTtfbUs);
            Interlocked.Add(ref WireSent, c.WireSent);
            Interlocked.Add(ref WireRecv, c.WireRecv);
            Interlocked.Add(ref BodySent, c.BodySent);
            Interlocked.Add(ref BodyRecv, c.BodyRecv);
            if (c.Connections != 0)
                Interlocked.Add(ref Connections, c.Connections);
            var us = c.MinUs;
            var v = Volatile.Read(ref MinUs);
            while (us < v)
            {
                var o = Interlocked.CompareExchange(ref MinUs, us, v);
                if (o == v)
                    break;
                v = o;
            }
            us = c.MaxUs;
            v = Volatile.Read(ref MaxUs);
            while (us > v)
            {
                var o = Interlocked.CompareExchange(ref MaxUs, us, v);
                if (o == v)
                    break;
                v = o;
            }
            if (histogram && (Hist != null) && (c.Hist != null))
                LatencyHistogram.AddAtomic(Hist, c.Hist);
        }

        /// <summary>
        /// Clear all values (not thread safe).
        /// </summary>
        public void Reset()
        {
            Count = 0;
            Errors = 0;
            SumUs = 0;
            SumTtfbUs = 0;
            MinUs = long.MaxValue;
            MaxUs = 0;
            WireSent = 0;
            WireRecv = 0;
            BodySent = 0;
            BodyRecv = 0;
            Connections = 0;
            if (Hist != null)
                Array.Clear(Hist);
        }

        /// <summary>
        /// Add the values of other counters (not thread safe, used to build snapshots).
        /// </summary>
        public void AddFrom(BenchCounters c)
        {
            if (c == null)
                return;
            var n = Volatile.Read(ref c.Count);
            if (n <= 0)
                return;
            Count += n;
            Errors += Volatile.Read(ref c.Errors);
            SumUs += Volatile.Read(ref c.SumUs);
            SumTtfbUs += Volatile.Read(ref c.SumTtfbUs);
            WireSent += Volatile.Read(ref c.WireSent);
            WireRecv += Volatile.Read(ref c.WireRecv);
            BodySent += Volatile.Read(ref c.BodySent);
            BodyRecv += Volatile.Read(ref c.BodyRecv);
            Connections += Volatile.Read(ref c.Connections);
            MinUs = Math.Min(MinUs, Volatile.Read(ref c.MinUs));
            MaxUs = Math.Max(MaxUs, Volatile.Read(ref c.MaxUs));
            if ((Hist != null) && (c.Hist != null))
                LatencyHistogram.AddTo(Hist, c.Hist);
        }

        public double AvgMs => Count > 0 ? SumUs / (Count * 1000.0) : 0;
        public double AvgTtfbMs => Count > 0 ? SumTtfbUs / (Count * 1000.0) : 0;
        public double MinMs => Count > 0 ? MinUs / 1000.0 : 0;
        public double MaxMs => MaxUs / 1000.0;
        public double ErrorPercent => Count > 0 ? Errors * 100.0 / Count : 0;

        /// <summary>
        /// A percentile in ms (needs the histogram).
        /// </summary>
        public double PercentileMs(double p) => LatencyHistogram.Percentile(Hist, Count, p, MinUs, MaxUs) / 1000.0;
    }

    /// <summary>
    /// One request, kept for the request details.
    /// </summary>
    public struct BenchSample
    {
        public long Seq;
        public long StartUs;
        public int EndPoint;
        public int Worker;
        public int Status;
        public int DurationUs;
        public int TtfbUs;
        public int WireSent;
        public int WireRecv;
        public int BodySent;
        public int BodyRecv;
        public bool NewConnection;
        public String Error;
    }

    /// <summary>
    /// The counters of one error (end point, status and message).
    /// </summary>
    public sealed class BenchError
    {
        public int EndPoint;
        public int Status;
        public String Message;
        public long Count;
        public long FirstUs = long.MaxValue;
        public long LastUs;
    }

    /// <summary>
    /// The counters of the wire bytes of one worker (all its connections).
    /// Only the connection of the worker writes them (one request at a time, no interlocked operations), other threads read them.
    /// </summary>
    public sealed class WireCounters
    {
        public long Sent;
        public long Recv;
        public long Connections;
    }

    /// <summary>
    /// A stream that counts the bytes read and written (wraps the network stream of a connection, below TLS: wire bytes including headers and TLS).
    /// </summary>
    sealed class CountingStream : Stream
    {
        public CountingStream(Stream inner, WireCounters counters)
        {
            Inner = inner;
            Counters = counters;
            Volatile.Write(ref counters.Connections, counters.Connections + 1);
        }

        readonly Stream Inner;
        readonly WireCounters Counters;

        public override bool CanRead => Inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => Inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => Inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => Inner.FlushAsync(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Inner.Read(buffer, offset, count);
            Volatile.Write(ref Counters.Recv, Counters.Recv + n);
            return n;
        }

        public override int Read(Span<byte> buffer)
        {
            var n = Inner.Read(buffer);
            Volatile.Write(ref Counters.Recv, Counters.Recv + n);
            return n;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var n = await Inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref Counters.Recv, Counters.Recv + n);
            return n;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Write(byte[] buffer, int offset, int count)
        {
            Inner.Write(buffer, offset, count);
            Volatile.Write(ref Counters.Sent, Counters.Sent + count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Inner.Write(buffer);
            Volatile.Write(ref Counters.Sent, Counters.Sent + buffer.Length);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref Counters.Sent, Counters.Sent + buffer.Length);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Inner.Dispose();
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync() => Inner.DisposeAsync();
    }
}
