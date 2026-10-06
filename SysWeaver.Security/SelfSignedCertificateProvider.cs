
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System;
using System.IO;
using System.Threading;

namespace SysWeaver.Security
{

    /// <summary>
    /// Provides a self-signed certificate, generated using <see cref="SignedCertificateCreator.CreateSelfSigned"/>.
    /// The certificate is cached in a pfx file (and a PEM ".crt" file) and reused as long as it matches the parameters and isn't about to expire.
    /// </summary>
    /// <remarks>
    /// When the certificate is about to expire (see <see cref="CertificateProviderParams.RenewBeforeExpirationHours"/>), the cached certificate is disposed and <see cref="OnChanged"/> is raised,
    /// the next call to <see cref="GetCert"/> creates a new certificate.
    /// Self-signed certificates are only trusted by clients that explicitly trust them.
    /// Thread safe.
    /// </remarks>
    public sealed class SelfSignedCertificateProvider : ICertificateProvider, IDisposable
    {
        /// <inheritdoc/>
        public override string ToString() => Filename;

        /// <summary>
        /// Create a self-signed certificate provider, no certificate is created until <see cref="GetCert"/> is called.
        /// </summary>
        /// <param name="p">Parameters, null uses the defaults.</param>
        /// <exception cref="Exception">A subject field contains an invalid character.</exception>
        public SelfSignedCertificateProvider(SelfSignedCertificateProviderParams p = null)
        {
            p = p ?? new SelfSignedCertificateProviderParams();
            P = new SignedCertificateCreator(p);
            Filename = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.Filename));
            Password = EnvInfo.ResolveText(p.Password);
            MinValidHours = Math.Max(p.MinValidHours, 2);
            RenewBeforeExpirationHours = -Math.Max(p.RenewBeforeExpirationHours, (MinValidHours + 1) >> 1);
        }

        readonly int MinValidHours;
        readonly int RenewBeforeExpirationHours;
        readonly string Filename;
        readonly string Password;

        readonly SignedCertificateCreator P;

        X509Certificate2 C;

        readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        IDisposable ExpireAction;

        /// <summary>
        /// Get the certificate, loading it from the cache file or creating (and caching) a new one if needed.
        /// </summary>
        /// <returns>The certificate (owned by the provider, do not dispose it).</returns>
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
                Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
                var p = P;
                var f = Filename;
                var pw = Password;
                var haveFile = !String.IsNullOrEmpty(f);
            //  Try to load from file
                if (haveFile && File.Exists(f))
                {
                    try
                    {
                        c = await CertificateTools.Load(f, pw, false).ConfigureAwait(false);
                        if (!CertificateTools.IsSoonExpired(c, out var expires, MinValidHours))
                        {
                            if (p.IsSame(c))
                            {
                                Interlocked.Exchange(ref C, c)?.Dispose();
                                ExpireAction = Scheduler.Add(expires.AddHours(RenewBeforeExpirationHours), InvokeExpireSoon, "Self signed cert renewal");
                                return c;
                            }
                        }
                        c.Dispose();
                    }
                    catch
                    {
                    }
                }
                //  Must create a new
                c = p.CreateSelfSigned();
                if (haveFile)
                {
                    var dir = Path.GetDirectoryName(f);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(f, c.Export(X509ContentType.Pfx, pw)).ConfigureAwait(false);
                    //await File.WriteAllBytesAsync(Path.ChangeExtension(f, "crt"), c.Export(X509ContentType.Cert, (String)null)).ConfigureAwait(false);
                    await File.WriteAllTextAsync(Path.ChangeExtension(f, "crt"), c.ExportCertificatePem()).ConfigureAwait(false);
                }
                Interlocked.Exchange(ref C, c)?.Dispose();
                ExpireAction = Scheduler.Add(c.GetExpiration().AddHours(RenewBeforeExpirationHours), InvokeExpireSoon, "Self signed cert renewal");
                return c;
            }
            finally
            {
                l.Release();
            }
           
        }

        void InvokeExpireSoon()
        {
            Interlocked.Exchange(ref C, null)?.Dispose();
            OnChanged?.Invoke(null);
        }

        /// <summary>
        /// An event that is fired if the certificate is about to expire.
        /// The argument is always null and the old certificate has already been disposed, call <see cref="GetCert"/> again to get a renewed certificate.
        /// </summary>
        public event Action<X509Certificate2> OnChanged;

        /// <summary>
        /// Cancel the scheduled renewal and dispose the current certificate.
        /// </summary>
        public void Dispose()
        {
            var l = Lock;
            l.Wait();
            Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
            Interlocked.Exchange(ref C, null)?.Dispose();
            l.Release();
        }

    }


}
