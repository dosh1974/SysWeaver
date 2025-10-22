using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    sealed class DiscManagedFile : IManagedFileSource
    {

        public DiscManagedFile(ManagedFile manager, String filename, ManagedFileParams p, Func<ManagedFileData, Exception, Task> onChange, Func<Byte[], Byte[]> computeHash)
        {
            Fn = filename;
            ConputeHash = computeHash;
            A = onChange;
            F = new OnFileChangeAsync(filename, OnChange, p.LocalGraceTime);
            Manager = manager;
        }
        readonly ManagedFile Manager;
        readonly Func<Byte[], Byte[]> ConputeHash;

        public async Task<Tuple<ManagedFileData, Exception>> TryGetNow()
        {
            ManagedFileData data = null;
            Exception ex = null;
            var f = Fn;
            try
            {
                var fi = new FileInfo(f).LastWriteTimeUtc;
                var b = await File.ReadAllBytesAsync(f).ConfigureAwait(false);
                data = new ManagedFileData(f, b, fi, ConputeHash(b), Manager);
            }
            catch (Exception e)
            {
                ex = e;
            }
            return Tuple.Create(data, ex);
        }

        readonly String Fn;
        readonly Func<ManagedFileData, Exception, Task> A;

        async Task OnChange(String f)
        {
            var r = await TryGetNow().ConfigureAwait(false);
            await A(r.Item1, r.Item2).ConfigureAwait(false);
        }

        OnFileChangeBase F;

        public void Dispose()
        {
            Interlocked.Exchange(ref F, null)?.Dispose();
        }


    }

}
