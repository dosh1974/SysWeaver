using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System;
using System.Threading;

namespace SysWeaver.Security
{


    /// <summary>
    /// Provides a certificate from a file (typically a .pfx file).
    /// OnChanged is fired if the file is modified. 
    /// </summary>
    public sealed class FileCertificateProvider : ICertificateProvider, IDisposable
    {
        public override string ToString() => P?.ToString();

        /// <summary>
        /// Provides a certificate from a file (typically a .pfx file)
        /// OnChanged is fired if the file is modified. 
        /// </summary>
        /// <param name="p">Paramaters</param>
        public FileCertificateProvider(FileCertificateProviderParams p)
        {
            P = p;
            CP = PathTemplate.Resolve(p.CertPassword);
        }

        readonly FileCertificateProviderParams P;
        readonly string CP;

        volatile X509Certificate2 C;

        readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        public async Task<X509Certificate2> GetCert()
        {
            var c = C;
            if (c != null)
                return c;
            var l = Lock;
            await l.WaitAsync().ConfigureAwait(false);
            try
            {
                c = C;
                if (c != null)
                    return c;
                var fw = MF;
                if (fw == null)
                {
                    fw = new ManagedFile(P, InvokeChange);
                    MF = fw;
                }
                var data = await fw.TryGetNowAsync().ConfigureAwait(false);
                c = await CertificateTools.Create(data.Data, CP).ConfigureAwait(false);
                C = c;
                return c;
            }
            finally
            {
                l.Release();
            }
        }

        async Task InvokeChange(ManagedFileData data)
        {
            bool ok = false;
            var l = Lock;
            await l.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var c = await CertificateTools.Create(data.Data, CP).ConfigureAwait(false))
                    ok = c.GetCertHashString() != C?.GetCertHashString();
            }
            catch
            {
            }
            finally
            {
                l.Release();
            }
            if (ok)
            {
                Interlocked.Exchange(ref C, null)?.Dispose();
                OnChanged?.Invoke();
            }
        }

        ManagedFile MF;

        /// <summary>
        /// An event that is fired whenever the certificate file have changed
        /// </summary>
        public event Action OnChanged;


        public void Dispose()
        {
            var l = Lock;
            l.Wait();
            Interlocked.Exchange(ref MF, null)?.Dispose();
            Interlocked.Exchange(ref C, null)?.Dispose();
            l.Release();
        }

    }



}
