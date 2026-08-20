using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Security;

namespace SysWeaver.MicroService
{
    public sealed partial class LanCertificateManagerService
    {
        sealed class Domain : IDisposable
        {
            public void Dispose()
            {
                var p = Provider;
                Provider.OnChanged -= OnChanged;
            }

            readonly LanCertificateManagerService S;

            public Domain(String domainName, LanCertificateManagerParams p, IMessageHost msg, LanCertificateManagerService s)
            {
                var ap = new AcmeCertificateParams();
                S = s;
                ap.CopyFrom(p);
                ap.AuthApi = p.AuthApi;
                ap.DomainName = domainName;
                DomainName = domainName;
                Provider = new AcmeCertificateProvider(msg, ap);
                Provider.OnChanged += OnChanged;
            }

            public long LastChecked;

            public readonly ExceptionTracker CertErrors = new();


            void OnChanged(X509Certificate2 c)
            {
                if (c != null)
                {
                    Interlocked.Exchange(ref Data, new CertData(c));
                    S.OnChange(this);
                }else
                {
                    CertErrors.OnException(new Exception("Failed to get certificate"));
                }
                Interlocked.Exchange(ref LastChecked, DateTime.UtcNow.Ticks);
            }


            static readonly AsyncLock Lock = new AsyncLock();

            public async Task UpdateCert()
            {
                using var _ = await Lock.Lock().ConfigureAwait(false);
                try
                {
                    var c = await Provider.GetCert().ConfigureAwait(false);
                    if (c != null)
                    {
                        Interlocked.Exchange(ref Data, new CertData(c));
                        S.OnChange(this);
                    }else
                    {
                        CertErrors.OnException(new Exception("Failed to get certificate"));
                    }
                }
                catch (Exception ex)
                {
                    S.CertErrors.OnException(ex);
                    CertErrors.OnException(ex);
                }
                Interlocked.Exchange(ref LastChecked, DateTime.UtcNow.Ticks);
            }


            public readonly String DomainName;
            public readonly AcmeCertificateProvider Provider;

            public CertData Data;
        }



    }
}
