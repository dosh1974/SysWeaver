using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System;
using System.Threading;

namespace SysWeaver.Security
{


    /// <summary>
    /// Provides a certificate from a file (typically a .pfx file), using a <see cref="ManagedFile"/> so the location can be anything that
    /// <see cref="ManagedFileParams"/> supports and changes are detected.
    /// <see cref="OnChanged"/> is fired if the file is modified and the new file contains a different certificate.
    /// </summary>
    /// <remarks>
    /// The certificate is loaded lazily on the first call to <see cref="GetCert"/> and cached until the file changes.
    /// When the file changes, the previously returned certificate is disposed before <see cref="OnChanged"/> is raised, consumers should call <see cref="GetCert"/> again.
    /// Thread safe.
    /// </remarks>
    public sealed class FileCertificateProvider : ICertificateProvider, IDisposable
    {
        /// <inheritdoc/>
        public override string ToString() => P?.ToString();

        /// <summary>
        /// Create a provider that reads the certificate from a file (typically a .pfx file).
        /// Nothing is read until <see cref="GetCert"/> is called.
        /// </summary>
        /// <param name="p">Parameters, may not be null. The <see cref="FileCertificateProviderParams.CertPassword"/> is resolved using <see cref="PathTemplate"/> and may be the name of a file containing the password.</param>
        public FileCertificateProvider(FileCertificateProviderParams p)
        {
            P = p;
            CP = PathTemplate.Resolve(p.CertPassword);
        }

        readonly FileCertificateProviderParams P;
        readonly string CP;

        volatile X509Certificate2 C;

        readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Get the certificate, loading it from the file on first use (or after a change).
        /// </summary>
        /// <returns>The certificate (owned by the provider, do not dispose it).</returns>
        /// <exception cref="Exception">The file couldn't be read or the certificate couldn't be decoded with the configured password.</exception>
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
                OnChanged?.Invoke(null);
            }
        }

        ManagedFile MF;

        /// <summary>
        /// An event that is fired whenever the certificate file have changed and contains a different certificate.
        /// The argument is always null, call <see cref="GetCert"/> to get the new certificate.
        /// </summary>
        public event Action<X509Certificate2> OnChanged;


        /// <summary>
        /// Stop monitoring the file and dispose the cached certificate.
        /// </summary>
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
