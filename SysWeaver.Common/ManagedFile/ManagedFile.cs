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
    /// <remarks>
    /// The location is resolved using PathTemplate.Resolve and made absolute (relative to the executable) before the source is selected.
    /// A location without "://" is a local file, else the schema selects the source ("http" and "https" are built in, more can be added using <see cref="TryAddSchema"/>).
    /// Exceptions from reading or from the callbacks are never thrown on change, they are collected in <see cref="Exceptions"/>.
    /// </remarks>
    public sealed class ManagedFile : IDisposable
    {
        /// <summary>
        /// The location as specified in the parameters
        /// </summary>
        /// <returns>The location</returns>
        public override string ToString() => Location;

        /// <summary>
        /// Create a managed file object.
        /// The file can be located locally on disc or remote using http/https.
        /// When the file change, the data is read and a callback is triggered
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="onChange">The callback to invoke whenever the file data has changed</param>
        /// <exception cref="Exception">The schema of the location isn't supported</exception>
        /// <remarks>Both constructors have an optional callback, so a call without a callback must specify the type of the null argument</remarks>
        public ManagedFile(ManagedFileParams p, Func<ManagedFileData, Task> onChange = null)
        {
            MustExist = p.MustExist;
            A = onChange;
            HashCheck = p.HashCheck;
            Location = p.Location;
            Source = GetSource(p);
        }

        readonly bool MustExist;

        /// <summary>
        /// Create a managed file object.
        /// The file can be located locally on disc or remote using http/https.
        /// When the file change, the data is read and a callback is triggered
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="onChange">The callback to invoke whenever the file data has changed</param>
        /// <exception cref="Exception">The schema of the location isn't supported</exception>
        public ManagedFile(ManagedFileParams p, Action<ManagedFileData> onChange = null)
        {
            MustExist = p.MustExist;
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
        /// <returns>The managed file data (check <see cref="ManagedFileData.Ex"/> if <see cref="ManagedFileParams.MustExist"/> is false)</returns>
        /// <exception cref="Exception">The read failed and <see cref="ManagedFileParams.MustExist"/> is true (the exception of the read)</exception>
        /// <remarks>
        /// The result of the first read is cached until a change is detected (also a failed read if <see cref="ManagedFileParams.MustExist"/> is false).
        /// A failed read is not cached if <see cref="ManagedFileParams.MustExist"/> is true, so the next call reads again (and throws again if it still fails).
        /// The read isn't synchronized, concurrent first calls may read the file more than once.
        /// </remarks>
        public async Task<ManagedFileData> TryGetNowAsync()
        {
            var x = InternalData;
            if (x != null)
                return x;
            var data = await Source.TryGetNow().ConfigureAwait(false);
            var ex = data.Ex;
            //  A failed read that throws isn't cached (else later calls would return the error data without throwing)
            if ((ex == null) || (!MustExist))
                Interlocked.Exchange(ref InternalData, data);
            if (ex != null)
            {
                Exceptions.OnException(ex);
                if (MustExist)
                    throw ex;
            }else 
                Interlocked.Increment(ref InternalChangeCount);
            return data;
        }

        /// <summary>
        /// Get the current state of the file.
        /// If the file haven't been read yet, try to read it.
        /// May throw an exception if the read fails.
        /// </summary>
        /// <returns>The managed file data (check <see cref="ManagedFileData.Ex"/> if <see cref="ManagedFileParams.MustExist"/> is false)</returns>
        /// <exception cref="Exception">The read failed and <see cref="ManagedFileParams.MustExist"/> is true (the exception of the read)</exception>
        /// <remarks>
        /// The result of the first read is cached until a change is detected (also a failed read if <see cref="ManagedFileParams.MustExist"/> is false).
        /// A failed read is not cached if <see cref="ManagedFileParams.MustExist"/> is true, so the next call reads again (and throws again if it still fails).
        /// The sync version blocks on the async read (sync over async).
        /// </remarks>
        public ManagedFileData TryGetNow()
        {
            var x = InternalData;
            if (x != null)
                return x;
            var data = Source.TryGetNow().RunAsync();
            var ex = data.Ex;
            //  A failed read that throws isn't cached (else later calls would return the error data without throwing)
            if ((ex == null) || (!MustExist))
                Interlocked.Exchange(ref InternalData, data);
            if (ex != null)
            {
                Exceptions.OnException(ex);
                if (MustExist)
                    throw ex;
            }
            else
                Interlocked.Increment(ref InternalChangeCount);
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
        /// Number of time the data have been changed (including the first successful read, and changes ignored because the hash was equal).
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

        /// <summary>
        /// Stop monitoring the file and release the current data (the object must not be used after this)
        /// </summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref Source, null)?.Dispose();
            InternalData = null;
        }

        /// <summary>
        /// Register a source creator for a location schema (the part before "://", case sensitive), ex: "ftp".
        /// </summary>
        /// <param name="schema">The schema</param>
        /// <param name="sourceCreator">The function that creates a source, the arguments are: the manager, the resolved location, the parameters, the change callback and the hash function</param>
        /// <returns>True if added, false if the schema is already registered</returns>
        /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null</exception>
        public static bool TryAddSchema(String schema, Func<ManagedFile, String, ManagedFileParams, Func<ManagedFileData, Task>, Func<ReadOnlyMemory<Byte>, Byte[]>, IManagedFileSource> sourceCreator)
        {
            var s = SourceSchemaCreator;
            lock (s)
                return s.TryAdd(schema, sourceCreator);
        }

        /// <summary>
        /// Unregister a source creator for a location schema (only if the registered creator is the supplied one)
        /// </summary>
        /// <param name="schema">The schema</param>
        /// <param name="sourceCreator">The creator that was registered</param>
        /// <returns>True if removed, false if the schema isn't registered or is registered with another creator</returns>
        /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null</exception>
        public static bool TryRemoveSchema(String schema, Func<ManagedFile, String, ManagedFileParams, Func<ManagedFileData, Task>, Func<ReadOnlyMemory<Byte>, Byte[]>, IManagedFileSource> sourceCreator)
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
            var s = new ConcurrentDictionary<string, Func<ManagedFile, String, ManagedFileParams, Func<ManagedFileData, Task>, Func<ReadOnlyMemory<Byte>, Byte[]>, IManagedFileSource>>(StringComparer.Ordinal);
            SourceSchemaCreator = s;
            //  The disc source wants a local path, not the url
            s.TryAdd("file", (m, l, p, t, h) => new DiscManagedFile(m, new Uri(l).LocalPath, p, t, h));
            s.TryAdd("http", (m, l, p, t, h) => new HttpManagedFile(m, l, p, t, h));
            s.TryAdd("https", (m, l, p, t, h) => new HttpManagedFile(m, l, p, t, h));
        }

        static readonly ConcurrentDictionary<String, Func<ManagedFile, String, ManagedFileParams, Func<ManagedFileData, Task>, Func<ReadOnlyMemory<Byte>, Byte[]>, IManagedFileSource>> SourceSchemaCreator; 

        IManagedFileSource GetSource(ManagedFileParams p)
        {
            var loc = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.Location));
            var hash = p.HashCheck ? Hash : NoHash;
            var t = loc.FastIndexOf("://");
            if (t < 0)
                return new DiscManagedFile(this, loc, p, OnChange, hash);
            var schema = loc.Substring(0, t);
            if (SourceSchemaCreator.TryGetValue(schema, out var fn))
                return fn(this, loc, p, OnChange, hash);
            throw new Exception("Unsupported schema " + schema.ToQuoted());
        }

        static readonly Func<ReadOnlyMemory<Byte>, Byte[]> Hash = m => MD5.HashData(m.Span);
        static readonly Func<ReadOnlyMemory<Byte>, Byte[]> NoHash = x => null;

        readonly bool HashCheck;
        readonly Func<ManagedFileData, Task> A;
        readonly Action<ManagedFileData> S;

        long InternalChangeCount;
        long InternalHashEqualCount;

        /// <summary>
        /// Called by the source when the file has changed, updates the current data and invokes the callback (unless the hash is unchanged)
        /// </summary>
        async Task OnChange(ManagedFileData data)
        {
            var ex = data.Ex;
            if (ex != null)
            {
                Exceptions.OnException(ex);
                return;
            }
            var old = Interlocked.Exchange(ref InternalData, data);
            Interlocked.Increment(ref InternalChangeCount);
            if (HashCheck)
            {
                //  Data from a failed read has no hash, treat it as different
                if (old?.Hash != null && data.Hash != null)
                {
                    if (old.Hash.AsSpan().SequenceEqual(data.Hash))
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
