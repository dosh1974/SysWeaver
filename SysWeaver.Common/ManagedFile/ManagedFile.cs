using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{


    /// <summary>
    /// An object that represents a file.
    /// The file can be located locally on disc or remote using http/https.
    /// When the file change, the data is read and a callback is triggered
    /// </summary>
    public sealed class ManagedFile : IDisposable
    {
        public override string ToString() => Location;

        /// <summary>
        /// Create a managed file object.
        /// The file can be located locally on disc or remote using http/https.
        /// When the file change, the data is read and a callback is triggered
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="onChange">The callback to invoke whenever the file data has changed</param>
        public ManagedFile(ManagedFileParams p, Func<ManagedFileData, Task> onChange = null)
        {
            A = onChange;
            HashCheck = p.HashCheck;
            Location = p.Location;
            Source = GetSource(p);
        }

        /// <summary>
        /// Create a managed file object.
        /// The file can be located locally on disc or remote using http/https.
        /// When the file change, the data is read and a callback is triggered
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="onChange">The callback to invoke whenever the file data has changed</param>
        public ManagedFile(ManagedFileParams p, Action<ManagedFileData> onChange = null)
        {
            S = onChange;
            HashCheck = p.HashCheck;
            Location = p.Location;
            Source = GetSource(p);
        }

        /// <summary>
        /// Get the current state of the file.
        /// If the file haven't been read yet, try to read it.
        /// May throw an exception if the read fails.
        /// </summary>
        /// <returns>The managed file data</returns>
        public async Task<ManagedFileData> TryGetNowAsync()
        {
            var x = InternalData;
            if (x != null)
                return x;
            var r = await Source.TryGetNow().ConfigureAwait(false);
            var ex = r.Item2;
            if (ex != null)
            {
                Exceptions.OnException(ex);
                throw ex;
            }
            var data = r.Item1;
            Interlocked.Exchange(ref InternalData, data);
            Interlocked.Increment(ref InternalChangeCount);
            return data;
        }

        /// <summary>
        /// Get the current state of the file.
        /// If the file haven't been read yet, try to read it.
        /// May throw an exception if the read fails.
        /// </summary>
        /// <returns>The managed file data</returns>
        public ManagedFileData TryGetNow()
        {
            var x = InternalData;
            if (x != null)
                return x;
            var r = Source.TryGetNow().RunAsync();
            var ex = r.Item2;
            if (ex != null)
            {
                Exceptions.OnException(ex);
                throw ex;
            }
            var data = r.Item1;
            Interlocked.Exchange(ref InternalData, data);
            return data;
        }

        /// <summary>
        /// The location of the file.
        /// </summary>
        public readonly String Location;

        /// <summary>
        /// Exception tracking.
        /// </summary>
        public readonly ExceptionTracker Exceptions = new ExceptionTracker();

        /// <summary>
        /// Number of time the data have been changed (including the first successfull read).
        /// </summary>
        public long ChangeCount => Interlocked.Read(ref InternalChangeCount);

        /// <summary>
        /// Number of times the hash have been equal and thus a change have been ignored.
        /// </summary>
        public long HashEqualCount => Interlocked.Read(ref InternalHashEqualCount);

        /// <summary>
        /// Get the current data (no reading will be done, may be null if the file haven't been read yet)
        /// </summary>
        public ManagedFileData CurrentData => InternalData;

        public void Dispose()
        {
            Interlocked.Exchange(ref Source, null)?.Dispose();
            InternalData = null;
        }

        public static bool TryAddSchema(String schema, Func<ManagedFile, String, ManagedFileParams, Func<ManagedFileData, Exception, Task>, Func<Byte[], Byte[]>, IManagedFileSource> sourceCreator)
        {
            var s = SourceSchemaCreator;
            lock (s)
                return s.TryAdd(schema, sourceCreator);
        }

        public static bool TryRemoveSchema(String schema, Func<ManagedFile, String, ManagedFileParams, Func<ManagedFileData, Exception, Task>, Func<Byte[], Byte[]>, IManagedFileSource> sourceCreator)
        {
            var s = SourceSchemaCreator;
            lock (s)
            {
                if (!s.TryGetValue(schema, out var fn))
                    return false;
                if (fn != sourceCreator)
                    return false;
                return s.TryRemove(schema, out fn);
            }
        }

        static ManagedFile()
        {
            var s = new ConcurrentDictionary<string, Func<ManagedFile, String, ManagedFileParams, Func<ManagedFileData, Exception, Task>, Func<byte[], byte[]>, IManagedFileSource>>(StringComparer.Ordinal);
            SourceSchemaCreator = s;
            s.TryAdd("file", (m, l, p, t, h) => new DiscManagedFile(m, l, p, t, h));
            s.TryAdd("http", (m, l, p, t, h) => new HttpManagedFile(m, l, p, t, h));
            s.TryAdd("https", (m, l, p, t, h) => new HttpManagedFile(m, l, p, t, h));
        }

        static readonly ConcurrentDictionary<String, Func<ManagedFile, String, ManagedFileParams, Func<ManagedFileData, Exception, Task>, Func<Byte[], Byte[]>, IManagedFileSource>> SourceSchemaCreator; 

        IManagedFileSource GetSource(ManagedFileParams p)
        {
            var loc = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.Location));
            var hash = p.HashCheck ? Hash : NoHash;
            var t = loc.IndexOf("://");
            if (t < 0)
                return new DiscManagedFile(this, loc, p, OnChange, hash);
            var schema = loc.Substring(0, t);
            if (SourceSchemaCreator.TryGetValue(schema, out var fn))
                return fn(this, loc, p, OnChange, hash);
            throw new Exception("Unsupported schema " + schema.ToQuoted());
        }

        static readonly Func<Byte[], Byte[]> Hash = MD5.HashData;
        static readonly Func<Byte[], Byte[]> NoHash = x => null;

        readonly bool HashCheck;
        readonly Func<ManagedFileData, Task> A;
        readonly Action<ManagedFileData> S;

        long InternalChangeCount;
        long InternalHashEqualCount;

        async Task OnChange(ManagedFileData data, Exception ex)
        {
            if (ex != null)
            {
                Exceptions.OnException(ex);
                return;
            }
            var old = Interlocked.Exchange(ref InternalData, data);
            Interlocked.Increment(ref InternalChangeCount);
            if (HashCheck)
            {
                if (old != null)
                {
                    if (old.Hash.SequenceEqual(data.Hash))
                    {
                        Interlocked.Increment(ref InternalHashEqualCount);
                        return;
                    }
                }
            }
            var a = A;
            if (a != null)
            {
                try
                {
                    await a(data).ConfigureAwait(false);
                }
                catch (Exception ex2)
                {
                    Exceptions.OnException(ex2);
                }
                return;
            }
            var s = S;
            if (s == null)
            {
                return;
            }
            try
            {
                s(data);
            }
            catch (Exception ex2)
            {
                Exceptions.OnException(ex2);
            }
        }


        ManagedFileData InternalData;

        IManagedFileSource Source;

    }

}
