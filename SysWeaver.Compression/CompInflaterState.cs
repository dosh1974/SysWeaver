using System;
using System.IO;
using System.IO.Compression;
using System.Linq.Expressions;
using System.Reflection;

namespace SysWeaver.Compression
{
    /// <summary>
    /// Detects truncated data in the .NET DeflateStream / GZipStream.
    /// The streams silently return the data decompressed so far when the compressed data is truncated
    /// (unless the process wide "System.IO.Compression.UseStrictValidation" switch is set, that also rejects empty data).
    /// This reads the state of the internal inflater, if the internals change (in a future .NET version) the check is skipped.
    /// </summary>
    static class CompInflaterState
    {
        /// <summary>
        /// The state of an inflater
        /// </summary>
        enum State
        {
            /// <summary>
            /// Done (or unknown)
            /// </summary>
            Done = 0,
            /// <summary>
            /// The data is truncated
            /// </summary>
            Truncated = 1,
            /// <summary>
            /// There was no data at all
            /// </summary>
            Empty = 2,
        }

        /// <summary>
        /// Get the state of a DeflateStream (compiled once, reads the internal fields without any allocations)
        /// </summary>
        static readonly Func<DeflateStream, State> GetDeflateState = BuildDeflate();

        /// <summary>
        /// Get the inner DeflateStream of a GZipStream
        /// </summary>
        static readonly Func<GZipStream, DeflateStream> GetGZipDeflate = BuildGZip();

        /// <summary>
        /// Empty data is valid if the encoder of this .NET version produces no data for empty data (true for .NET 10 and earlier)
        /// </summary>
        static readonly bool DeflateEmptyIsValid = EncodesEmptyAsEmpty(s => new DeflateStream(s, CompressionLevel.Fastest, true));

        /// <summary>
        /// Empty data is valid if the encoder of this .NET version produces no data for empty data (true for .NET 10 and earlier)
        /// </summary>
        static readonly bool GZipEmptyIsValid = EncodesEmptyAsEmpty(s => new GZipStream(s, CompressionLevel.Fastest, true));

        static bool EncodesEmptyAsEmpty(Func<Stream, Stream> create)
        {
            using var ms = new MemoryStream();
            using (create(ms))
            {
            }
            return ms.Length == 0;
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        static Func<DeflateStream, State> BuildDeflate()
        {
            try
            {
                var inflaterField = typeof(DeflateStream).GetField("_inflater", Flags);
                var inflaterType = inflaterField?.FieldType;
                var nonEmptyInput = inflaterType?.GetField("_nonEmptyInput", Flags);
                var finished = inflaterType?.GetField("_finished", Flags);
                if ((nonEmptyInput == null) || (finished == null) || (nonEmptyInput.FieldType != typeof(bool)) || (finished.FieldType != typeof(bool)))
                    return null;
                //  s => s._inflater is not { } i ? Done : !i._nonEmptyInput ? Empty : i._finished ? Done : Truncated
                var s = Expression.Parameter(typeof(DeflateStream), "s");
                var i = Expression.Variable(inflaterType, "i");
                var done = Expression.Constant(State.Done);
                var body = Expression.Block(typeof(State), [i],
                    Expression.Assign(i, Expression.Field(s, inflaterField)),
                    Expression.Condition(Expression.Equal(i, Expression.Constant(null, inflaterType)), done,
                        Expression.Condition(Expression.Not(Expression.Field(i, nonEmptyInput)), Expression.Constant(State.Empty),
                            Expression.Condition(Expression.Field(i, finished), done, Expression.Constant(State.Truncated)))));
                return Expression.Lambda<Func<DeflateStream, State>>(body, s).Compile();
            }
            catch
            {
                return null;
            }
        }

        static Func<GZipStream, DeflateStream> BuildGZip()
        {
            try
            {
                var field = typeof(GZipStream).GetField("_deflateStream", Flags);
                if (field?.FieldType != typeof(DeflateStream))
                    return null;
                var s = Expression.Parameter(typeof(GZipStream), "s");
                return Expression.Lambda<Func<GZipStream, DeflateStream>>(Expression.Field(s, field), s).Compile();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Call when all data have been read from a decompression stream (and before it's disposed).
        /// Throws an InvalidDataException if the compressed data was truncated.
        /// Empty compressed data is valid if the encoder of the current .NET version produces no data for empty data (.NET 10 and earlier).
        /// </summary>
        /// <param name="s">A DeflateStream or GZipStream in decompression mode</param>
        /// <exception cref="InvalidDataException"></exception>
        public static void ThrowIfTruncated(Stream s)
        {
            var getState = GetDeflateState;
            if (getState == null)
                return;
            var d = s as DeflateStream;
            var emptyIsValid = DeflateEmptyIsValid;
            if ((d == null) && (s is GZipStream g))
            {
                d = GetGZipDeflate?.Invoke(g);
                emptyIsValid = GZipEmptyIsValid;
            }
            if (d == null)
                return;
            var state = getState(d);
            if ((state == State.Truncated) || ((state == State.Empty) && !emptyIsValid))
                throw new InvalidDataException(CompHelpers.DecTruncated);
        }
    }
}
