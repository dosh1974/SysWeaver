using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    sealed class DiscManagedFile : IManagedFileSource
    {

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

        public void Dispose()
        {
            Interlocked.Exchange(ref F, null)?.Dispose();
        }


    }

}
