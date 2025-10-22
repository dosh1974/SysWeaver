
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System;
using System.IO;
using System.Threading;

namespace SysWeaver.Security
{

    /// <summary>
    /// Creates a self sigend certificate
    /// </summary>
    public sealed class SelfSignedCertificateProvider : ICertificateProvider, IDisposable
    {
        public override string ToString() => Filename;

        /// <summary>
        /// Creates a self sigend certificate
        /// </summary>
        /// <param name="p">Paramaters</param>
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
                                ExpireAction = Scheduler.Add(expires.AddHours(RenewBeforeExpirationHours), InvokeExpireSoon, true);
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
                ExpireAction = Scheduler.Add(c.GetExpiration().AddHours(RenewBeforeExpirationHours), InvokeExpireSoon, true);
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
            OnChanged?.Invoke();
        }

        /// <summary>
        /// An event that is fired if the certificate is about to expire.
        /// An application should restart (or re-init) to get the updated cert (calling GetCert again will return an updated cert).
        /// </summary>
        public event Action OnChanged;

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
