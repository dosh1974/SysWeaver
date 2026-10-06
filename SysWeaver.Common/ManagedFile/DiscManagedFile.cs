using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// A managed file source for a local file, changes are detected using a <see cref="OnFileChangeAsync"/> (with <see cref="ManagedFileParams.LocalGraceTime"/> as the delay)
    /// </summary>
    sealed class DiscManagedFile : IManagedFileSource
    {

        /// <summary>
        /// Start monitoring a local file
        /// </summary>
        /// <param name="manager">The owning managed file (stored in the returned data)</param>
        /// <param name="filename">The local file name</param>
        /// <param name="p">The parameters</param>
        /// <param name="onChange">Invoked with the new data (or an error) when a change is detected</param>
        /// <param name="computeHash">Computes the hash of the data (may return null if hashing is disabled)</param>
        public DiscManagedFile(ManagedFile manager, String filename, ManagedFileParams p, Func<ManagedFileData, Task> onChange, Func<ReadOnlyMemory<Byte>, Byte[]> computeHash)
        {
            Fn = filename;
            ComputeHash = computeHash;
            A = onChange;
            F = new OnFileChangeAsync(filename, OnChange, p.LocalGraceTime);
            Manager = manager;
        }
        readonly ManagedFile Manager;
        readonly Func<ReadOnlyMemory<Byte>, Byte[]> ComputeHash;

        /// <summary>
        /// Read the file now
        /// </summary>
        /// <returns>The data, or a data object with the exception (<see cref="ManagedFileData.Ex"/>) if the read failed (never null, never throws)</returns>
        public async Task<ManagedFileData> TryGetNow()
        {
            ManagedFileData data = null;
            Exception ex = null;
            var f = Fn;
            try
            {
                var fi = new FileInfo(f).LastWriteTimeUtc;
                var b = await FileExt.ReadBytesAsync(f).ConfigureAwait(false);
                data = new ManagedFileData(f, b, fi, ComputeHash(b), Manager, null);
            }
            catch (Exception e)
            {
                ex = e;
                data = new ManagedFileData(f, Memory<Byte>.Empty, DateTime.MinValue, null, Manager, ex);
            }
            return data;
        }

        readonly String Fn;
        readonly Func<ManagedFileData, Task> A;

        async Task OnChange(String f)
        {
            var r = await TryGetNow().ConfigureAwait(false);
            await A(r).ConfigureAwait(false);
        }

        OnFileChangeBase F;

        /// <summary>
        /// Stop monitoring the file
        /// </summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref F, null)?.Dispose();
        }


    }

}
